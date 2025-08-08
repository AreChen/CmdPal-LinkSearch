# LinkSearch

English | [简体中文](./README.zh.md)

![Setting](https://i.imgur.com/fcsbu0o.gif)

[PowerToys CmdPal](https://github.com/microsoft/PowerToys) plugin to search and open your [Linkwarden](https://github.com/linkwarden/linkwarden) links quickly inside the command palette, with optional Rerank integration for better, semantic ordering.

- Efficient search: quickly search from Linkwarden collections and navigate to corresponding pages.
- Optional reranking: connect a configurable Rerank API to semantically reorder candidates.

![Setting](https://i.imgur.com/eEM6ZtC.png)


## Usage
- Since I don't have a developer certificate, you need to compile and deploy the program yourself
- Open PowerToys' CmdPal (Command Palette)
- Go to the extension settings page and enable the LinkSearch extension
- Configure your preferred shortcut key for the extension
- Configure Linkwarden server address, AccessTokens, and maximum search return results
- (Optional) Enable and configure Rerank service parameters (such as API URL, Key, model name, etc.)
- Enter the shortcut key to start searching!


Rerank service API format:
- Theoretically compatible with any service that supports the following rerank API interface
- API call format
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
- Response structure
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

## Roadmap

- [ ] Microsoft Store publishing

Issues/PRs are welcome!

## Credits

- Built on the PowerToys CmdPal extension framework (Microsoft Command Palette Extensions Toolkit).
- Semantic reranking capability aligns with modern retrieval engineering practices, thanks to community resources and examples.

## Thanks

- Thanks to contributors for testing and documentation suggestions.

## License

MIT
