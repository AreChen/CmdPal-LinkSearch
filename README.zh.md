# LinkSearch

[English](./README.md) | 简体中文

[PowerToys CmdPal](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview) 插件：在命令面板中快速搜索与打开你的链接资源，并可选接入 Rerank 服务以获得更相关的排序结果。

- 一站式检索：统一搜索常用网址、知识库、内部链接或来自 Linkwarden 的收藏。
- 可选重排：连接可配置的 Rerank API，按语义相关性对候选列表重排。
- 轻量易用：即装即用，支持基础设置与连接测试。

## 使用

- 打开 PowerToys 的 CmdPal（命令面板）。
- 通过顶层命令进入 LinkSearch 页：
  - 顶层命令由命令提供器注册： [LinkSearchCommandsProvider](LinkSearch/LinkSearchCommandsProvider.cs:20)
- 在 LinkSearch 页面输入关键词进行搜索，按回车打开结果。
- 若启用 Rerank，结果将调用配置的 Rerank API 进行语义重排后展示。

页面与交互：
- 入口与交互逻辑： [LinkSearchPage](LinkSearch/Pages/LinkSearchPage.cs:1)

## 配置

在 PowerToys 的 CmdPal 插件设置页中打开 LinkSearch 的设置页面（也可通过 MoreCommands 进入设置页）：
- 设置绑定： [LinkSearchCommandsProvider](LinkSearch/LinkSearchCommandsProvider.cs:24)

配置项定义见： [SettingsManager](LinkSearch/Helpers/SettingsManager.cs:11)

支持的字段（插件界面优先，可环境变量兜底，部分提供默认值）：
- LinkwardenBaseUrl
  - 描述：Linkwarden 服务器地址。多级优先级：插件设置 > 环境变量 LINKWARDEN_BASE_URL > 默认 https://cloud.linkwarden.app
- LinkwardenApiKey
  - 描述：Linkwarden API 访问令牌。多级优先级：插件设置 > 环境变量 LINKWARDEN_API_KEY
- EnableRerank
  - 描述：是否启用 Rerank。多级优先级：插件设置（布尔）> 环境变量 LINKSEARCH_ENABLE_RERANK（true/false）。默认 false。
- RerankApiUrl
  - 描述：Rerank API URL。多级优先级：插件设置 > 环境变量 LINKSEARCH_RERANK_API_URL > 默认 https://api.siliconflow.cn/v1/rerank
- RerankApiKey
  - 描述：Rerank API Key。多级优先级：插件设置 > 环境变量 LINKSEARCH_RERANK_API_KEY
- RerankModelName
  - 描述：Rerank 模型名。多级优先级：插件设置 > 环境变量 LINKSEARCH_RERANK_MODEL_NAME > 默认 BAAI/bge-reranker-v2-m3
- SearchDelayMilliseconds
  - 描述：搜索输入后的防抖延迟，范围 300-2000ms。多级优先级：插件设置 > 环境变量 LINKSEARCH_SEARCH_DELAY_MS，默认 600ms，越界值会被钳制到范围内。

环境变量示例（PowerShell）：
```powershell
$env:LINKWARDEN_BASE_URL="https://cloud.linkwarden.app"
$env:LINKWARDEN_API_KEY="your-linkwarden-token"
$env:LINKSEARCH_ENABLE_RERANK="true"
$env:LINKSEARCH_RERANK_API_URL="https://api.siliconflow.cn/v1/rerank"
$env:LINKSEARCH_RERANK_API_KEY="your-rerank-key"
$env:LINKSEARCH_RERANK_MODEL_NAME="BAAI/bge-reranker-v2-m3"
$env:LINKSEARCH_SEARCH_DELAY_MS="600"
```

Rerank 服务调用逻辑：
- 服务实现： [RerankService](LinkSearch/Services/RerankService.cs:18)
- 对于每条链接，会构造“名称/描述/URL/标签/集合”等上下文作为 document 发送至 Rerank API，并按返回的索引顺序重排。
- 连接测试： [RerankConnectionTestService](LinkSearch/Services/RerankConnectionTestService.cs:14) 提供快速“连通性/鉴权/响应”检查，便于定位 API URL、Key 或模型名配置问题。

## 开发与构建

- 解决方案与项目：
  - [LinkSearch.sln](LinkSearch.sln:1)
  - [LinkSearch.csproj](LinkSearch/LinkSearch.csproj:1)
- 应用与打包清单：
  - [app.manifest](LinkSearch/app.manifest:1)
  - [Package.appxmanifest](LinkSearch/Package.appxmanifest:1)
- 发布配置：
  - [Properties/PublishProfiles](LinkSearch/Properties/PublishProfiles/)

默认 Release 构建后，会在 bin/x64/Release/.../AppPackages 生成可安装的 MSIX 包与安装脚本。

## 路线图

- [ ] Microsoft Store 上架与 WinGet 投递
- [ ] 更丰富的数据源（本地索引/更多服务）
- [ ] 可选离线重排能力（本地模型/嵌入）
- [ ] 增强页面交互与筛选

欢迎 Issue / PR！

## Credits

- 基于 PowerToys CmdPal 扩展框架构建（Microsoft Command Palette Extensions Toolkit）。
- 语义重排能力对齐现代检索工程实践，感谢社区相关资料与示例。

## Thanks

- 感谢为测试与文档提供建议的贡献者们。

## License

MIT