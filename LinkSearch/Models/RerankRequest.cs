// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

namespace LinkSearch.Models
{
    /// <summary>
    /// 表示rerank API请求
    /// </summary>
    internal class RerankRequest
    {
        /// <summary>
        /// 查询文本
        /// </summary>
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// 需要重新排序的文档列表
        /// </summary>
        [JsonPropertyName("documents")]
        public string[] Documents { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 模型名称
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// 返回结果数量
        /// </summary>
        [JsonPropertyName("top_n")]
        public int? TopN { get; set; }

        /// <summary>
        /// 是否返回每个文档的分数
        /// </summary>
        [JsonPropertyName("return_documents")]
        public bool? ReturnDocuments { get; set; } = true;

        /// <summary>
        /// 是否返回每个文档的分数
        /// </summary>
        [JsonPropertyName("return_scores")]
        public bool? ReturnScores { get; set; } = true;

        /// <summary>
        /// 构造函数
        /// </summary>
        public RerankRequest()
        {
// #if DEBUG
//             // 调试日志：验证构造函数被调用
//             System.Diagnostics.Debug.WriteLine("RerankRequest 构造函数被调用");
// #endif
        }

        /// <summary>
        /// 创建rerank请求
        /// </summary>
        /// <param name="query">查询文本</param>
        /// <param name="documents">文档列表</param>
        /// <param name="model">模型名称</param>
        /// <param name="topN">返回结果数量</param>
        /// <param name="returnDocuments">是否返回文档</param>
        /// <param name="returnScores">是否返回分数</param>
        /// <returns>rerank请求实例</returns>
        public static RerankRequest Create(
            string query,
            string[] documents,
            string model,
            int? topN = null,
            bool? returnDocuments = true,
            bool? returnScores = true)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("创建RerankRequest实例");
#endif
            
            return new RerankRequest
            {
                Query = query,
                Documents = documents,
                Model = model,
                TopN = topN,
                ReturnDocuments = returnDocuments,
                ReturnScores = returnScores
            };
        }
    }
}