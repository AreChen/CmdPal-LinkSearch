// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace LinkSearch.Models
{
    /// <summary>
    /// 表示rerank连接测试结果
    /// </summary>
    internal class RerankConnectionTestResult
    {
        /// <summary>
        /// 测试是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 错误类型
        /// </summary>
        public string ErrorType { get; set; } = string.Empty;

        /// <summary>
        /// 响应时间（毫秒）
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// HTTP状态码
        /// </summary>
        public int? HttpStatusCode { get; set; }

        /// <summary>
        /// API响应内容
        /// </summary>
        public string? ResponseContent { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public RerankConnectionTestResult()
        {
// #if DEBUG
//             // 调试日志：验证构造函数被调用
//             System.Diagnostics.Debug.WriteLine("RerankConnectionTestResult 构造函数被调用");
// #endif
        }

        /// <summary>
        /// 创建成功的测试结果
        /// </summary>
        /// <param name="responseTimeMs">响应时间（毫秒）</param>
        /// <param name="httpStatusCode">HTTP状态码</param>
        /// <param name="responseContent">响应内容</param>
        /// <returns>成功的测试结果</returns>
        public static RerankConnectionTestResult CreateSuccess(
            long responseTimeMs,
            int? httpStatusCode = null,
            string? responseContent = null)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"创建成功的RerankConnectionTestResult，响应时间: {responseTimeMs}ms");
#endif
            
            return new RerankConnectionTestResult
            {
                IsSuccess = true,
                ResponseTimeMs = responseTimeMs,
                HttpStatusCode = httpStatusCode,
                ResponseContent = responseContent
            };
        }

        /// <summary>
        /// 创建失败的测试结果
        /// </summary>
        /// <param name="errorType">错误类型</param>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="responseTimeMs">响应时间（毫秒）</param>
        /// <param name="httpStatusCode">HTTP状态码</param>
        /// <param name="responseContent">响应内容</param>
        /// <returns>失败的测试结果</returns>
        public static RerankConnectionTestResult CreateFailure(
            string errorType,
            string errorMessage,
            long responseTimeMs = 0,
            int? httpStatusCode = null,
            string? responseContent = null)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"创建失败的RerankConnectionTestResult，错误类型: {errorType}, 错误消息: {errorMessage}");
#endif
            
            return new RerankConnectionTestResult
            {
                IsSuccess = false,
                ErrorType = errorType,
                ErrorMessage = errorMessage,
                ResponseTimeMs = responseTimeMs,
                HttpStatusCode = httpStatusCode,
                ResponseContent = responseContent
            };
        }

        /// <summary>
        /// 获取测试结果的摘要信息
        /// </summary>
        /// <returns>摘要信息</returns>
        public string GetSummary()
        {
            if (IsSuccess)
            {
                return $"连接成功，响应时间: {ResponseTimeMs}ms";
            }
            else
            {
                return $"连接失败: {ErrorType} - {ErrorMessage}";
            }
        }
    }
}