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
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="settingsManager">设置管理器</param>
        public RerankService(SettingsManager settingsManager)
        {
// #if DEBUG
//             // 调试日志：验证构造函数被调用
//             System.Diagnostics.Debug.WriteLine("RerankService 构造函数被调用");
// #endif
            
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // 配置JSON序列化选项
            _jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        /// <summary>
        /// 对链接结果进行重新排序
        /// </summary>
        /// <param name="query">查询文本</param>
        /// <param name="linkResults">链接结果列表</param>
        /// <returns>重新排序后的链接结果列表</returns>
        public async Task<List<LinkResult>> RerankLinksAsync(string query, List<LinkResult> linkResults)
        {
            var startTime = System.Diagnostics.Stopwatch.StartNew();
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"开始对链接进行rerank，查询: {query}, 链接数量: {linkResults.Count}");
#endif
            
            // 检查是否启用rerank功能
            if (!_settingsManager.EnableRerank)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("Rerank功能未启用，返回原始结果");
#endif
                return linkResults;
            }

            // 检查API Key是否有效
            var apiKey = _settingsManager.RerankApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("Rerank API Key未设置，返回原始结果");
#endif
                return linkResults;
            }

            // 检查链接结果是否为空
            if (linkResults == null || linkResults.Count == 0)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("链接结果为空，返回原始结果");
#endif
                return linkResults ?? new List<LinkResult>();
            }

            try
            {
                // 将链接结果转换为文档格式
                var documentBuildStart = System.Diagnostics.Stopwatch.StartNew();
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"开始构建文档，链接数量: {linkResults.Count}");
#endif
                
                var documents = linkResults.Select(link =>
                {
                    // 构建文档内容，包含名称、描述和URL
                    var documentBuilder = new StringBuilder();
                    documentBuilder.AppendLine(string.Format(CultureInfo.CurrentCulture, "名称: {0}", link.name));
                    documentBuilder.AppendLine(string.Format(CultureInfo.CurrentCulture, "描述: {0}", link.description));
                    documentBuilder.AppendLine(string.Format(CultureInfo.CurrentCulture, "URL: {0}", link.url));
                    
                    // 添加标签信息
                    if (link.tags != null && link.tags.Length > 0)
                    {
                        var tagNames = link.tags.Where(t => !string.IsNullOrEmpty(t.name)).Select(t => t.name);
                        if (tagNames.Any())
                        {
                            documentBuilder.AppendLine(string.Format(CultureInfo.CurrentCulture, "标签: {0}", string.Join(", ", tagNames)));
                        }
                    }
                    
                    // 添加集合信息
                    if (link.collection != null && !string.IsNullOrEmpty(link.collection.name))
                    {
                        documentBuilder.AppendLine(string.Format(CultureInfo.CurrentCulture, "集合: {0}", link.collection.name));
                    }
                    
                    return documentBuilder.ToString();
                }).ToArray();
                
                documentBuildStart.Stop();
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"文档构建完成，耗时: {documentBuildStart.ElapsedMilliseconds}ms");
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
                var rerankResponse = await CallRerankApiAsync(rerankRequest, apiKey);
                
                if (rerankResponse == null || rerankResponse.Results == null || rerankResponse.Results.Length == 0)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("Rerank API返回空结果，返回原始结果");
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
                System.Diagnostics.Debug.WriteLine($"Rerank完成，重新排序后的链接数量: {rerankedResults.Count}");
                System.Diagnostics.Debug.WriteLine($"Rerank总耗时: {startTime.ElapsedMilliseconds}ms");
#endif
                return rerankedResults;
            }
            catch (Exception)
            {
                // 记录异常并返回原始结果
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Rerank过程中发生异常");
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
        private async Task<RerankResponse?> CallRerankApiAsync(RerankRequest request, string apiKey)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("开始调用rerank API");
#endif
            
            var apiUrl = _settingsManager.RerankApiUrl;
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("Rerank API URL未设置");
#endif
                return null;
            }

            try
            {
                // 序列化请求
                var serializationStart = System.Diagnostics.Stopwatch.StartNew();
                var jsonContent = JsonSerializer.Serialize(request, _jsonSerializerOptions);
                serializationStart.Stop();
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"JSON序列化完成，耗时: {serializationStart.ElapsedMilliseconds}ms，内容长度: {jsonContent.Length}");
#endif
                
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // 设置请求头
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                
                // 发送请求
                var apiCallStart = System.Diagnostics.Stopwatch.StartNew();
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"开始调用rerank API");
#endif
                
                var response = await _httpClient.PostAsync(apiUrl, content);
                
                apiCallStart.Stop();
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"API调用完成，耗时: {apiCallStart.ElapsedMilliseconds}ms");
                
                // 调试日志：记录响应状态码
                System.Diagnostics.Debug.WriteLine($"Rerank API响应状态码: {response.StatusCode}");
#endif
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Rerank API请求失败，状态码: {response.StatusCode}, 响应内容: {errorContent}");
#endif
                    return null;
                }
                
                var responseJson = await response.Content.ReadAsStringAsync();
                
                // 调试提示：显示 API 响应内容（仅在开发环境）
// #if DEBUG
//                 System.Diagnostics.Debug.WriteLine($"Rerank API 响应: {responseJson}");
// #endif
                
                // 反序列化响应
                var deserializationStart = System.Diagnostics.Stopwatch.StartNew();
                var rerankResponse = JsonSerializer.Deserialize<RerankResponse>(responseJson, _jsonSerializerOptions);
                deserializationStart.Stop();
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"JSON反序列化完成，耗时: {deserializationStart.ElapsedMilliseconds}ms");
                
                System.Diagnostics.Debug.WriteLine("Rerank API调用成功");
#endif
                return rerankResponse;
            }
            catch (HttpRequestException)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Rerank API HTTP请求异常");
#endif
                return null;
            }
            catch (TaskCanceledException)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Rerank API任务取消异常（超时）");
#endif
                return null;
            }
            catch (JsonException)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Rerank API JSON序列化异常");
#endif
                return null;
            }
            catch (Exception)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Rerank API未预期的异常");
#endif
                return null;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}