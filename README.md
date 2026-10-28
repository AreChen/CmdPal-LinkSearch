<div align="center">
  <img src="./LinkSearch/Assets/StoreLogo.png" width="96" alt="LinkSearch logo" />
  <h1>LinkSearch</h1>
  <p><strong>Search Linkwarden from Microsoft PowerToys Command Palette.</strong></p>
  <p>Find saved links, open pages quickly, and optionally rank results with a rerank API or decision model.</p>
  <p>English | <a href="./README.zh.md">简体中文</a></p>
  <p>
    <a href="https://apps.microsoft.com/detail/9MZ9Q4CFP2N9"><img alt="Get it from Microsoft Store" src="https://img.shields.io/badge/Get_it_on-Microsoft_Store-0078D4?style=for-the-badge&logo=microsoftstore&logoColor=white" /></a>
    <a href="https://github.com/AreChen/CmdPal-LinkSearch/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/AreChen/CmdPal-LinkSearch?style=for-the-badge&logo=github&color=24292f" /></a>
    <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
    <img alt="License MIT" src="https://img.shields.io/badge/License-MIT-0f766e?style=for-the-badge" />
  </p>
  <p>
    <a href="#install">Install</a> •
    <a href="#features">Features</a> •
    <a href="#usage">Usage</a> •
    <a href="#rerank-api">Rerank API</a> •
    <a href="#decision-model-api">Decision Model API</a> •
    <a href="./PRIVACY.md">Privacy</a>
  </p>
</div>

---

## Preview

<div align="center">
  <img src="https://i.imgur.com/fcsbu0o.gif" alt="LinkSearch search preview in PowerToys Command Palette" width="820" />
  <br />
  <br />
  <img src="https://i.imgur.com/eEM6ZtC.png" alt="LinkSearch settings page" width="820" />
</div>

## Features

<table>
  <tr>
    <td width="25%" valign="top">
      <img src="https://api.iconify.design/lucide:search.svg?color=%230078D4" width="32" alt="Search icon" />
      <h3>Fast Search</h3>
      <p>Search Linkwarden collections directly from PowerToys Command Palette.</p>
    </td>
    <td width="25%" valign="top">
      <img src="https://api.iconify.design/lucide:external-link.svg?color=%230078D4" width="32" alt="Open link icon" />
      <h3>Quick Open</h3>
      <p>Open saved pages from search results without switching context.</p>
    </td>
    <td width="25%" valign="top">
      <img src="https://api.iconify.design/lucide:sparkles.svg?color=%230078D4" width="32" alt="Rerank icon" />
      <h3>Optional Rerank</h3>
      <p>Use a rerank API or decision model for semantic ordering.</p>
    </td>
    <td width="25%" valign="top">
      <img src="https://api.iconify.design/lucide:languages.svg?color=%230078D4" width="32" alt="Language icon" />
      <h3>Bilingual UI</h3>
      <p>Use Auto, Simplified Chinese, or English from extension settings.</p>
    </td>
  </tr>
</table>

## Install

> LinkSearch requires Microsoft PowerToys with Command Palette support.

<a href="https://apps.microsoft.com/detail/9MZ9Q4CFP2N9"><img alt="Get it from Microsoft Store" src="https://img.shields.io/badge/Get_it_on-Microsoft_Store-0078D4?style=for-the-badge&logo=microsoftstore&logoColor=white" /></a>

