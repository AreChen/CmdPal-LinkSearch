// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using LinkSearch.Helpers;
using LinkSearch.Models;

namespace LinkSearch.Services
{
    /// <summary>
    /// Rerank连接测试服务类，实现测试连接功能
    /// </summary>
    internal partial class RerankConnectionTestService : IDisposable
    {
        private static readonly string[] TestDocuments = new[] { "This is a test document for connection testing." };
        
        private readonly SettingsManager _settingsManager;
        private readonly HttpClient _httpClient;
        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
 
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="settingsManager">设置管理器</param>
        public RerankConnectionTestService(SettingsManager settingsManager)
        {
 // #if DEBUG
 //             // 调试日志：验证构造函数被调用
 //             Log.Debug("RerankConnectionTestService 构造函数被调用");
 // #endif
             
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            // 使用共享 HttpClient，避免频繁创建导致的端口耗尽问题
            _httpClient = HttpClientProvider.Shared;
            // 使用静态 JsonSerializerOptions，避免每次构造
        }

        /// <summary>
        /// 测试rerank API连接
        /// </summary>
        /// <returns>连接测试结果</returns>
        [RequiresUnreferencedCode("JSON serialization and deserialization may require types that cannot be statically analyzed.")]
        [RequiresDynamicCode("JSON serialization and deserialization may require code that cannot be statically generated.")]
        public async Task<RerankConnectionTestResult> TestConnectionAsync(System.Threading.CancellationToken cancellationToken = default)
        {
#if DEBUG
            Log.Debug("开始测试rerank API连接");
#endif
            
            var startTime = DateTimeOffset.UtcNow;
            
            try
            {
                // 检查API URL是否设置
                var apiUrl = _settingsManager.RerankApiUrl;
                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    return RerankConnectionTestResult.CreateFailure(
                        "ConfigurationError",
                        "Rerank API URL未设置",
                        GetElapsedTimeMs(startTime)
                    );
                }

                // 检查API Key是否设置
                var apiKey = _settingsManager.RerankApiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return RerankConnectionTestResult.CreateFailure(
                        "ConfigurationError",
                        "Rerank API Key未设置",
                        GetElapsedTimeMs(startTime)
                    );
                }

                // 检查模型名称是否设置
                var modelName = _settingsManager.RerankModelName;
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    return RerankConnectionTestResult.CreateFailure(
                        "ConfigurationError",
                        "Rerank模型名称未设置",
                        GetElapsedTimeMs(startTime)
                    );
                }

                // 创建测试请求
                var testRequest = RerankRequest.Create(
                    query: "test",
                    documents: TestDocuments,
                    model: modelName,
                    topN: 1,
                    returnDocuments: false,
                    returnScores: true
                );

                // 序列化请求为 UTF-8 字节，避免中间 string 带来的大对象分配
                var bytes = JsonSerializer.SerializeToUtf8Bytes(testRequest, s_jsonSerializerOptions);
                using var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
 
