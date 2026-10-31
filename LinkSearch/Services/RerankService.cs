// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LinkSearch.Helpers;
using LinkSearch.Models;

namespace LinkSearch.Services
{
    /// <summary>
    /// Rerank服务类，实现rerank API调用功能
    /// </summary>
    internal sealed partial class RerankService : IDisposable
    {
        private readonly SettingsManager _settingsManager;
        private readonly HttpClient _httpClient;
        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        // 服务级取消令牌源：在 Provider.Dispose 时取消，确保 HTTP 请求可被及时终止
        private readonly CancellationTokenSource _serviceCts = new();
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="settingsManager">设置管理器</param>
        public RerankService(SettingsManager settingsManager)
            : this(settingsManager, HttpClientProvider.Shared)
        {
        }

        internal RerankService(SettingsManager settingsManager, HttpClient httpClient)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// 对 Linkwarden 链接进行重新排序
        /// </summary>
        /// <param name="query">查询文本</param>
        /// <param name="links">Linkwarden 链接列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>重新排序后的 Linkwarden 链接列表</returns>
        public async Task<IReadOnlyList<LinkwardenLink>> RerankLinksAsync(string query, IReadOnlyList<LinkwardenLink> links, CancellationToken cancellationToken = default)
        {
            return (await RankLinksAsync(query, links, cancellationToken).ConfigureAwait(false)).Links;
        }

        internal async Task<RankingOutcome> RankLinksAsync(string query, IReadOnlyList<LinkwardenLink> items, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (items.Count < 2 || !_settingsManager.EnableRerank)
            {
                return new RankingOutcome(items, RankingStatus.Skipped);
            }

            var mode = _settingsManager.RankingMode;
            var provider = _settingsManager.DecisionProvider;
            var apiKey = mode == RankingMode.Rerank ? _settingsManager.RerankApiKey : _settingsManager.GetDecisionApiKey(provider);
            var apiUrl = mode == RankingMode.Rerank ? _settingsManager.RerankApiUrl : _settingsManager.GetDecisionApiUrl(provider);
            var model = mode == RankingMode.Rerank ? _settingsManager.RerankModelName : _settingsManager.GetDecisionModelName(provider);
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiUrl) || string.IsNullOrWhiteSpace(model))
            {
                return new RankingOutcome(items, RankingStatus.ConfigurationError);
            }

            try
            {
                var documents = new string[Math.Min(items.Count, _settingsManager.RankingCandidateLimit)];
                for (var i = 0; i < documents.Length; i++)
                {
                    documents[i] = BuildDocument(items[i]);
                }

                (RerankResponse? Response, int? HttpStatusCode) apiResult;
                if (mode == RankingMode.Rerank)
                {
                    var rerankRequest = RerankRequest.Create(query, documents, model, documents.Length, false, true);
                    apiResult = await CallRerankApiAsync(rerankRequest, apiUrl, apiKey, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var bytes = DecisionRanking.CreateRequest(mode, model, query, documents, provider);
                    apiResult = await CallDecisionApiAsync(bytes, mode, provider, documents.Length, apiUrl, apiKey, cancellationToken).ConfigureAwait(false);
                }

                if (apiResult.Response?.Results is not { Length: > 0 } results ||
                    !Array.Exists(results, result => result.Index >= 0 && result.Index < documents.Length))
                {
                    return new RankingOutcome(items, RankingStatus.Failed, apiResult.HttpStatusCode);
                }

                return new RankingOutcome(ApplyRerankOrder(items, apiResult.Response, documents.Length), RankingStatus.Applied);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Debug($"Rerank failed and original order will be used: {ex.Message}");
                return new RankingOutcome(items, RankingStatus.Failed);
            }
        }

        private static string BuildDocument(LinkwardenLink link)
        {
            var sb = new StringBuilder(256);
            sb.Append("Name: ").AppendLine(link.Name);
            if (!string.IsNullOrWhiteSpace(link.Description))
            {
                sb.Append("Description: ").AppendLine(link.Description);
            }
            if (link.Tags.Any(tag => !string.IsNullOrWhiteSpace(tag)))
            {
                sb.Append("Tags: ").AppendLine(string.Join(", ", link.Tags));
            }
            return sb.ToString();
        }

        private async Task<(RerankResponse? Response, int? HttpStatusCode)> CallDecisionApiAsync(byte[] body, RankingMode mode, DecisionProvider provider, int documentCount, string apiUrl, string apiKey, CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _serviceCts.Token);
            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log.Debug($"Decision API request failed with HTTP {(int)response.StatusCode}");
                return (null, (int)response.StatusCode);
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(linkedCts.Token).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(responseStream, cancellationToken: linkedCts.Token).ConfigureAwait(false);
            if (!DecisionRanking.TryParseResponse(json.RootElement, mode, documentCount, out var result, provider))
            {
                Log.Debug("Decision API returned an invalid response");
                return (null, null);
            }

