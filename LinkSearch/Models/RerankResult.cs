// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace LinkSearch.Models
{
    /// <summary>
    /// 表示单个rerank结果
    /// </summary>
    internal class RerankResult
    {
        /// <summary>
        /// 文档索引
        /// </summary>
        [JsonPropertyName("index")]
        public int Index { get; set; }

        /// <summary>
        /// 相关性分数
        /// </summary>
        [JsonPropertyName("relevance_score")]
        public double RelevanceScore { get; set; }

        /// <summary>
        /// 文档内容
        /// </summary>
        [JsonPropertyName("document")]
        public string? Document { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public RerankResult()
        {
// #if DEBUG
//             // 调试日志：验证构造函数被调用
//             System.Diagnostics.Debug.WriteLine("RerankResult 构造函数被调用");
// #endif
        }

        /// <summary>
        /// 创建rerank结果
        /// </summary>
        /// <param name="index">文档索引</param>
        /// <param name="relevanceScore">相关性分数</param>
        /// <param name="document">文档内容</param>
        /// <returns>rerank结果实例</returns>
        public static RerankResult Create(int index, double relevanceScore, string? document = null)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"创建RerankResult实例，索引: {index}, 分数: {relevanceScore}");
#endif
            
            return new RerankResult
            {
                Index = index,
                RelevanceScore = relevanceScore,
                Document = document
            };
        }
    }
}