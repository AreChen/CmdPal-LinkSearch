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
    <a href="./docs/ranking.zh.md#rerank-api">Rerank API</a> •
    <a href="./docs/ranking.zh.md#决策模型-api">决策模型 API</a> •
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
      <img src="./docs/assets/icons/search.svg" width="32" alt="搜索图标" />
      <h3>快速检索</h3>
      <p>直接在 PowerToys Command Palette 中搜索 Linkwarden 收藏。</p>
    </td>
    <td width="25%" valign="top">
      <img src="./docs/assets/icons/external-link.svg" width="32" alt="打开链接图标" />
      <h3>快速打开</h3>
      <p>从搜索结果直接打开保存的网页，减少上下文切换。</p>
    </td>
    <td width="25%" valign="top">
      <img src="./docs/assets/icons/sparkles.svg" width="32" alt="重排图标" />
      <h3>可选重排</h3>
      <p>使用 Rerank API 或决策模型，按语义相关性重新排序。</p>
    </td>
    <td width="25%" valign="top">
      <img src="./docs/assets/icons/languages.svg" width="32" alt="语言图标" />
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
6. 可选：启用重排序，选择服务并填写配置，点击 **保存并测试连接**。
7. 输入快捷键，开始检索。

## 排序模式

在设置中启用重排序并选择服务：

- **Rerank API**：使用兼容 rerank 接口的服务，保留已有 Rerank 设置。
- **OpenRouter** 或 **自建 System One**：选择逐项判断（Noul）或候选选择（Choice）。
- **OpenAI**：使用 Score 为候选评分并排序。

各服务的配置和密钥相互独立。旧版 OpenAI 配置会保留地址、密钥和模型，并改用 Score。

重排序时会向所选服务发送搜索词以及候选的标题、描述和标签，这些字段可能包含敏感内容。搜索结果先行展示，排序完成后更新；失败时显示错误并保留原顺序。重排候选上限默认为 50，可独立于显示结果上限调整。

配置步骤、接口格式及请求示例请参阅 [排序 API 使用指南](./docs/ranking.zh.md)。

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