- Recommended: install LinkSearch from the [Microsoft Store](https://apps.microsoft.com/detail/9MZ9Q4CFP2N9).
- Fallback: download the latest [GitHub Release](https://github.com/AreChen/CmdPal-LinkSearch/releases/latest) MSIX sideload package if Microsoft Store is unavailable.

## Usage

1. Open PowerToys CmdPal (Command Palette).
2. Go to the extension settings page and enable the LinkSearch extension.
3. Configure your preferred shortcut key for the extension.
4. Configure Linkwarden server address, access token, and maximum search result count.
5. Choose the interface language: Auto, Simplified Chinese, or English.
6. Optional: enable reranking, choose a mode, and configure its API URL, key, and model name.
7. Enter the shortcut key and start searching.

## Ranking Modes

Enable reranking in the settings and choose one of these modes:

- **Rerank API**: use a compatible rerank endpoint with your existing rerank settings.
- **Decision model: Noul**: use a System One endpoint to assess each candidate independently.
- **Decision model: Choice**: compare all candidates with a System One endpoint and order them by relative probability.

For decision modes, configure the full System One endpoint (for example, `https://decision.example.com/v1/systemone`), API key, and model name (for example, `Eapil-D1`) separately. The service must support the selected `noul` or `choice` question type. You can also set `LINKSEARCH_DECISION_API_URL`, `LINKSEARCH_DECISION_API_KEY`, and `LINKSEARCH_DECISION_MODEL_NAME` as environment variables; values entered in the extension settings take precedence.

Ranking sends the query and each candidate's title, description, and tags, not the separate URL or collection fields. Titles, descriptions, or tags may themselves contain sensitive information. If the request fails or the decision response is incomplete, the original search order is retained. Choice compares all candidates in one request; probabilities from separate batches are not combined, and a large candidate set may exceed the service's limits.

## Rerank API

Rerank API mode works with services that expose a compatible rerank endpoint.

Request example:

```cURL
curl --location 'https://rerank-api.provider.com/v1/rerank' \
  --header 'Authorization: Bearer your_api_key' \
  --header 'Content-Type: application/json' \
  --data '{
    "model": "bge-reranker-v2-m3",
    "query": "What is Corona disease?",
    "documents": [
      "Corona is a Mexican brand of beer produced by Grupo Modelo in Mexico and exported to markets around the world.",
      "it is a bear",
      "COVID-19 is a contagious illness caused by the a virus SARS-CoV-2."
    ]
  }'
```

Response example:

```json
{
  "results": [
    {
      "index": 2,
      "relevance_score": 0.3174557089805603
    },
    {
      "index": 0,
      "relevance_score": 0.017295653000473976
    },
    {
      "index": 1,
      "relevance_score": 0.000016235228031291626
    }
  ],
  "usage": {
    "prompt_tokens": 50,
    "completion_tokens": 0,
    "total_tokens": 50,
    "prompt_tokens_details": {
      "cached_tokens": 0,
      "text_tokens": 0,
      "audio_tokens": 0,
      "image_tokens": 0
    },
    "completion_tokens_details": {
      "text_tokens": 0,
      "audio_tokens": 0,
      "reasoning_tokens": 0
    },
    "input_tokens": 0,
    "output_tokens": 0,
    "input_tokens_details": null
  }
}
```

## Decision Model API

### Configuration

1. Install LinkSearch 0.1.8 or later, enable reranking, and select **Decision model: per item (Noul)** or **Decision model: candidate choice (Choice)**.
2. Enter the full decision endpoint, its API key, and its model name in the three decision-model settings. For example: `https://decision.example.com/v1/systemone`, your service's key, and `Eapil-D1`. The hostname, endpoint path, and model name depend on your provider.
3. Save the settings and search for a term that returns at least two results. Check the decision service's request logs to confirm that the expected model was called. The separate Rerank API fields are not used in decision modes.

LinkSearch sends a `POST` with `Authorization: Bearer <API key>` and `Content-Type: application/json`. The examples below use fictional bookmarks and show the fields required by LinkSearch. The service may return metadata at the root or inside answer objects, but `answers` must contain exactly the requested question IDs. Replace the URL and model with values from your provider. The curl example reads the key from `LINKSEARCH_DECISION_API_KEY`; do not commit real API keys.

### Noul Request and Response

Noul sends one question per candidate in a single request. `state` is the search query; each question's `instructions` contains the candidate's name, description, and tags.

```bash
curl --request POST 'https://decision.example.com/v1/systemone' \
  --header "Authorization: Bearer $LINKSEARCH_DECISION_API_KEY" \
  --header 'Content-Type: application/json' \
  --data '{
    "model": "Eapil-D1",
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
  "model": "Eapil-D1",
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

## Roadmap

- [x] Microsoft Store publishing.

## Credits

- Built on the PowerToys CmdPal extension framework (Microsoft Command Palette Extensions Toolkit).
- Semantic reranking capability aligns with modern retrieval engineering practices, thanks to community resources and examples.

## Contributing

Issues and pull requests are welcome.

## Privacy

[Privacy Policy](./PRIVACY.md)

## License

MIT
