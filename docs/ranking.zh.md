# 排序 API 使用指南

[English](./ranking.md) | 简体中文 | [返回首页](../README.zh.md)

- [排序模式](#排序模式)
- [Rerank API](#rerank-api)
- [决策模型配置](#决策模型-api)
- [自建 System One：Noul](#noul-请求与响应) | [Choice](#choice-请求与响应)
- [OpenRouter Decisions](#openrouter-decisions) | [OpenAI Decisions](#openai-decisions)
- [排查问题](#排查问题)

## 排序模式

在设置中启用重排序并选择以下模式：

- **Rerank API**：使用兼容 rerank 接口的服务，保留已有 Rerank 设置。
- **决策模型：逐项判断 (Noul / Predicate)**：分别判断每个候选的相关性。
- **决策模型：候选选择 (Choice)**：比较所有候选，按返回的相对概率排序。

决策模式支持自建 System One、OpenRouter 和 OpenAI 官方 Decisions API。各服务的配置与密钥相互独立，原有 Rerank 设置保持不变。0.1.8 版本不包含 OpenAI 和 OpenRouter 预设。

重排序时只发送搜索词以及候选的标题、描述、标签，不发送独立的链接地址和收藏夹字段；标题、描述或标签本身仍可能包含敏感内容。请求失败或决策响应不完整时，保留原始搜索顺序。Choice 一次比较全部候选，不会合并不同批次的概率；候选过多可能超过服务限制。

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

1. 启用重排序，选择 **逐项判断**（Noul / Predicate）或 **候选选择**（Choice）。未启用时不会显示重排序配置。
2. 选择 **决策模型服务**，对应的配置字段会立即显示；填写地址、密钥和模型名后保存。OpenAI 和 OpenRouter 配置区提供官方配置教程及 Decisions 文档入口。
   - **自建 System One**：填写完整接口地址、密钥和模型名称。仍支持环境变量 `LINKSEARCH_DECISION_API_URL`、`LINKSEARCH_DECISION_API_KEY` 和 `LINKSEARCH_DECISION_MODEL_NAME`。
   - **OpenRouter**：默认接口为 `https://openrouter.ai/api/alpha/decisions`，默认模型为 `typesafe/jev-1.14`。未填写密钥时读取 `OPENROUTER_API_KEY`。
   - **OpenAI**：默认接口为 `https://api.openai.com/v1/decisions`，默认模型为 `gpt-6-luna`。未填写密钥时读取 `OPENAI_API_KEY`；账号和密钥需要有该模型的访问权限。
3. 搜索至少返回两条结果的关键词，在选中服务的后台检查是否调用了预期模型。决策模式不使用 Rerank API 配置；服务必须支持所选模型和问题类型。

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

[OpenAI Decisions](https://developers.openai.com/api/docs/guides/decisions) 使用 `input`，问题和回答是带名称的数组。逐项判断使用 `predicate`：

```bash
curl --request POST 'https://api.openai.com/v1/decisions' \
  --header "Authorization: Bearer $OPENAI_API_KEY" \
  --header 'Content-Type: application/json' \
  --data '{
    "model": "gpt-6-luna",
    "input": "python packaging",
    "questions": [
      { "type": "predicate", "name": "rel_0", "instructions": "Is this bookmark relevant to the query? Name: Python Packaging User Guide\nDescription: Packaging Python projects\nTags: python, packaging" },
      { "type": "predicate", "name": "rel_1", "instructions": "Is this bookmark relevant to the query? Name: The Rust Book\nDescription: Learn Rust\nTags: rust" }
    ]
  }'
```

```json
{ "answers": [
  { "type": "predicate", "name": "rel_0", "probability": 0.93 },
  { "type": "predicate", "name": "rel_1", "probability": 0.17 }
] }
```

Choice 模式使用相同的 `model` 和 `input`，将 `questions` 替换为：

```json
[{ "type": "choice", "name": "rank", "instructions": "Which bookmark is most relevant to the query?",
   "choices": [
     { "value": "doc_0", "description": "Name: Python Packaging User Guide\nDescription: Packaging Python projects\nTags: python, packaging" },
     { "value": "doc_1", "description": "Name: The Rust Book\nDescription: Learn Rust\nTags: rust" }
   ] }]
```

```json
{ "answers": [{ "type": "choice", "name": "rank", "choice": "doc_0", "confidence": 0.93,
  "probabilities": [{ "value": "doc_0", "probability": 0.93 }, { "value": "doc_1", "probability": 0.07 }] }] }
```

扩展通过 `name` 匹配回答、通过 `value` 匹配候选概率。模型拒答、回答或概率缺失、数值无效时保留原始顺序。OpenAI Choice 接受 2 至 255 个候选，LinkSearch 的搜索结果上限为 200 条。

## 排查问题

- 后台没有请求时，请确认已启用重排序并保存，然后搜索至少返回两条结果的关键词；检查当前选择的模式、接口地址、密钥和模型名。
- 每个决策服务使用独立配置，决策模式不会使用 Rerank API 的配置。
- 请求失败，或决策响应不完整、格式无效时，扩展会保留原始顺序。请检查服务日志和返回格式；Choice 模式需要每个候选的概率。
- Rerank 结果按服务返回的顺序显示，请确认服务已对 `results` 数组排序。
