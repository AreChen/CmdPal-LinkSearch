# 排序 API 使用指南

[English](./ranking.md) | 简体中文 | [返回首页](../README.zh.md)

- [排序模式](#排序模式)
- [Rerank API](#rerank-api)
- [决策模型配置](#决策模型-api)
- [自建 System One：Noul](#noul-请求与响应) | [Choice](#choice-请求与响应)
- [OpenRouter Decisions](#openrouter-decisions) | [OpenAI Decisions](#openai-decisions)
- [排查问题](#排查问题)

## 排序模式

在设置中启用重排序并选择服务：

- **Rerank API**：使用兼容 rerank 接口的服务，保留已有 Rerank 设置。
- **OpenRouter** 或 **自建 System One**：选择逐项判断（Noul，推荐用于 Jev）或候选选择（Choice）。
- **OpenAI**：使用 Score 对每个候选评分并排序。

各服务的配置和密钥相互独立。旧版 OpenAI 配置会保留地址、模型和密钥，之前的逐项判断或候选选择会自动改用 Score；切回 OpenRouter 或自建服务时仍保留原来的决策模式。

重排序时只发送搜索词以及候选的标题、描述、标签，不发送独立的链接地址和收藏夹字段；为空或仅包含空白字符的描述和标签会省略。标题、描述或标签本身仍可能包含敏感内容。搜索先展示原始结果，排序完成后再更新；失败时首条结果显示错误并保留原始顺序，不会自动调用其他服务。更换搜索词时会尽量取消旧请求，但服务商可能已经处理或计费。

保存设置之前，可以使用环境变量 `LINKSEARCH_ENABLE_RERANK=true` 启用重排。保存设置后，以设置页面中的重排开关为准，包括用户手动关闭的情况。

**重排候选上限**默认为 50，与显示结果上限分开设置，实际范围为 2 至 200。扩展只在一次请求中重排 Linkwarden 返回的前 N 条，其余结果按原有相对顺序追加。减少候选可能降低延迟和费用，但排在范围之外的相关结果无法提升到已重排部分；Choice 不会合并不同批次的概率，请同时留意服务商的限制。

点击 **保存并测试连接** 会先保存当前设置，再使用所选服务和协议发送两条虚构书签。这是真实请求，可能产生费用；测试期间设置表单暂不可编辑。

以下 `curl` 示例使用 Bash 语法；Windows 用户可在 WSL 或 Git Bash 中运行，不能直接粘贴到 PowerShell。

## Rerank API

### 配置方式

1. 启用重排序并选择 **Rerank**。
2. 填写服务的完整 Rerank 接口地址、API 密钥和模型名称，然后点击 **保存**。默认地址为 `https://api.siliconflow.cn/v1/rerank`，默认模型为 `BAAI/bge-reranker-v2-m3`；如果服务商使用不同值，请替换。
3. 搜索至少返回两条结果的关键词，并在重排序服务后台检查请求记录。

对应设置留空时，LinkSearch 可以读取环境变量 `LINKSEARCH_RERANK_API_URL`、`LINKSEARCH_RERANK_API_KEY` 和 `LINKSEARCH_RERANK_MODEL_NAME`。地址和模型字段有默认值，使用环境变量前需先清空对应字段。

### 请求与响应

以下示例展示 LinkSearch 发送的请求字段。请替换为服务商支持的接口地址和模型，并在环境变量中设置 `LINKSEARCH_RERANK_API_KEY`。

```bash
curl --request POST 'https://rerank.example.com/v1/rerank' \
  --header "Authorization: Bearer $LINKSEARCH_RERANK_API_KEY" \
  --header 'Content-Type: application/json' \
  --data '{
    "model": "BAAI/bge-reranker-v2-m3",
    "query": "python packaging",
    "documents": [
      "Name: Python Packaging User Guide\r\nDescription: Packaging Python projects\r\nTags: python, packaging\r\n",
      "Name: The Rust Book\r\nDescription: Learn Rust\r\nTags: rust\r\n"
    ],
    "top_n": 2,
    "return_documents": false,
    "return_scores": true
  }'
```

响应示例：

```json
{
  "results": [
    { "index": 0, "relevance_score": 0.93 },
    { "index": 1, "relevance_score": 0.17 }
  ]
}
```

`index` 对应请求中 `documents` 数组的位置。LinkSearch 按服务返回的 `results` 顺序排列，并将未列出的文档按原顺序追加；其他响应元数据为可选项。

## 决策模型 API

### 配置方式

1. 启用重排序并选择服务。未启用时不会显示重排序配置。
2. 选择 OpenRouter 或自建 System One 后，再选择 **逐项判断**（Noul）或 **候选选择**（Choice）；OpenAI 自动使用 Score。对应配置字段会立即显示；填写地址、密钥和模型名后保存。OpenAI 和 OpenRouter 配置区提供官方配置教程及 Decisions 文档入口。
   - **自建 System One**：填写完整接口地址、密钥和模型名称。仍支持环境变量 `LINKSEARCH_DECISION_API_URL`、`LINKSEARCH_DECISION_API_KEY` 和 `LINKSEARCH_DECISION_MODEL_NAME`。
   - **OpenRouter**：默认接口为 `https://openrouter.ai/api/alpha/decisions`，默认模型为 `typesafe/jev-1.14`。未填写密钥时读取 `OPENROUTER_API_KEY`。
   - **OpenAI**：默认接口为 `https://api.openai.com/v1/decisions`，默认模型为 `gpt-6-luna`。未填写密钥时读取 `OPENAI_API_KEY`；账号和密钥需要有该模型的访问权限。
3. 点击 **保存并测试连接**，再搜索至少返回两条结果的关键词，在所选服务后台检查是否调用了预期模型。决策服务不使用 Rerank API 配置；服务必须支持所选模型和问题类型。

两个服务都可以填写兼容网关的**完整接口地址**，例如 OpenRouter 使用 `https://gateway.example/api/alpha/decisions`，OpenAI 使用 `https://gateway.example/v1/decisions`；请填写网关密钥，并确认网关支持相应模型和协议。密钥和书签数据会发送给配置的接口，请使用可信的 HTTPS 网关。地址留空时恢复官方默认地址。

OpenRouter / OpenAI 模型名称留空时，分别读取 `LINKSEARCH_OPENROUTER_MODEL_NAME` / `LINKSEARCH_OPENAI_MODEL_NAME`。密钥在设置界面中遮挡，但设置文件本身未加密；也可以使用环境变量。不要把真实密钥提交到仓库。

LinkSearch 使用 `POST` 请求，携带 `Authorization: Bearer <API 密钥>` 和 `Content-Type: application/json`。下面使用虚构书签展示扩展所需的字段。以下 Noul 和 Choice 示例适用于 **自建 System One**；OpenRouter 和 OpenAI 格式另见后文。

### Noul 请求与响应

Noul 在一次请求中为每个候选生成一个问题。`state` 是搜索词，每个问题的 `instructions` 包含候选的标题、描述和标签。

```bash
curl --request POST 'https://decision.example.com/v1/systemone' \
  --header "Authorization: Bearer $LINKSEARCH_DECISION_API_KEY" \
  --header 'Content-Type: application/json' \
  --data '{
    "model": "typesafe/jev-1.14",
    "state": "python packaging",
    "questions": {
      "rel_0": {
        "type": "noul",
        "instructions": "Is this bookmark relevant to the query? Name: Python Packaging User Guide\r\nDescription: Packaging Python projects\r\nTags: python, packaging\r\n"
      },
      "rel_1": {
        "type": "noul",
        "instructions": "Is this bookmark relevant to the query? Name: The Rust Book\r\nDescription: Learn Rust\r\nTags: rust\r\n"
      }
    },
    "stream": false
  }'
```

响应示例：

```json
{
  "answers": {
    "rel_0": { "type": "noul", "noul": 0.93 },
    "rel_1": { "type": "noul", "noul": 0.17 }
  }
}
```

每个问题都必须有对应的回答，`noul` 概率必须是 0 到 1 之间的有限数值。扩展按概率从高到低排序。

### Choice 请求与响应

Choice 将所有候选放入一个问题的 `criteria`。使用相同的接口地址和请求头，发送以下 JSON 请求体：

```json
{
  "model": "typesafe/jev-1.14",
  "state": "python packaging",
  "questions": {
    "rank": {
      "type": "choice",
      "instructions": "Which bookmark is most relevant to the query?",
      "criteria": {
        "doc_0": "Name: Python Packaging User Guide\r\nDescription: Packaging Python projects\r\nTags: python, packaging\r\n",
        "doc_1": "Name: The Rust Book\r\nDescription: Learn Rust\r\nTags: rust\r\n"
      }
    }
  },
  "stream": false
}
```

响应示例：

```json
{
  "answers": {
    "rank": {
      "type": "choice",
      "choice": "doc_0",
      "probabilities": { "doc_0": 0.93, "doc_1": 0.07 }
    }
  }
}
```

`choice` 必须是某个候选的标识；`probabilities` 必须为每个候选提供一个 0 到 1 之间的有限数值，总和与 1 的误差不能超过 0.001。扩展按各候选的概率排序，而不是只使用选中的候选。响应无效或不完整时保留原始顺序；分数相同时保持原有顺序。

### OpenRouter Decisions

[OpenRouter Decisions](https://openrouter.ai/docs/api/api-reference/alphadecisions/submit-a-decisions-request) 使用与上述示例相同的 `state`、对象形式的 `questions`、`noul` 和 `choice`，但 OpenRouter 预设不会发送自建 System One 示例中的 `stream` 字段：

```bash
curl --request POST 'https://openrouter.ai/api/alpha/decisions' \
  --header "Authorization: Bearer $OPENROUTER_API_KEY" \
  --header 'Content-Type: application/json' \
  --data '{
    "model": "typesafe/jev-1.14",
    "state": "python packaging",
    "questions": {
      "rel_0": { "type": "noul", "instructions": "Is this bookmark relevant to the query? Name: Python Packaging User Guide\nDescription: Packaging Python projects\nTags: python, packaging" },
      "rel_1": { "type": "noul", "instructions": "Is this bookmark relevant to the query? Name: The Rust Book\nDescription: Learn Rust\nTags: rust" }
    }
  }'
```

响应中的 `answers` 与自建 System One 示例同为对象。Choice 模式需要每个候选的 `probabilities`；如果服务未返回这些概率，扩展保留原始顺序。

### OpenAI Decisions

[OpenAI Decisions](https://developers.openai.com/api/docs/guides/decisions) 使用 `input`，问题和回答是带名称的数组。扩展为每个候选发送一个 `score` 问题，使用四个由低到高的相关性等级，并在每个问题中重复查询。候选越多或查询越长，额外输入 token 越多：

```bash
curl --request POST 'https://api.openai.com/v1/decisions' \
  --header "Authorization: Bearer $OPENAI_API_KEY" \
  --header 'Content-Type: application/json' \
  --data-binary @- <<'JSON'
  {
    "model": "gpt-6-luna",
    "input": "python packaging",
    "questions": [
      { "type": "score", "name": "rel_0", "instructions": "How well does this bookmark address the user's search query? Search query: python packaging. Name: Python Packaging User Guide\nDescription: Packaging Python projects\nTags: python, packaging\n",
        "levels": [
          { "label": "unrelated", "description": "Does not address the user's requested subject or task." },
          { "label": "weak", "description": "Related to the subject but does not address the requested task." },
          { "label": "partial", "description": "Addresses part of the requested task but misses an important requirement." },
          { "label": "direct", "description": "Directly addresses the specific subject and task in the query." }
        ] },
      { "type": "score", "name": "rel_1", "instructions": "How well does this bookmark address the user's search query? Search query: python packaging. Name: The Rust Book\nDescription: Learn Rust\nTags: rust\n",
        "levels": [
          { "label": "unrelated", "description": "Does not address the user's requested subject or task." },
          { "label": "weak", "description": "Related to the subject but does not address the requested task." },
          { "label": "partial", "description": "Addresses part of the requested task but misses an important requirement." },
          { "label": "direct", "description": "Directly addresses the specific subject and task in the query." }
        ] }
    ]
  }
JSON
```

```json
{ "answers": [
  { "type": "score", "name": "rel_0", "score": 2.85 },
  { "type": "score", "name": "rel_1", "score": 0.12 }
] }
```

扩展通过 `name` 匹配回答，每个候选都需要一个 0 至 3 之间的有限 `score`；分数越高排序越靠前，相同分数保持原顺序。模型拒答、回答缺失或分数无效时保留原始顺序；额外的响应元数据会被忽略。

## 排查问题

- 后台没有请求时，请启用重排序并点击 **保存并测试连接**，检查所选服务、接口地址、密钥和模型名，然后搜索至少返回两条结果的关键词。
- 每个决策服务使用独立配置，决策模式不会使用 Rerank API 的配置。
- 排序失败时，首条结果会显示错误，扩展保留原始顺序。请检查服务日志和返回格式；Choice 模式需要每个候选的概率，Score 需要每个候选的有效分数。
- Rerank 结果按服务返回的顺序显示，请确认服务已对 `results` 数组排序。