            return (result, null);
        }

        internal static IReadOnlyList<T> ApplyRerankOrder<T>(IReadOnlyList<T> links, RerankResponse? response, int? rankedCount = null)
        {
            if (response?.Results is null || response.Results.Length == 0)
            {
                return links;
            }

            var ordered = new List<T>(links.Count);
            var used = new HashSet<int>();
            foreach (var result in response.Results)
            {
                if (result.Index >= 0 && result.Index < Math.Min(links.Count, rankedCount ?? links.Count) && used.Add(result.Index))
                {
                    ordered.Add(links[result.Index]);
                }
            }

            if (ordered.Count == 0)
            {
                return links;
            }

            for (var i = 0; i < links.Count; i++)
            {
                if (!used.Contains(i))
                {
                    ordered.Add(links[i]);
                }
            }

            return ordered;
        }

        /// <summary>
        /// 调用rerank API
        /// </summary>
        /// <param name="request">rerank请求</param>
        /// <param name="apiKey">API密钥</param>
        /// <returns>rerank响应</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2026")]
        [UnconditionalSuppressMessage("AOT", "IL3050")]
        private async Task<(RerankResponse? Response, int? HttpStatusCode)> CallRerankApiAsync(RerankRequest request, string apiUrl, string apiKey, CancellationToken cancellationToken = default)
        {
#if DEBUG
            Log.Debug("开始调用rerank API");
#endif
            
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
#if DEBUG
                Log.Debug("Rerank API URL未设置");
#endif
                return (null, null);
            }

            // 链接服务级取消令牌与外部传入的取消令牌，任一取消则请求取消
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _serviceCts.Token);
            var effectiveToken = linkedCts.Token;

            try
            {
                // 序列化请求为 UTF-8 字节，避免中间 string 带来的大对象分配
                var serializationStart = System.Diagnostics.Stopwatch.StartNew();
                var bytes = JsonSerializer.SerializeToUtf8Bytes(request, s_jsonSerializerOptions);
                serializationStart.Stop();
#if DEBUG
                Log.Debug($"JSON序列化完成，耗时: {serializationStart.ElapsedMilliseconds}ms，字节长度: {bytes.Length}");
#endif

                using var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                // 为每个请求创建 HttpRequestMessage，避免修改共享 HttpClient.DefaultRequestHeaders 导致并发竞态
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = content
                };
                requestMessage.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                // 发送请求（使用 ResponseHeadersRead 可在处理大响应时减少内存占用）
                var apiCallStart = System.Diagnostics.Stopwatch.StartNew();
#if DEBUG
                Log.Debug($"开始调用rerank API");
#endif

                using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, effectiveToken).ConfigureAwait(false);

                apiCallStart.Stop();
#if DEBUG
                Log.Debug($"API调用完成，耗时: {apiCallStart.ElapsedMilliseconds}ms");
                Log.Debug($"Rerank API响应状态码: {response.StatusCode}");
#endif

                if (!response.IsSuccessStatusCode)
                {
#if DEBUG
                    Log.Debug($"Rerank API请求失败，状态码: {response.StatusCode}");
#endif
                    return (null, (int)response.StatusCode);
                }

                // 使用流式反序列化，避免把整个响应先读入 string
                await using var responseStream = await response.Content.ReadAsStreamAsync(effectiveToken).ConfigureAwait(false);
                var deserializationStart = System.Diagnostics.Stopwatch.StartNew();
                var rerankResponse = await JsonSerializer.DeserializeAsync<RerankResponse>(responseStream, s_jsonSerializerOptions, effectiveToken).ConfigureAwait(false);
                deserializationStart.Stop();
#if DEBUG
                Log.Debug($"JSON反序列化完成，耗时: {deserializationStart.ElapsedMilliseconds}ms");
                Log.Debug("Rerank API调用成功");
#endif
                return (rerankResponse, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (_serviceCts.IsCancellationRequested)
            {
                Log.Info("Rerank API request was canceled because the service is disposing");
                return (null, null);
            }
            catch (TaskCanceledException ex) when (!effectiveToken.IsCancellationRequested)
            {
                Log.Debug($"Rerank API timed out: {ex.Message}");
                return (null, null);
            }
            catch (HttpRequestException ex)
            {
                Log.Debug($"Rerank API HTTP请求异常: {ex.Message}");
                return (null, null);
            }
            catch (JsonException ex)
            {
                Log.Debug($"Rerank API JSON序列化异常: {ex.Message}");
                return (null, null);
            }
            catch (Exception ex)
            {
                Log.Debug($"Rerank API未预期的异常: {ex.Message}");
                return (null, null);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                // 取消所有可能正在进行的 API 请求
                _serviceCts.Cancel();
                _serviceCts.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error($"RerankService.Dispose 释放异常: {ex.Message}");
            }
            // 不要释放共享 HttpClient（SharedHttpClient）
        }
    }
}
