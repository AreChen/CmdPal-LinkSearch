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
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="settingsManager">设置管理器</param>
        public RerankConnectionTestService(SettingsManager settingsManager)
        {
// #if DEBUG
//             // 调试日志：验证构造函数被调用
//             System.Diagnostics.Debug.WriteLine("RerankConnectionTestService 构造函数被调用");
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
        /// 测试rerank API连接
        /// </summary>
        /// <returns>连接测试结果</returns>
        [RequiresUnreferencedCode("JSON serialization and deserialization may require types that cannot be statically analyzed.")]
        [RequiresDynamicCode("JSON serialization and deserialization may require code that cannot be statically generated.")]
        public async Task<RerankConnectionTestResult> TestConnectionAsync()
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("开始测试rerank API连接");
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

                // 序列化请求
                var jsonContent = JsonSerializer.Serialize(testRequest, _jsonSerializerOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // 设置请求头
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                
                // 发送请求
                var response = await _httpClient.PostAsync(apiUrl, content);
                var responseTimeMs = GetElapsedTimeMs(startTime);
                
                // 调试日志：记录响应状态码
    #if DEBUG
                System.Diagnostics.Debug.WriteLine($"Rerank API测试响应状态码: {response.StatusCode}");
    #endif
                
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // 调试提示：显示 API 响应内容（仅在开发环境）
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Rerank API测试响应: {responseContent}");
#endif
                
                if (!response.IsSuccessStatusCode)
                {
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
                        responseContent
                    );
                }
                
                // 尝试解析响应
                try
                {
                    var rerankResponse = JsonSerializer.Deserialize<RerankResponse>(responseContent, _jsonSerializerOptions);
                    
                    if (rerankResponse == null)
                    {
                        return RerankConnectionTestResult.CreateFailure(
                            "ResponseError",
                            "API响应为空",
                            responseTimeMs,
                            (int)response.StatusCode,
                            responseContent
                        );
                    }
                    
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("Rerank API连接测试成功");
#endif
                    return RerankConnectionTestResult.CreateSuccess(
                        responseTimeMs,
                        (int)response.StatusCode,
                        responseContent
                    );
                }
                catch (JsonException jsonEx)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Rerank API响应JSON解析异常: {jsonEx.Message}");
#endif
                    
                    return RerankConnectionTestResult.CreateFailure(
                        "ResponseError",
                        $"API响应格式错误: {jsonEx.Message}",
                        responseTimeMs,
                        (int)response.StatusCode,
                        responseContent
                    );
                }
            }
            catch (HttpRequestException httpEx)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"Rerank API HTTP请求异常: {httpEx.Message}");
                System.Diagnostics.Debug.WriteLine($"异常类型: {httpEx.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"内部异常: {httpEx.InnerException?.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Rerank API任务取消异常（超时）");
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
                System.Diagnostics.Debug.WriteLine($"Rerank API URL格式异常: {uriEx.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Rerank API未预期的异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常类型: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"内部异常: {ex.InnerException?.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
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
            _httpClient?.Dispose();
        }
    }
}