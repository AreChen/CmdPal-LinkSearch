<div align="center">
  <img src="./LinkSearch/Assets/StoreLogo.png" width="96" alt="LinkSearch logo" />
  <h1>LinkSearch</h1>
  <p><strong>在 Microsoft PowerToys Command Palette 中搜索 Linkwarden。</strong></p>
  <p>快速找到收藏链接、打开页面，并可选接入 Rerank 或决策模型优化结果排序。</p>
  <p><a href="./README.md">English</a> | 简体中文</p>
  <p>
    <a href="https://apps.microsoft.com/detail/9MZ9Q4CFP2N9"><img alt="从 Microsoft Store 获取" src="https://img.shields.io/badge/Get_it_on-Microsoft_Store-0078D4?style=for-the-badge&logo=microsoftstore&logoColor=white" /></a>
    <a href="https://github.com/AreChen/CmdPal-LinkSearch/releases/latest"><img alt="最新版本" src="https://img.shields.io/github/v/release/AreChen/CmdPal-LinkSearch?style=for-the-badge&logo=github&color=24292f" /></a>
    <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
    <img alt="MIT License" src="https://img.shields.io/badge/License-MIT-0f766e?style=for-the-badge" />
  </p>
  <p>
    <a href="#安装">安装</a> •
    <a href="#功能">功能</a> •
    <a href="#使用">使用</a> •
    <a href="#rerank-api">Rerank API</a> •
    <a href="./PRIVACY.md">隐私</a>
  </p>
</div>

---

## 预览

<div align="center">
  <img src="https://i.imgur.com/fcsbu0o.gif" alt="LinkSearch 在 PowerToys Command Palette 中搜索" width="820" />
  <br />
  <br />
  <img src="https://i.imgur.com/eEM6ZtC.png" alt="LinkSearch 设置页面" width="820" />
</div>

## 功能

<table>
  <tr>
    <td width="25%" valign="top">
      <img src="https://api.iconify.design/lucide:search.svg?color=%230078D4" width="32" alt="搜索图标" />
      <h3>快速检索</h3>
      <p>直接在 PowerToys Command Palette 中搜索 Linkwarden 收藏。</p>
    </td>
    <td width="25%" valign="top">
      <img src="https://api.iconify.design/lucide:external-link.svg?color=%230078D4" width="32" alt="打开链接图标" />
      <h3>快速打开</h3>
      <p>从搜索结果直接打开保存的网页，减少上下文切换。</p>
    </td>
    <td width="25%" valign="top">
      <img src="https://api.iconify.design/lucide:sparkles.svg?color=%230078D4" width="32" alt="重排图标" />
      <h3>可选重排</h3>
      <p>使用 Rerank API 或决策模型，按语义相关性重新排序。</p>
    </td>
    <td width="25%" valign="top">
      <img src="https://api.iconify.design/lucide:languages.svg?color=%230078D4" width="32" alt="语言图标" />
      <h3>双语界面</h3>
      <p>可在扩展设置中选择自动、简体中文或 English。</p>
    </td>
  </tr>
</table>

## 安装

> LinkSearch 需要已安装支持 Command Palette 的 Microsoft PowerToys。

<a href="https://apps.microsoft.com/detail/9MZ9Q4CFP2N9"><img alt="从 Microsoft Store 获取" src="https://img.shields.io/badge/Get_it_on-Microsoft_Store-0078D4?style=for-the-badge&logo=microsoftstore&logoColor=white" /></a>

- 推荐：从 [Microsoft Store](https://apps.microsoft.com/detail/9MZ9Q4CFP2N9) 安装 LinkSearch。
- 备用：如果无法使用 Microsoft Store，可下载最新 [GitHub Release](https://github.com/AreChen/CmdPal-LinkSearch/releases/latest) 中的 MSIX 侧载包。

## 使用

1. 打开 PowerToys CmdPal（Command Palette）。
2. 进入扩展设置页面并打开 LinkSearch 扩展。
3. 为扩展配置你喜欢的快捷键。
4. 配置 Linkwarden 服务器地址、访问令牌和最大检索返回结果数量。
5. 选择界面语言：自动、简体中文或 English。
6. 可选：启用重排序，选择模式并配置对应服务的 API URL、密钥和模型名。
7. 输入快捷键，开始检索。

## Rerank API

在设置中启用重排序并选择以下模式：

- **Rerank API**：使用兼容 rerank 接口的服务，保留已有 Rerank 设置。
- **决策模型：逐项判断 (Noul)**：通过 System One 接口分别判断每个候选的相关性。
- **决策模型：候选选择 (Choice)**：通过 System One 接口比较所有候选，按返回的相对概率排序。

决策模型需单独设置完整的 System One 地址（例如 `https://decision.example.com/v1/systemone`）、API 密钥和模型名称（例如 `Eapil-D1`）。服务必须支持所选的 `noul` 或 `choice` 问题类型。也可使用环境变量 `LINKSEARCH_DECISION_API_URL`、`LINKSEARCH_DECISION_API_KEY` 和 `LINKSEARCH_DECISION_MODEL_NAME`；扩展设置中填写的值优先。

重排序时只发送搜索词以及候选的标题、描述、标签，不发送独立的链接地址和收藏夹字段；标题、描述或标签本身仍可能包含敏感内容。请求失败或决策响应不完整时，保留原始搜索顺序。Choice 一次比较全部候选，不会合并不同批次的概率；候选过多可能超过服务限制。

Rerank API 模式兼容暴露类似 rerank endpoint 的服务。

请求示例：

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

返回示例：

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

- [x] Microsoft Store 上架。

## Credits

- 基于 PowerToys CmdPal 扩展框架构建（Microsoft Command Palette Extensions Toolkit）。
- 语义重排能力对齐现代检索工程实践，感谢社区相关资料与示例。

## 贡献

欢迎提交 Issue 和 Pull Request。

## 隐私

[隐私政策](./PRIVACY.md)

## License

MIT
