# Ranking API Guide

English | [简体中文](./ranking.zh.md) | [Back to README](../README.md)

- [Ranking modes](#ranking-modes)
- [Rerank API](#rerank-api)
- [Decision model configuration](#decision-model-api)
- [Custom System One: Noul](#noul-request-and-response) | [Choice](#choice-request-and-response)
- [OpenRouter Decisions](#openrouter-decisions) | [OpenAI Decisions](#openai-decisions)
- [Troubleshooting](#troubleshooting)

## Ranking Modes

Enable reranking in the settings and choose one of these modes:

- **Rerank API**: use a compatible rerank endpoint with your existing rerank settings.
- **Decision model: per item (Noul / Predicate)**: assess each candidate independently.
- **Decision model: Choice**: compare all candidates and order them by relative probability.

Decision modes support custom System One, OpenRouter, and the official OpenAI Decisions API. Each provider has separate settings and credentials; the Rerank API settings remain unchanged. OpenAI and OpenRouter presets are not available in version 0.1.8.

Ranking sends the query and each candidate's title, description, and tags, not the separate URL or collection fields. Titles, descriptions, or tags may themselves contain sensitive information. If the request fails or the decision response is incomplete, the original search order is retained. Choice compares all candidates in one request; probabilities from separate batches are not combined, and a large candidate set may exceed the service's limits.

The `curl` examples below use Bash syntax; on Windows, run them in WSL or Git Bash rather than PowerShell.

## Rerank API

### Configuration

1. Enable ranking and select **Rerank**.
2. Enter the service's full rerank endpoint, API key, and model name, then select **Save**. The defaults are `https://api.siliconflow.cn/v1/rerank` and `BAAI/bge-reranker-v2-m3`; replace them if your provider uses different values.
3. Search for a term that returns at least two results and check the rerank provider's request logs.

If the corresponding setting is empty, LinkSearch can read `LINKSEARCH_RERANK_API_URL`, `LINKSEARCH_RERANK_API_KEY`, and `LINKSEARCH_RERANK_MODEL_NAME` from the environment. URL and model fields have defaults, so clear them first to use the environment values.

### Request and Response

This example shows the request fields sent by LinkSearch. Replace the example endpoint and model with values supported by your provider, and set `LINKSEARCH_RERANK_API_KEY` in your environment.

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

Example response:

```json
{
  "results": [
    { "index": 0, "relevance_score": 0.93 },
    { "index": 1, "relevance_score": 0.17 }
  ]
}
```

Each `index` refers to a position in the original `documents` array. LinkSearch follows the order of `results` returned by the service and appends any omitted documents in their original order. Additional response metadata is optional.

## Decision Model API

### Configuration

1. Enable ranking and choose **Per item** (Noul / Predicate) or **Choice**. Ranking settings stay hidden while ranking is disabled.
2. Choose the **Decision provider**. Its fields appear immediately; enter the endpoint, API key and model, then save. OpenAI and OpenRouter also provide links to their official setup and Decisions guides.
   - **Custom System One**: enter the full endpoint, key and model. The existing `LINKSEARCH_DECISION_API_URL`, `LINKSEARCH_DECISION_API_KEY` and `LINKSEARCH_DECISION_MODEL_NAME` environment variables are still supported.
   - **OpenRouter**: the default endpoint is `https://openrouter.ai/api/alpha/decisions`, and the default model is `typesafe/jev-1.14`. An empty key falls back to `OPENROUTER_API_KEY`.
   - **OpenAI**: the default endpoint is `https://api.openai.com/v1/decisions`, and the default model is `gpt-6-luna`. An empty key falls back to `OPENAI_API_KEY`. The API key and account must have access to the model.
3. Search for a term that returns at least two results and check the selected service's request logs. The Rerank API fields are not used in decision modes. The provider must support the selected question type and model.

Both providers accept a custom **full endpoint URL** for compatible gateways, such as `https://gateway.example/api/alpha/decisions` for OpenRouter or `https://gateway.example/v1/decisions` for OpenAI; use the gateway's key and confirm that its model and protocol are supported. The key and bookmark data are sent to the configured endpoint, so use a trusted HTTPS gateway. Leaving the endpoint blank restores the official default.

An empty OpenRouter/OpenAI model field falls back to `LINKSEARCH_OPENROUTER_MODEL_NAME` / `LINKSEARCH_OPENAI_MODEL_NAME` respectively. Keys are masked in the settings form, but the settings file itself is not encrypted; environment variables can be used instead. Do not commit real keys.

LinkSearch sends a `POST` with `Authorization: Bearer <API key>` and `Content-Type: application/json`. The examples use fictional bookmarks and show the fields required by LinkSearch. The following Noul and Choice examples describe **custom System One**; the OpenRouter and OpenAI formats are described separately below.

### Noul Request and Response

Noul sends one question per candidate in a single request. `state` is the search query; each question's `instructions` contains the candidate's name, description, and tags.

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

Example response:

```json
{
  "answers": {
    "rel_0": { "type": "noul", "noul": 0.93 },
    "rel_1": { "type": "noul", "noul": 0.17 }
  }
}
```

Each question must have a matching answer with a finite `noul` probability between 0 and 1. Results are sorted by that probability, highest first.

### Choice Request and Response

Choice sends one question with all candidates as `criteria`. Send this JSON body to the same configured endpoint with the same headers:

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

Example response:

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

The selected `choice` must name a candidate, and `probabilities` must contain one finite value between 0 and 1 for every candidate, summing to 1 within a tolerance of 0.001. LinkSearch sorts by the probabilities, not just the selected choice. Invalid or incomplete responses leave the original order unchanged; equal scores preserve the original order.

### OpenRouter Decisions

[OpenRouter Decisions](https://openrouter.ai/docs/api/api-reference/alphadecisions/submit-a-decisions-request) uses the same `state`, object-shaped `questions`, `noul` and `choice` formats shown above, but the OpenRouter preset does not send the custom System One `stream` field:

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

The answer has the same object-shaped `answers` as the custom System One examples. Choice mode requires the per-candidate `probabilities`; if the provider omits them, LinkSearch retains the original order.

### OpenAI Decisions

[OpenAI Decisions](https://developers.openai.com/api/docs/guides/decisions) uses `input` and arrays of named questions and answers. Per-item mode sends `predicate` questions:

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

For Choice mode, send the same `model` and `input`, with the following `questions` array:

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

Answers are matched by `name` and choice probabilities by `value`. A refusal, missing answer, incomplete probabilities, or invalid values leaves the original order unchanged. OpenAI Choice accepts 2 to 255 candidates; LinkSearch limits search results to 200.

## Troubleshooting

- If no request appears, enable ranking, save the settings, and search for a term with at least two results. Verify the selected mode, endpoint, key, and model.
- Each decision provider has its own configuration. Decision modes do not use the Rerank API fields.
- If a request fails or a decision response is incomplete or invalid, LinkSearch keeps the original order. Check the service's response format and logs; Choice requires a probability for every candidate.
- Rerank results are displayed in the order returned by the service. Confirm the service has sorted its `results` array.
