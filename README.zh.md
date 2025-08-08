# LinkSearch

[English](./README.md) | 简体中文

![Setting](https://i.imgur.com/fcsbu0o.gif)

[PowerToys CmdPal](https://github.com/microsoft/PowerToys) 插件：在命令面板中快速搜索与打开你的[Linkwarden](https://github.com/linkwarden/linkwarden)资源，并可选接入 Rerank 服务以获得更相关的排序结果。




- 高效检索：快速搜索来自 Linkwarden 的收藏并跳转至相应页面。
- 可选重排：连接可配置的 Rerank API，按语义相关性对候选列表重排。

![Setting](https://i.imgur.com/eEM6ZtC.png)



## 使用
- 由于我没有开发者证书,因此你需要进行自行编译部署程序
- 打开 PowerToys 的 CmdPal（命令面板）
- 进入扩展设置页面并打开 LinkSearch 扩展
- 为扩展配置你喜欢的快捷键
- 配置 Linkwarden 服务器地址与 AccessTokens 访问令牌以及最大检索返回的结果数量
- (可选)启用并配置 Rerank 服务相关参数（如 API URL、Key、模型名等）
- 输入快捷键 开始进行检索!


Rerank 服务调用接口格式：
- 理论上兼容以下rerank接口的服务都可以使用
- 调用接口格式
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
- 返回结构
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

## 路线图

- [ ] Microsoft Store 上架

欢迎 Issue / PR！

## Credits

- 基于 PowerToys CmdPal 扩展框架构建（Microsoft Command Palette Extensions Toolkit）。
- 语义重排能力对齐现代检索工程实践，感谢社区相关资料与示例。

## Thanks

- 感谢为测试与文档提供建议的贡献者们。

## License

MIT