                // 为每次请求创建 HttpRequestMessage，避免修改共享 HttpClient.DefaultRequestHeaders 导致并发竞态
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = content
                };
                requestMessage.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
 
                // 发送请求（流式读取响应头，减少内存占用）
                using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                var responseTimeMs = GetElapsedTimeMs(startTime);
 
                // 调试日志：记录响应状态码
    #if DEBUG
                Log.Debug($"Rerank API测试响应状态码: {response.StatusCode}");
    #endif
                
                if (!response.IsSuccessStatusCode)
                {
                    // 非成功分支读取响应内容以便返回详细信息
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    
                    // 根据状态码确定错误类型
                    string errorType;
                    string errorMessage;
                    
                    switch ((int)response.StatusCode)
                    {
                        case 401:
                            errorType = "AuthenticationError";
                            errorMessage = "API Key无效或已过期";
                            break;
                        case 403:
                            errorType = "AuthorizationError";
                            errorMessage = "没有访问权限";
                            break;
                        case 404:
                            errorType = "EndpointError";
                            errorMessage = "API端点不存在";
                            break;
                        case 429:
                            errorType = "RateLimitError";
                            errorMessage = "请求频率超过限制";
                            break;
                        case 500:
                        case 502:
                        case 503:
                        case 504:
                            errorType = "ServerError";
                            errorMessage = "服务器内部错误";
                            break;
                        default:
                            errorType = "HttpError";
                            errorMessage = $"HTTP请求失败: {response.StatusCode}";
                            break;
                    }
                    
                    return RerankConnectionTestResult.CreateFailure(
                        errorType,
                        errorMessage,
                        responseTimeMs,
                        (int)response.StatusCode,
                        errorContent
                    );
                }
                
                // 尝试解析响应（使用流式反序列化，支持取消）
                try
                {
                    await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    var rerankResponse = await JsonSerializer.DeserializeAsync<RerankResponse>(responseStream, s_jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                    
                    if (rerankResponse == null)
                    {
                        return RerankConnectionTestResult.CreateFailure(
                            "ResponseError",
                            "API响应为空",
                            responseTimeMs,
                            (int)response.StatusCode,
                            string.Empty
                        );
                    }
                    
    #if DEBUG
                    Log.Debug("Rerank API连接测试成功");
    #endif
                    return RerankConnectionTestResult.CreateSuccess(
                        responseTimeMs,
                        (int)response.StatusCode,
                        string.Empty
                    );
                }
                catch (JsonException jsonEx)
                {
    #if DEBUG
                    Log.Debug($"Rerank API响应JSON解析异常: {jsonEx.Message}");
    #endif
                    
                    return RerankConnectionTestResult.CreateFailure(
                        "ResponseError",
                        $"API响应格式错误: {jsonEx.Message}",
                        responseTimeMs,
                        (int)response.StatusCode,
                        string.Empty
                    );
                }
            }
            catch (HttpRequestException httpEx)
            {
#if DEBUG
                Log.Debug($"Rerank API HTTP请求异常: {httpEx.Message}");
                Log.Debug($"异常类型: {httpEx.GetType().Name}");
                Log.Debug($"内部异常: {httpEx.InnerException?.Message}");
#endif
                
                // 根据异常类型确定错误类型
                string errorType;
                string errorMessage;
                
                if (httpEx.Message.Contains("SSL") || httpEx.Message.Contains("security") || httpEx.Message.Contains("certificate"))
                {
                    errorType = "SslError";
                    errorMessage = "SSL连接失败：服务器证书无效或不受信任";
                }
                else if (httpEx.Message.Contains("timeout") || httpEx.Message.Contains("timed out"))
                {
                    errorType = "TimeoutError";
                    errorMessage = "连接超时：服务器响应时间过长";
                }
                else if (httpEx.Message.Contains("resolve") || httpEx.Message.Contains("DNS"))
                {
                    errorType = "DnsError";
                    errorMessage = "DNS解析失败：无法解析服务器地址";
                }
                else if (httpEx.Message.Contains("connect") || httpEx.Message.Contains("connection"))
                {
                    errorType = "ConnectionError";
                    errorMessage = "连接失败：无法连接到服务器，请检查网络连接和服务器地址";
                }
                else
                {
                    errorType = "NetworkError";
                    errorMessage = $"网络请求失败: {httpEx.Message}";
                }
                
                return RerankConnectionTestResult.CreateFailure(
                    errorType,
                    errorMessage,
                    GetElapsedTimeMs(startTime)
                );
            }
            catch (TaskCanceledException)
            {
#if DEBUG
                Log.Debug($"Rerank API任务取消异常（超时）");
#endif
                
                return RerankConnectionTestResult.CreateFailure(
                    "TimeoutError",
                    "请求超时：服务器响应时间过长",
                    GetElapsedTimeMs(startTime)
                );
            }
            catch (UriFormatException uriEx)
            {
#if DEBUG
                Log.Debug($"Rerank API URL格式异常: {uriEx.Message}");
#endif
                
                return RerankConnectionTestResult.CreateFailure(
                    "ConfigurationError",
                    $"URL格式错误: {uriEx.Message}",
                    GetElapsedTimeMs(startTime)
                );
            }
            catch (Exception ex)
            {
#if DEBUG
                Log.Debug($"Rerank API未预期的异常: {ex.Message}");
                Log.Debug($"异常类型: {ex.GetType().Name}");
                Log.Debug($"内部异常: {ex.InnerException?.Message}");
                Log.Debug($"堆栈跟踪: {ex.StackTrace}");
#endif
                
                return RerankConnectionTestResult.CreateFailure(
                    "UnknownError",
                    $"未预期的错误: {ex.Message}",
                    GetElapsedTimeMs(startTime)
                );
            }
        }

        /// <summary>
        /// 计算经过的时间（毫秒）
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <returns>经过的毫秒数</returns>
        private static long GetElapsedTimeMs(DateTimeOffset startTime)
        {
            return (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 使用共享 HttpClient，不在此处 Dispose
            // 保留方法以便将来清理其他资源
        }
    }
}