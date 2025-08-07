// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

namespace LinkSearch.Models
{
    /// <summary>
    /// 表示rerank API响应
    /// </summary>
    internal class RerankResponse
    {
        /// <summary>
        /// 模型ID
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 对象类型
        /// </summary>
        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonPropertyName("created")]
        public long Created { get; set; }

        /// <summary>
        /// 模型名称
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// 结果列表
        /// </summary>
        [JsonPropertyName("results")]
        public RerankResult[] Results { get; set; } = Array.Empty<RerankResult>();

        /// <summary>
        /// 使用情况
        /// </summary>
        [JsonPropertyName("usage")]
        public UsageInfo? Usage { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public RerankResponse()
        {
// #if DEBUG
//             // 调试日志：验证构造函数被调用
//             System.Diagnostics.Debug.WriteLine("RerankResponse 构造函数被调用");
// #endif
        }

        /// <summary>
        /// 使用情况类
        /// </summary>
        internal class UsageInfo
        {
            /// <summary>
            /// 处理的token数量
            /// </summary>
            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }

            /// <summary>
            /// 构造函数
            /// </summary>
            public UsageInfo()
            {
// #if DEBUG
//                 // 调试日志：验证构造函数被调用
//                 System.Diagnostics.Debug.WriteLine("UsageInfo 构造函数被调用");
// #endif
            }
        }
    }
}