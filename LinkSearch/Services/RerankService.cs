// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
    internal partial class RerankService : IDisposable
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
        {
 // #if DEBUG
 //             // 调试日志：验证构造函数被调用
 //             Log.Debug("RerankService 构造函数被调用");
 // #endif
             
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            // 使用 Helpers 中的共享 HttpClient 实例
            _httpClient = HttpClientProvider.Shared;
            // 使用静态 JsonSerializerOptions，避免每次构造
        }

        /// <summary>
        /// 对链接结果进行重新排序
        /// </summary>
        /// <param name="query">查询文本</param>
        /// <param name="linkResults">链接结果列表</param>
        /// <returns>重新排序后的链接结果列表</returns>
        public async Task<List<LinkResult>> RerankLinksAsync(string query, List<LinkResult> linkResults, System.Threading.CancellationToken cancellationToken = default)
        {
            var startTime = System.Diagnostics.Stopwatch.StartNew();
#if DEBUG
            Log.Debug($"开始对链接进行rerank，查询: {query}, 链接数量: {linkResults.Count}");
#endif
            
            // 检查是否启用rerank功能
            if (!_settingsManager.EnableRerank)
            {
#if DEBUG
                Log.Debug("Rerank功能未启用，返回原始结果");
#endif
                return linkResults;
            }

            // 检查API Key是否有效
            var apiKey = _settingsManager.RerankApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
#if DEBUG
                Log.Debug("Rerank API Key未设置，返回原始结果");
#endif
                return linkResults;
            }

            // 检查链接结果是否为空
            if (linkResults == null || linkResults.Count == 0)
            {
#if DEBUG
                Log.Debug("链接结果为空，返回原始结果");
#endif
                return linkResults ?? new List<LinkResult>();
            }

            try
            {
                // 将链接结果转换为文档格式
                var documentBuildStart = System.Diagnostics.Stopwatch.StartNew();
#if DEBUG
                Log.Debug($"开始构建文档，链接数量: {linkResults.Count}");
#endif
                
                // 减少中间分配：使用可复用的 StringBuilder，避免 LINQ/临时集合及多次构造 StringBuilder
                var documents = new string[linkResults.Count];
                var sb = new StringBuilder(256);
                for (int i = 0; i < linkResults.Count; i++)
                {
                    var link = linkResults[i];
                    sb.Clear();

                    sb.AppendLine(string.Format(CultureInfo.CurrentCulture, "名称: {0}", link.name));
                    sb.AppendLine(string.Format(CultureInfo.CurrentCulture, "描述: {0}", link.description));
                    sb.AppendLine(string.Format(CultureInfo.CurrentCulture, "URL: {0}", link.url));

                    // 直接拼接标签，避免先构造集合再 Join 导致的分配
                    if (link.tags != null && link.tags.Length > 0)
                    {
                        bool hasTag = false;
                        for (int ti = 0; ti < link.tags.Length; ti++)
                        {
                            var tagName = link.tags[ti]?.name;
                            if (string.IsNullOrEmpty(tagName))
                                continue;

                            if (!hasTag)
                            {
                                sb.Append("标签: ");
                                sb.Append(tagName);
                                hasTag = true;
                            }
                            else
                            {
                                sb.Append(", ");
                                sb.Append(tagName);
                            }
                        }

                        if (hasTag)
                        {
                            sb.AppendLine();
                        }
                    }

                    if (link.collection != null && !string.IsNullOrEmpty(link.collection.name))
                    {
                        sb.AppendLine(string.Format(CultureInfo.CurrentCulture, "集合: {0}", link.collection.name));
                    }

                    documents[i] = sb.ToString();
                }
                
                documentBuildStart.Stop();
#if DEBUG
                Log.Debug($"文档构建完成，耗时: {documentBuildStart.ElapsedMilliseconds}ms");
#endif

                // 创建rerank请求
                var rerankRequest = RerankRequest.Create(
                    query: query,
                    documents: documents,
                    model: _settingsManager.RerankModelName,
                    topN: documents.Length,
                    returnDocuments: false,
                    returnScores: true
                );

                // 调用rerank API
                var rerankResponse = await CallRerankApiAsync(rerankRequest, apiKey, cancellationToken).ConfigureAwait(false);
                
                if (rerankResponse == null || rerankResponse.Results == null || rerankResponse.Results.Length == 0)
                {
#if DEBUG
                    Log.Debug("Rerank API返回空结果，返回原始结果");
#endif
                    return linkResults;
                }

                // 根据rerank结果重新排序原始链接数据
                var rerankedResults = new List<LinkResult>();
                foreach (var result in rerankResponse.Results)
                {
                    if (result.Index >= 0 && result.Index < linkResults.Count)
                    {
                        rerankedResults.Add(linkResults[result.Index]);
                    }
                }

#if DEBUG
                Log.Debug($"Rerank完成，重新排序后的链接数量: {rerankedResults.Count}");
                Log.Debug($"Rerank总耗时: {startTime.ElapsedMilliseconds}ms");
#endif
                return rerankedResults;
            }
            catch (Exception)
            {
                // 记录异常并返回原始结果
#if DEBUG
                Log.Debug($"Rerank过程中发生异常");
#endif
                
                return linkResults;
            }
        }

        /// <summary>
        /// 调用rerank API
        /// </summary>
        /// <param name="request">rerank请求</param>
        /// <param name="apiKey">API密钥</param>
        /// <returns>rerank响应</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2026")]
        [UnconditionalSuppressMessage("AOT", "IL3050")]
        private async Task<RerankResponse?> CallRerankApiAsync(RerankRequest request, string apiKey, System.Threading.CancellationToken cancellationToken = default)
        {
#if DEBUG
            Log.Debug("开始调用rerank API");
#endif
            
            var apiUrl = _settingsManager.RerankApiUrl;
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
#if DEBUG
                Log.Debug("Rerank API URL未设置");
#endif
                return null;
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
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl)
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
                    var errorContent = await response.Content.ReadAsStringAsync(effectiveToken).ConfigureAwait(false);
#if DEBUG
                    Log.Debug($"Rerank API请求失败，状态码: {response.StatusCode}, 响应内容: {errorContent}");
#endif
                    return null;
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
                return rerankResponse;
            }
            catch (OperationCanceledException) when (effectiveToken.IsCancellationRequested)
            {
                // 专门处理取消场景，记录为取消而非错误
                Log.Info("Rerank API 请求已取消");
                return null;
            }
            catch (HttpRequestException ex)
            {
#if DEBUG
                Log.Debug($"Rerank API HTTP请求异常: {ex.Message}");
#endif
                return null;
            }
            catch (TaskCanceledException ex) when (!effectiveToken.IsCancellationRequested)
            {
                // 超时等取消场景
#if DEBUG
                Log.Debug($"Rerank API任务取消异常（超时）: {ex.Message}");
#endif
                return null;
            }
            catch (JsonException ex)
            {
#if DEBUG
                Log.Debug($"Rerank API JSON序列化异常: {ex.Message}");
#endif
                return null;
            }
            catch (Exception ex)
            {
#if DEBUG
                Log.Debug($"Rerank API未预期的异常: {ex.Message}");
#endif
                return null;
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