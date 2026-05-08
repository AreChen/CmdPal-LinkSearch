# LinkSearch Bilingual Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Chinese/English language selection, split Linkwarden search and rerank responsibilities into testable units, and add a short-lived in-memory search cache.

**Architecture:** Keep the PowerToys Command Palette provider and manifest registration stable. Extract localization, Linkwarden parsing/search, presentation, and caching behind focused internal types, then shrink `LinkSearchPage` back to CmdPal lifecycle, debounce, cancellation, and item refresh.

**Tech Stack:** .NET 9, C#, Microsoft.CommandPalette.Extensions.Toolkit 0.2.0, System.Text.Json, xUnit, Microsoft.NET.Test.Sdk.

---

## Commit Policy

The plan uses checkpoint steps instead of git commits. Do not create a git commit unless the user explicitly asks for one.

## File Structure

Create:

- `LinkSearch.Tests/LinkSearch.Tests.csproj`: host-independent xUnit tests.
- `LinkSearch.Tests/Localization/LocalizedStringsTests.cs`: language parsing, auto detection, and string lookup tests.
- `LinkSearch.Tests/Helpers/SettingsManagerTests.cs`: settings parsing and clamping tests.
- `LinkSearch.Tests/Services/LinkwardenResponseParserTests.cs`: Linkwarden JSON parser tests.
- `LinkSearch.Tests/Services/SearchCacheTests.cs`: in-memory cache behavior tests.
- `LinkSearch.Tests/Services/RerankOrderingTests.cs`: rerank ordering fallback tests.
- `LinkSearch/Properties/AssemblyInfo.cs`: exposes internals to `LinkSearch.Tests`.
- `LinkSearch/Localization/LanguageMode.cs`: persisted and resolved language enums.
- `LinkSearch/Localization/LocalizedTextKey.cs`: string keys for settings and prompts.
- `LinkSearch/Localization/LocalizedStrings.cs`: localized string table and language resolution helpers.
- `LinkSearch/Models/LinkwardenLink.cs`: internal link model used outside UI code.
- `LinkSearch/Models/LinkwardenSearchError.cs`: typed search error categories.
- `LinkSearch/Models/LinkwardenSearchResult.cs`: service return model.
- `LinkSearch/Services/LinkwardenResponseParser.cs`: JSON response parsing.
- `LinkSearch/Services/SearchCache.cs`: short-lived in-memory cache.
- `LinkSearch/Services/LinkwardenService.cs`: Linkwarden HTTP search service.
- `LinkSearch/Presenters/SearchResultPresenter.cs`: maps models/errors to CmdPal list items.

Modify:

- `Directory.Packages.props`: add test package versions.
- `LinkSearch.sln`: add `LinkSearch.Tests`.
- `LinkSearch/Helpers/SettingsManager.cs`: add language setting, culture injection for tests, localized labels, and cleaner parsing helpers.
- `LinkSearch/Services/RerankService.cs`: accept `LinkwardenLink` models and expose deterministic rerank ordering helper.
- `LinkSearch/Services/RerankConnectionTestService.cs`: localize user-facing connection test result strings through caller or helper.
- `LinkSearch/Pages/LinkSearchPage.cs`: remove embedded Linkwarden parsing, models, and UI strings; call services and presenter.
- `LinkSearch/LinkSearchCommandsProvider.cs`: construct and own new services, presenter, and cache; avoid double-disposing services from page and provider.
- `README.md` and `README.zh.md`: remove the obsolete `LinkSearch/config.json` setup hint if it is still shown in runtime prompts or docs.

## Task 1: Add Test Project and Internal Access

**Files:**

- Create: `LinkSearch.Tests/LinkSearch.Tests.csproj`
- Create: `LinkSearch/Properties/AssemblyInfo.cs`
- Modify: `Directory.Packages.props`
- Modify: `LinkSearch.sln`

- [ ] **Step 1: Add central test package versions**

Use `apply_patch` to add these package versions inside the existing `Directory.Packages.props` `<ItemGroup>`:

```xml
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.0.2" />
```

- [ ] **Step 2: Create the xUnit project file**

Create `LinkSearch.Tests/LinkSearch.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows10.0.22621.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\LinkSearch\LinkSearch.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Expose internals to tests**

Create `LinkSearch/Properties/AssemblyInfo.cs` with:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("LinkSearch.Tests")]
```

- [ ] **Step 4: Add the test project to the solution**

Run:

```powershell
dotnet sln "LinkSearch.sln" add "LinkSearch.Tests\LinkSearch.Tests.csproj"
```

Expected: solution reports that `LinkSearch.Tests` was added.

- [ ] **Step 5: Verify baseline tests run**

Run:

```powershell
dotnet test "LinkSearch.sln" -p:Platform=x64
```

Expected: build succeeds, `LinkSearch.Tests` reports 0 tests, existing CA1852 warnings may still appear.

- [ ] **Step 6: Checkpoint**

Run:

```powershell
git status --short
```

Expected: new test project files and solution/package changes are listed. Do not commit unless the user asks.

## Task 2: Add Localization Primitives and Tests

**Files:**

- Create: `LinkSearch.Tests/Localization/LocalizedStringsTests.cs`
- Create: `LinkSearch/Localization/LanguageMode.cs`
- Create: `LinkSearch/Localization/LocalizedTextKey.cs`
- Create: `LinkSearch/Localization/LocalizedStrings.cs`

- [ ] **Step 1: Write failing localization tests**

Create `LinkSearch.Tests/Localization/LocalizedStringsTests.cs` with:

```csharp
using System.Globalization;
using LinkSearch.Localization;
using Xunit;

namespace LinkSearch.Tests.Localization;

public sealed class LocalizedStringsTests
{
    [Theory]
    [InlineData(null, LanguageMode.Auto)]
    [InlineData("", LanguageMode.Auto)]
    [InlineData("auto", LanguageMode.Auto)]
    [InlineData("zh-CN", LanguageMode.Chinese)]
    [InlineData("zh", LanguageMode.Chinese)]
    [InlineData("en-US", LanguageMode.English)]
    [InlineData("en", LanguageMode.English)]
    [InlineData("unexpected", LanguageMode.Auto)]
    public void ParseLanguageMode_accepts_known_values(string? value, LanguageMode expected)
    {
        Assert.Equal(expected, LocalizedStrings.ParseLanguageMode(value));
    }

    [Theory]
    [InlineData(LanguageMode.Auto, "zh-CN", UiLanguage.Chinese)]
    [InlineData(LanguageMode.Auto, "zh-Hans", UiLanguage.Chinese)]
    [InlineData(LanguageMode.Auto, "en-US", UiLanguage.English)]
    [InlineData(LanguageMode.Chinese, "en-US", UiLanguage.Chinese)]
    [InlineData(LanguageMode.English, "zh-CN", UiLanguage.English)]
    public void ResolveUiLanguage_uses_manual_override_or_culture(LanguageMode mode, string culture, UiLanguage expected)
    {
        Assert.Equal(expected, LocalizedStrings.ResolveUiLanguage(mode, CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void Get_returns_selected_language_text()
    {
        Assert.Equal("语言", LocalizedStrings.Get(LocalizedTextKey.LanguageSettingLabel, UiLanguage.Chinese));
        Assert.Equal("Language", LocalizedStrings.Get(LocalizedTextKey.LanguageSettingLabel, UiLanguage.English));
    }
}
```

- [ ] **Step 2: Run localization tests to verify failure**

Run:

```powershell
dotnet test "LinkSearch.Tests\LinkSearch.Tests.csproj" -p:Platform=x64 --filter LocalizedStringsTests
```

Expected: FAIL with compiler errors for missing `LinkSearch.Localization` types.

- [ ] **Step 3: Add language enums**

Create `LinkSearch/Localization/LanguageMode.cs` with:

```csharp
namespace LinkSearch.Localization;

internal enum LanguageMode
{
    Auto,
    Chinese,
    English,
}

internal enum UiLanguage
{
    Chinese,
    English,
}
```

- [ ] **Step 4: Add localization keys**

Create `LinkSearch/Localization/LocalizedTextKey.cs` with:

```csharp
namespace LinkSearch.Localization;

internal enum LocalizedTextKey
{
    LanguageSettingLabel,
    LanguageSettingDescription,
    LanguageAutoChoice,
    LanguageChineseChoice,
    LanguageEnglishChoice,
    LinkwardenBaseUrlLabel,
    LinkwardenBaseUrlDescription,
    LinkwardenApiKeyLabel,
    LinkwardenApiKeyDescription,
    EnableRerankLabel,
    EnableRerankDescription,
    RerankApiUrlLabel,
    RerankApiUrlDescription,
    RerankApiKeyLabel,
    RerankApiKeyDescription,
    RerankModelNameLabel,
    RerankModelNameDescription,
    SearchDelayLabel,
    SearchDelayDescription,
    MaxResultsLabel,
    MaxResultsDescription,
    PageName,
    SearchPlaceholder,
    EmptyResultTitle,
    EmptyResultSubtitle,
    EmptyQueryTitle,
    OpenLinkCommandName,
    SearchFailed,
    RetryOrCheckSettings,
    MissingTokenTitle,
    ConfigureApiKeyTitle,
    EnvTokenHintTitle,
    InvalidApiKeyTitle,
    InvalidApiKeySubtitle,
    InvalidBaseUrlTitle,
    InvalidBaseUrlSubtitle,
    ApiRequestFailed,
    MissingDataNode,
    InvalidDataNode,
    LinksNotArray,
    NetworkRequestFailed,
    CheckServerAndNetwork,
    RequestTimeout,
    RetryLater,
    UrlFormatError,
    CheckServerAddress,
    ApiCallException,
    TagLabel,
    RerankConnectionSuccess,
    RerankConnectionFailed,
    RerankConnectionException,
}
```

- [ ] **Step 5: Add localized string table**

Create `LinkSearch/Localization/LocalizedStrings.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace LinkSearch.Localization;

internal static class LocalizedStrings
{
    private static readonly IReadOnlyDictionary<LocalizedTextKey, (string Chinese, string English)> s_strings =
        new Dictionary<LocalizedTextKey, (string Chinese, string English)>
        {
            [LocalizedTextKey.LanguageSettingLabel] = ("语言", "Language"),
            [LocalizedTextKey.LanguageSettingDescription] = ("选择扩展界面语言：auto、zh-CN 或 en-US", "Choose the extension UI language: auto, zh-CN, or en-US"),
            [LocalizedTextKey.LanguageAutoChoice] = ("自动", "Automatic"),
            [LocalizedTextKey.LanguageChineseChoice] = ("简体中文", "Simplified Chinese"),
            [LocalizedTextKey.LanguageEnglishChoice] = ("English", "English"),
            [LocalizedTextKey.LinkwardenBaseUrlLabel] = ("Linkwarden 服务器地址", "Linkwarden Base URL"),
            [LocalizedTextKey.LinkwardenBaseUrlDescription] = ("Linkwarden 服务器的 URL 地址", "URL of your Linkwarden server"),
            [LocalizedTextKey.LinkwardenApiKeyLabel] = ("Linkwarden API Key", "Linkwarden API Key"),
            [LocalizedTextKey.LinkwardenApiKeyDescription] = ("Linkwarden API 访问令牌", "Linkwarden API access token"),
            [LocalizedTextKey.EnableRerankLabel] = ("启用 Rerank", "Enable Rerank"),
            [LocalizedTextKey.EnableRerankDescription] = ("使用 Rerank API 对搜索结果重新排序", "Use the Rerank API to reorder search results"),
            [LocalizedTextKey.RerankApiUrlLabel] = ("Rerank API 地址", "Rerank API URL"),
            [LocalizedTextKey.RerankApiUrlDescription] = ("Rerank API 的 URL 地址", "URL of the Rerank API endpoint"),
            [LocalizedTextKey.RerankApiKeyLabel] = ("Rerank API Key", "Rerank API Key"),
            [LocalizedTextKey.RerankApiKeyDescription] = ("Rerank API 访问密钥", "Rerank API access key"),
            [LocalizedTextKey.RerankModelNameLabel] = ("Rerank 模型名称", "Rerank model name"),
            [LocalizedTextKey.RerankModelNameDescription] = ("用于 Rerank 的模型名称", "Model name used for reranking"),
            [LocalizedTextKey.SearchDelayLabel] = ("搜索延迟时间（毫秒）", "Search delay (ms)"),
            [LocalizedTextKey.SearchDelayDescription] = ("输入停止后的搜索延迟，范围 300-2000 毫秒", "Delay after typing stops, from 300 to 2000 ms"),
            [LocalizedTextKey.MaxResultsLabel] = ("最大检索结果数量", "Maximum results"),
            [LocalizedTextKey.MaxResultsDescription] = ("最大检索结果数量，范围 1-200", "Maximum number of search results, from 1 to 200"),
            [LocalizedTextKey.PageName] = ("打开", "Open"),
            [LocalizedTextKey.SearchPlaceholder] = ("请输入关键词进行 Linkwarden 检索", "Enter keywords to search Linkwarden"),
            [LocalizedTextKey.EmptyResultTitle] = ("未找到相关结果", "No matching results"),
            [LocalizedTextKey.EmptyResultSubtitle] = ("请尝试其他关键词", "Try different keywords"),
            [LocalizedTextKey.EmptyQueryTitle] = ("请输入关键词进行 Linkwarden 检索", "Enter keywords to search Linkwarden"),
            [LocalizedTextKey.OpenLinkCommandName] = ("打开链接", "Open link"),
            [LocalizedTextKey.SearchFailed] = ("搜索失败: {0}", "Search failed: {0}"),
            [LocalizedTextKey.RetryOrCheckSettings] = ("请稍后重试或检查设置", "Try again later or check settings"),
            [LocalizedTextKey.MissingTokenTitle] = ("未检测到 Linkwarden API Token", "Linkwarden API token was not found"),
            [LocalizedTextKey.ConfigureApiKeyTitle] = ("请在插件设置中配置 API Key", "Configure the API key in extension settings"),
            [LocalizedTextKey.EnvTokenHintTitle] = ("或设置环境变量: set LINKWARDEN_API_KEY=your_token", "Or set environment variable: set LINKWARDEN_API_KEY=your_token"),
            [LocalizedTextKey.InvalidApiKeyTitle] = ("API Key 格式无效", "Invalid API key format"),
            [LocalizedTextKey.InvalidApiKeySubtitle] = ("请检查您的 API Key 是否正确", "Check whether your API key is correct"),
            [LocalizedTextKey.InvalidBaseUrlTitle] = ("Linkwarden Base URL 无效", "Invalid Linkwarden Base URL"),
            [LocalizedTextKey.InvalidBaseUrlSubtitle] = ("请在插件设置中配置 Base URL", "Configure the Base URL in extension settings"),
            [LocalizedTextKey.ApiRequestFailed] = ("API 请求失败: {0}", "API request failed: {0}"),
            [LocalizedTextKey.MissingDataNode] = ("API 响应格式错误: 缺少 data 节点", "Invalid API response: missing data node"),
            [LocalizedTextKey.InvalidDataNode] = ("API 响应格式错误: data 节点格式不正确", "Invalid API response: unsupported data node format"),
            [LocalizedTextKey.LinksNotArray] = ("API 响应格式错误: links 节点不是数组类型", "Invalid API response: links node is not an array"),
            [LocalizedTextKey.NetworkRequestFailed] = ("网络请求失败：请检查网络连接和服务器地址", "Network request failed: check your network connection and server address"),
            [LocalizedTextKey.CheckServerAndNetwork] = ("请检查服务器地址和网络连接", "Check the server address and network connection"),
            [LocalizedTextKey.RequestTimeout] = ("请求超时：服务器响应时间过长", "Request timed out: the server took too long to respond"),
            [LocalizedTextKey.RetryLater] = ("请检查网络连接或稍后重试", "Check your network connection or try again later"),
            [LocalizedTextKey.UrlFormatError] = ("URL 格式错误：服务器地址无效", "URL format error: invalid server address"),
            [LocalizedTextKey.CheckServerAddress] = ("请在插件设置中检查服务器地址", "Check the server address in extension settings"),
            [LocalizedTextKey.ApiCallException] = ("API 调用异常: {0}", "API call exception: {0}"),
            [LocalizedTextKey.TagLabel] = ("标签", "Tags"),
            [LocalizedTextKey.RerankConnectionSuccess] = ("连接成功！响应时间: {0}ms", "Connection succeeded. Response time: {0}ms"),
            [LocalizedTextKey.RerankConnectionFailed] = ("连接失败: {0} - {1}", "Connection failed: {0} - {1}"),
            [LocalizedTextKey.RerankConnectionException] = ("测试连接时发生异常: {0}", "Connection test exception: {0}"),
        };

    public static LanguageMode ParseLanguageMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "zh" or "zh-cn" or "chinese" => LanguageMode.Chinese,
            "en" or "en-us" or "english" => LanguageMode.English,
            _ => LanguageMode.Auto,
        };
    }

    public static string ToSettingValue(LanguageMode mode)
    {
        return mode switch
        {
            LanguageMode.Chinese => "zh-CN",
            LanguageMode.English => "en-US",
            _ => "auto",
        };
    }

    public static UiLanguage ResolveUiLanguage(LanguageMode mode, CultureInfo culture)
    {
        return mode switch
        {
            LanguageMode.Chinese => UiLanguage.Chinese,
            LanguageMode.English => UiLanguage.English,
            _ when culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) => UiLanguage.Chinese,
            _ => UiLanguage.English,
        };
    }

    public static string Get(LocalizedTextKey key, UiLanguage language)
    {
        if (!s_strings.TryGetValue(key, out var value))
        {
            return key.ToString();
        }

        return language == UiLanguage.Chinese ? value.Chinese : value.English;
    }

    public static string Format(LocalizedTextKey key, UiLanguage language, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key, language), args);
    }
}
```

- [ ] **Step 6: Run localization tests to verify pass**

Run:

```powershell
dotnet test "LinkSearch.Tests\LinkSearch.Tests.csproj" -p:Platform=x64 --filter LocalizedStringsTests
```

Expected: PASS.

## Task 3: Localize Settings and Add Settings Tests

**Files:**

- Create: `LinkSearch.Tests/Helpers/SettingsManagerTests.cs`
- Modify: `LinkSearch/Helpers/SettingsManager.cs`

- [ ] **Step 1: Write failing settings tests**

Create `LinkSearch.Tests/Helpers/SettingsManagerTests.cs` with:

```csharp
using System.Globalization;
using LinkSearch.Helpers;
using LinkSearch.Localization;
using Xunit;

namespace LinkSearch.Tests.Helpers;

public sealed class SettingsManagerTests
{
    [Fact]
    public void CurrentUiLanguage_uses_auto_culture()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("zh-CN"));

        Assert.Equal(LanguageMode.Auto, settings.LanguageMode);
        Assert.Equal(UiLanguage.Chinese, settings.CurrentUiLanguage);
    }

    [Fact]
    public void ApplyLocalizedText_updates_setting_labels()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));

        Assert.Contains(settings.Settings.Settings, setting => setting.Label == "Language");
        Assert.Contains(settings.Settings.Settings, setting => setting.Label == "Linkwarden Base URL");
    }

    [Theory]
    [InlineData("10", 300)]
    [InlineData("600", 600)]
    [InlineData("3000", 2000)]
    public void SearchDelayMilliseconds_clamps_values(string value, int expected)
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.SetForTest("linkSearch.SearchDelayMilliseconds", value);

        Assert.Equal(expected, settings.SearchDelayMilliseconds);
    }

    private sealed class TempSettingsFile : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LinkSearch.Tests", Guid.NewGuid().ToString("N"));

        public TempSettingsFile()
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "settings.json");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
```

- [ ] **Step 2: Run settings tests to verify failure**

Run:

```powershell
dotnet test "LinkSearch.Tests\LinkSearch.Tests.csproj" -p:Platform=x64 --filter SettingsManagerTests
```

Expected: FAIL with missing constructor, language properties, or `SetForTest`.

- [ ] **Step 3: Update settings fields and constructor**

In `LinkSearch/Helpers/SettingsManager.cs`, add these usings:

```csharp
using System.Collections.Generic;
using System.Globalization;
using LinkSearch.Localization;
```

Replace the setting field declarations with non-readonly labels that can be localized:

```csharp
    private readonly Func<CultureInfo> _cultureProvider;

    private readonly ChoiceSetSetting _language = new(
        Namespaced(nameof(LanguageMode)),
        "Language",
        "Choose the extension UI language",
        new List<ChoiceSetSetting.Choice>
        {
            new("auto", "Automatic"),
            new("zh-CN", "Simplified Chinese"),
            new("en-US", "English"),
        })
    {
        Value = "auto",
    };

    private readonly TextSetting _linkwardenBaseUrl = new(Namespaced(nameof(LinkwardenBaseUrl)), string.Empty, string.Empty, string.Empty);
    private readonly TextSetting _linkwardenApiKey = new(Namespaced(nameof(LinkwardenApiKey)), string.Empty, string.Empty, string.Empty);
    private readonly ToggleSetting _enableRerank = new(Namespaced(nameof(EnableRerank)), string.Empty, string.Empty, false);
    private readonly TextSetting _rerankApiUrl = new(Namespaced(nameof(RerankApiUrl)), string.Empty, string.Empty, "https://api.siliconflow.cn/v1/rerank");
    private readonly TextSetting _rerankApiKey = new(Namespaced(nameof(RerankApiKey)), string.Empty, string.Empty, string.Empty);
    private readonly TextSetting _rerankModelName = new(Namespaced(nameof(RerankModelName)), string.Empty, string.Empty, "BAAI/bge-reranker-v2-m3");
    private readonly TextSetting _searchDelayMilliseconds = new(Namespaced(nameof(SearchDelayMilliseconds)), string.Empty, string.Empty, "600");
    private readonly TextSetting _maxResults = new(Namespaced(nameof(MaxResults)), string.Empty, string.Empty, "50");
```

Replace the constructor with:

```csharp
    public SettingsManager(string? settingsFilePath = null, Func<CultureInfo>? cultureProvider = null)
    {
        _cultureProvider = cultureProvider ?? (() => CultureInfo.CurrentUICulture);
        FilePath = settingsFilePath ?? SettingsJsonPath();

        Settings.Add(_language);
        Settings.Add(_linkwardenBaseUrl);
        Settings.Add(_linkwardenApiKey);
        Settings.Add(_enableRerank);
        Settings.Add(_rerankApiUrl);
        Settings.Add(_rerankApiKey);
        Settings.Add(_rerankModelName);
        Settings.Add(_searchDelayMilliseconds);
        Settings.Add(_maxResults);

        LoadSettings();
        ApplyLocalizedSettingText();

        Settings.SettingsChanged += (s, a) =>
        {
            ApplyLocalizedSettingText();
            SaveSettings();
        };
    }
```

- [ ] **Step 4: Add language and localization helpers to SettingsManager**

Add these members after the `Namespaced` method:

```csharp
    public LanguageMode LanguageMode => LocalizedStrings.ParseLanguageMode(_language.Value);

    public UiLanguage CurrentUiLanguage => LocalizedStrings.ResolveUiLanguage(LanguageMode, _cultureProvider());

    public string Text(LocalizedTextKey key) => LocalizedStrings.Get(key, CurrentUiLanguage);

    public string Format(LocalizedTextKey key, params object[] args) => LocalizedStrings.Format(key, CurrentUiLanguage, args);

    internal void SetForTest(string key, string value)
    {
        foreach (var setting in Settings.Settings)
        {
            if (setting.Key == key)
            {
                switch (setting)
                {
                    case TextSetting textSetting:
                        textSetting.Value = value;
                        return;
                    case ChoiceSetSetting choiceSetting:
                        choiceSetting.Value = value;
                        return;
                }
            }
        }

        throw new InvalidOperationException($"Setting key was not found: {key}");
    }

    private void ApplyLocalizedSettingText()
    {
        var language = CurrentUiLanguage;

        _language.Label = LocalizedStrings.Get(LocalizedTextKey.LanguageSettingLabel, language);
        _language.Description = LocalizedStrings.Get(LocalizedTextKey.LanguageSettingDescription, language);
        _language.Choices = new List<ChoiceSetSetting.Choice>
        {
            new("auto", LocalizedStrings.Get(LocalizedTextKey.LanguageAutoChoice, language)),
            new("zh-CN", LocalizedStrings.Get(LocalizedTextKey.LanguageChineseChoice, language)),
            new("en-US", LocalizedStrings.Get(LocalizedTextKey.LanguageEnglishChoice, language)),
        };

        _linkwardenBaseUrl.Label = LocalizedStrings.Get(LocalizedTextKey.LinkwardenBaseUrlLabel, language);
        _linkwardenBaseUrl.Description = LocalizedStrings.Get(LocalizedTextKey.LinkwardenBaseUrlDescription, language);
        _linkwardenApiKey.Label = LocalizedStrings.Get(LocalizedTextKey.LinkwardenApiKeyLabel, language);
        _linkwardenApiKey.Description = LocalizedStrings.Get(LocalizedTextKey.LinkwardenApiKeyDescription, language);
        _enableRerank.Label = LocalizedStrings.Get(LocalizedTextKey.EnableRerankLabel, language);
        _enableRerank.Description = LocalizedStrings.Get(LocalizedTextKey.EnableRerankDescription, language);
        _rerankApiUrl.Label = LocalizedStrings.Get(LocalizedTextKey.RerankApiUrlLabel, language);
        _rerankApiUrl.Description = LocalizedStrings.Get(LocalizedTextKey.RerankApiUrlDescription, language);
        _rerankApiKey.Label = LocalizedStrings.Get(LocalizedTextKey.RerankApiKeyLabel, language);
        _rerankApiKey.Description = LocalizedStrings.Get(LocalizedTextKey.RerankApiKeyDescription, language);
        _rerankModelName.Label = LocalizedStrings.Get(LocalizedTextKey.RerankModelNameLabel, language);
        _rerankModelName.Description = LocalizedStrings.Get(LocalizedTextKey.RerankModelNameDescription, language);
        _searchDelayMilliseconds.Label = LocalizedStrings.Get(LocalizedTextKey.SearchDelayLabel, language);
        _searchDelayMilliseconds.Description = LocalizedStrings.Get(LocalizedTextKey.SearchDelayDescription, language);
        _maxResults.Label = LocalizedStrings.Get(LocalizedTextKey.MaxResultsLabel, language);
        _maxResults.Description = LocalizedStrings.Get(LocalizedTextKey.MaxResultsDescription, language);
    }
```

- [ ] **Step 5: Run settings tests to verify pass**

Run:

```powershell
dotnet test "LinkSearch.Tests\LinkSearch.Tests.csproj" -p:Platform=x64 --filter "LocalizedStringsTests|SettingsManagerTests"
```

Expected: PASS.

## Task 4: Extract Linkwarden Models and Parser

**Files:**

- Create: `LinkSearch.Tests/Services/LinkwardenResponseParserTests.cs`
- Create: `LinkSearch/Models/LinkwardenLink.cs`
- Create: `LinkSearch/Models/LinkwardenSearchError.cs`
- Create: `LinkSearch/Models/LinkwardenSearchResult.cs`
- Create: `LinkSearch/Services/LinkwardenResponseParser.cs`
- Modify: `LinkSearch/Pages/LinkSearchPage.cs`

- [ ] **Step 1: Write failing parser tests**

Create `LinkSearch.Tests/Services/LinkwardenResponseParserTests.cs` with:

```csharp
using LinkSearch.Services;
using Xunit;

namespace LinkSearch.Tests.Services;

public sealed class LinkwardenResponseParserTests
{
    [Fact]
    public void Parse_supports_data_links_shape()
    {
        const string json = """
        {
          "data": {
            "links": [
              {
                "name": "Docs",
                "description": "PowerToys docs",
                "url": "https://learn.microsoft.com",
                "tags": [{ "name": "windows" }, { "name": "cmdpal" }],
                "collection": { "name": "Dev" }
              }
            ]
          }
        }
        """;

        var result = LinkwardenResponseParser.Parse(json);

        Assert.Null(result.Error);
        var link = Assert.Single(result.Links);
        Assert.Equal("Docs", link.Name);
        Assert.Equal("PowerToys docs", link.Description);
        Assert.Equal("https://learn.microsoft.com", link.Url);
        Assert.Equal("Dev", link.Collection);
        Assert.Equal(new[] { "windows", "cmdpal" }, link.Tags);
    }

    [Fact]
    public void Parse_supports_data_array_shape()
    {
        const string json = """
        {
          "data": [
            { "name": "Link", "url": "https://example.com" }
          ]
        }
        """;

        var result = LinkwardenResponseParser.Parse(json);

        Assert.Null(result.Error);
        var link = Assert.Single(result.Links);
        Assert.Equal("Link", link.Name);
        Assert.Equal(string.Empty, link.Description);
        Assert.Equal(string.Empty, link.Collection);
        Assert.Empty(link.Tags);
    }

    [Fact]
    public void Parse_returns_response_format_error_when_data_missing()
    {
        const string json = "{ \"items\": [] }";

        var result = LinkwardenResponseParser.Parse(json);

        Assert.NotNull(result.Error);
        Assert.Equal(LinkwardenSearchErrorKind.ResponseFormat, result.Error.Kind);
        Assert.Equal(LocalizedTextKey.MissingDataNode, result.Error.MessageKey);
    }
}
```

- [ ] **Step 2: Run parser tests to verify failure**

Run:

```powershell
dotnet test "LinkSearch.Tests\LinkSearch.Tests.csproj" -p:Platform=x64 --filter LinkwardenResponseParserTests
```

Expected: FAIL with missing parser/model types.

- [ ] **Step 3: Add Linkwarden models**

Create `LinkSearch/Models/LinkwardenLink.cs` with:

```csharp
using System;
using System.Collections.Generic;

namespace LinkSearch.Models;

internal sealed record LinkwardenLink(
    string Name,
    string Description,
    string Url,
    string Collection,
    IReadOnlyList<string> Tags)
{
    public static LinkwardenLink Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, Array.Empty<string>());
}
```

Create `LinkSearch/Models/LinkwardenSearchError.cs` with:

```csharp
using LinkSearch.Localization;

namespace LinkSearch.Models;

internal enum LinkwardenSearchErrorKind
{
    Configuration,
    Authentication,
    Authorization,
    ApiFailure,
    ResponseFormat,
    Network,
    Timeout,
    Unexpected,
}

internal sealed record LinkwardenSearchError(
    LinkwardenSearchErrorKind Kind,
    LocalizedTextKey MessageKey,
    string? Detail = null,
    int? StatusCode = null);
```

Create `LinkSearch/Models/LinkwardenSearchResult.cs` with:

```csharp
using System;
using System.Collections.Generic;

namespace LinkSearch.Models;

internal sealed record LinkwardenSearchResult(IReadOnlyList<LinkwardenLink> Links, LinkwardenSearchError? Error)
{
    public static LinkwardenSearchResult Success(IReadOnlyList<LinkwardenLink> links) => new(links, null);

    public static LinkwardenSearchResult Failure(LinkwardenSearchError error) => new(Array.Empty<LinkwardenLink>(), error);
}
```

- [ ] **Step 4: Add parser implementation**

Create `LinkSearch/Services/LinkwardenResponseParser.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using LinkSearch.Localization;
using LinkSearch.Models;

namespace LinkSearch.Services;

internal static class LinkwardenResponseParser
{
    public static LinkwardenSearchResult Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Parse(document.RootElement);
    }

    public static LinkwardenSearchResult Parse(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var dataElement))
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.ResponseFormat, LocalizedTextKey.MissingDataNode));
        }

        JsonElement linksElement;
        if (dataElement.ValueKind == JsonValueKind.Object && dataElement.TryGetProperty("links", out linksElement))
        {
        }
        else if (dataElement.ValueKind == JsonValueKind.Array)
        {
            linksElement = dataElement;
        }
        else
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.ResponseFormat, LocalizedTextKey.InvalidDataNode));
        }

        if (linksElement.ValueKind != JsonValueKind.Array)
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.ResponseFormat, LocalizedTextKey.LinksNotArray));
        }

        var links = new List<LinkwardenLink>();
        foreach (var link in linksElement.EnumerateArray())
        {
            var name = ReadString(link, "name");
            var description = ReadString(link, "description");
            var url = ReadString(link, "url");
            var collection = string.Empty;

            if (link.TryGetProperty("collection", out var collectionElement) && collectionElement.ValueKind == JsonValueKind.Object)
            {
                collection = ReadString(collectionElement, "name");
            }

            var tags = new List<string>();
            if (link.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsElement.EnumerateArray())
                {
                    var tagName = ReadString(tag, "name");
                    if (!string.IsNullOrWhiteSpace(tagName))
                    {
                        tags.Add(tagName);
                    }
                }
            }

            links.Add(new LinkwardenLink(name, description, url, collection, tags));
        }

        return LinkwardenSearchResult.Success(links);
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }
}
```

- [ ] **Step 5: Remove duplicate models from LinkSearchPage**

In `LinkSearch/Pages/LinkSearchPage.cs`, delete the internal `LinkResult`, `Tag`, and `Collection` classes after rerank migration in Task 7. In this task, keep them temporarily if `RerankService` still references them.

- [ ] **Step 6: Run parser tests**

Run:

```powershell
dotnet test "LinkSearch.Tests\LinkSearch.Tests.csproj" -p:Platform=x64 --filter LinkwardenResponseParserTests
```

Expected: PASS.

## Task 5: Add Search Cache

**Files:**

- Create: `LinkSearch.Tests/Services/SearchCacheTests.cs`
- Create: `LinkSearch/Services/SearchCache.cs`

- [ ] **Step 1: Write failing cache tests**

Create `LinkSearch.Tests/Services/SearchCacheTests.cs` with:

```csharp
using LinkSearch.Models;
using LinkSearch.Services;
using Xunit;

namespace LinkSearch.Tests.Services;

public sealed class SearchCacheTests
{
    [Fact]
    public void TryGet_returns_cached_links_before_ttl_expires()
    {
        var now = new MutableClock(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        var cache = new SearchCache(TimeSpan.FromMinutes(5), now.UtcNow);
        var links = new[] { new LinkwardenLink("A", string.Empty, "https://example.com", string.Empty, Array.Empty<string>()) };

        cache.Set("https://cloud.linkwarden.app", "docs", links);

        Assert.True(cache.TryGet("https://cloud.linkwarden.app", "docs", out var cached));
        Assert.Same(links, cached);
    }

    [Fact]
    public void TryGet_expires_after_ttl()
    {
        var now = new MutableClock(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        var cache = new SearchCache(TimeSpan.FromMinutes(5), now.UtcNow);
        var links = new[] { new LinkwardenLink("A", string.Empty, "https://example.com", string.Empty, Array.Empty<string>()) };

        cache.Set("https://cloud.linkwarden.app", "docs", links);
        now.Value = now.Value.AddMinutes(6);

        Assert.False(cache.TryGet("https://cloud.linkwarden.app", "docs", out _));
    }

    [Fact]
    public void Clear_removes_cached_links()
    {
        var now = new MutableClock(DateTimeOffset.Parse("2026-05-08T00:00:00Z"));
        var cache = new SearchCache(TimeSpan.FromMinutes(5), now.UtcNow);
        var links = new[] { new LinkwardenLink("A", string.Empty, "https://example.com", string.Empty, Array.Empty<string>()) };

        cache.Set("https://cloud.linkwarden.app", "docs", links);
        cache.Clear();

        Assert.False(cache.TryGet("https://cloud.linkwarden.app", "docs", out _));
    }

    private sealed class MutableClock
    {
        public MutableClock(DateTimeOffset value)
        {
            Value = value;
        }

        public DateTimeOffset Value { get; set; }

        public DateTimeOffset UtcNow() => Value;
    }
}
```

- [ ] **Step 2: Run cache tests to verify failure**

Run:

```powershell
dotnet test "LinkSearch.Tests\LinkSearch.Tests.csproj" -p:Platform=x64 --filter SearchCacheTests
```

Expected: FAIL with missing `SearchCache`.

- [ ] **Step 3: Add cache implementation**

Create `LinkSearch/Services/SearchCache.cs` with:

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using LinkSearch.Models;

namespace LinkSearch.Services;

internal sealed class SearchCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl;
    private readonly Func<DateTimeOffset> _utcNow;

    public SearchCache(TimeSpan? ttl = null, Func<DateTimeOffset>? utcNow = null)
    {
        _ttl = ttl ?? TimeSpan.FromMinutes(5);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public bool TryGet(string baseUrl, string query, out IReadOnlyList<LinkwardenLink> links)
    {
        var key = CreateKey(baseUrl, query);
        if (_entries.TryGetValue(key, out var entry) && _utcNow() - entry.CreatedAt <= _ttl)
        {
            links = entry.Links;
            return true;
        }

        _entries.TryRemove(key, out _);
        links = Array.Empty<LinkwardenLink>();
        return false;
    }

    public void Set(string baseUrl, string query, IReadOnlyList<LinkwardenLink> links)
    {
        _entries[CreateKey(baseUrl, query)] = new CacheEntry(_utcNow(), links);
    }

    public void Clear()
    {
        _entries.Clear();
    }

    private static string CreateKey(string baseUrl, string query)
    {
        return string.Concat(baseUrl.TrimEnd('/').ToUpperInvariant(), "\n", query.Trim().ToUpperInvariant());
    }

    private sealed record CacheEntry(DateTimeOffset CreatedAt, IReadOnlyList<LinkwardenLink> Links);
}
```

- [ ] **Step 4: Run cache tests**

Run:

```powershell
dotnet test "LinkSearch.Tests\LinkSearch.Tests.csproj" -p:Platform=x64 --filter SearchCacheTests
```

Expected: PASS.

## Task 6: Add LinkwardenService

**Files:**

- Create: `LinkSearch/Services/LinkwardenService.cs`
- Modify: `LinkSearch/Pages/LinkSearchPage.cs`

- [ ] **Step 1: Add service implementation**

Create `LinkSearch/Services/LinkwardenService.cs` with:

```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LinkSearch.Helpers;
using LinkSearch.Localization;
using LinkSearch.Models;

namespace LinkSearch.Services;

internal sealed class LinkwardenService
{
    private readonly SettingsManager _settingsManager;
    private readonly HttpClient _httpClient;
    private readonly SearchCache _cache;

    public LinkwardenService(SettingsManager settingsManager, SearchCache cache, HttpClient? httpClient = null)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _httpClient = httpClient ?? HttpClientProvider.Shared;
    }

    public async Task<LinkwardenSearchResult> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return LinkwardenSearchResult.Success(Array.Empty<LinkwardenLink>());
        }

        var token = _settingsManager.LinkwardenApiKey;
        if (string.IsNullOrWhiteSpace(token))
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Configuration, LocalizedTextKey.MissingTokenTitle));
        }

        if (!SettingsManager.ValidateApiKey(token))
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Configuration, LocalizedTextKey.InvalidApiKeyTitle));
        }

        var baseUrl = _settingsManager.LinkwardenBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Configuration, LocalizedTextKey.InvalidBaseUrlTitle));
        }

        if (_cache.TryGet(baseUrl, query, out var cachedLinks))
        {
            return LinkwardenSearchResult.Success(cachedLinks);
        }

        try
        {
            var url = $"{baseUrl}/api/v1/search?searchQueryString={Uri.EscapeDataString(query)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var key = (int)response.StatusCode switch
                {
                    401 => LocalizedTextKey.InvalidApiKeyTitle,
                    403 => LocalizedTextKey.ApiRequestFailed,
                    _ => LocalizedTextKey.ApiRequestFailed,
                };
                var kind = (int)response.StatusCode switch
                {
                    401 => LinkwardenSearchErrorKind.Authentication,
                    403 => LinkwardenSearchErrorKind.Authorization,
                    _ => LinkwardenSearchErrorKind.ApiFailure,
                };

                return LinkwardenSearchResult.Failure(new LinkwardenSearchError(kind, key, response.StatusCode.ToString(), (int)response.StatusCode));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var parsed = LinkwardenResponseParser.Parse(document.RootElement);
            if (parsed.Error is null)
            {
                _cache.Set(baseUrl, query, parsed.Links);
            }

            return parsed;
        }
        catch (HttpRequestException ex)
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Network, LocalizedTextKey.NetworkRequestFailed, ex.Message));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Timeout, LocalizedTextKey.RequestTimeout));
        }
        catch (UriFormatException ex)
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Configuration, LocalizedTextKey.UrlFormatError, ex.Message));
        }
        catch (JsonException ex)
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.ResponseFormat, LocalizedTextKey.InvalidDataNode, ex.Message));
        }
        catch (IOException ex)
        {
            return LinkwardenSearchResult.Failure(new LinkwardenSearchError(LinkwardenSearchErrorKind.Network, LocalizedTextKey.NetworkRequestFailed, ex.Message));
        }
    }
}
```

- [ ] **Step 2: Build after adding service**

Run:

```powershell
dotnet build "LinkSearch.sln" -p:Platform=x64
```

Expected: build succeeds or only fails where `LinkwardenLink` is not yet wired into Rerank in Task 7.

## Task 7: Refactor RerankService to Use LinkwardenLink

**Files:**

- Create: `LinkSearch.Tests/Services/RerankOrderingTests.cs`
- Modify: `LinkSearch/Services/RerankService.cs`
- Modify: `LinkSearch/Pages/LinkSearchPage.cs`

- [ ] **Step 1: Write failing rerank ordering tests**

Create `LinkSearch.Tests/Services/RerankOrderingTests.cs` with:

```csharp
using LinkSearch.Models;
using LinkSearch.Services;
using Xunit;

namespace LinkSearch.Tests.Services;

public sealed class RerankOrderingTests
{
    [Fact]
    public void ApplyRerankOrder_reorders_by_response_indices_and_appends_missing_items()
    {
        var links = new[]
        {
            Link("A"),
            Link("B"),
            Link("C"),
        };
        var response = new RerankResponse
        {
            Results = new[]
            {
                new RerankResult { Index = 2, RelevanceScore = 0.9 },
                new RerankResult { Index = 0, RelevanceScore = 0.8 },
            },
        };

        var ordered = RerankService.ApplyRerankOrder(links, response);

        Assert.Equal(new[] { "C", "A", "B" }, ordered.Select(link => link.Name));
    }

    [Fact]
    public void ApplyRerankOrder_returns_original_order_for_empty_response()
    {
        var links = new[] { Link("A"), Link("B") };

        var ordered = RerankService.ApplyRerankOrder(links, new RerankResponse { Results = Array.Empty<RerankResult>() });

        Assert.Same(links, ordered);
    }

    private static LinkwardenLink Link(string name) => new(name, string.Empty, $"https://example.com/{name}", string.Empty, Array.Empty<string>());
}
```

- [ ] **Step 2: Run rerank ordering tests to verify failure**

Run:

```powershell
dotnet test "LinkSearch.Tests\LinkSearch.Tests.csproj" -p:Platform=x64 --filter RerankOrderingTests
```

Expected: FAIL because `RerankService.ApplyRerankOrder` does not exist and `RerankService` still uses `LinkResult`.

- [ ] **Step 3: Update RerankService method signature and document builder**

In `LinkSearch/Services/RerankService.cs`, add `using LinkSearch.Models;` if missing. Replace `RerankLinksAsync` signature and document construction with:

```csharp
        public async Task<IReadOnlyList<LinkwardenLink>> RerankLinksAsync(string query, IReadOnlyList<LinkwardenLink> links, CancellationToken cancellationToken = default)
        {
            if (!_settingsManager.EnableRerank)
            {
                return links;
            }

            var apiKey = _settingsManager.RerankApiKey;
            if (string.IsNullOrWhiteSpace(apiKey) || links.Count == 0)
            {
                return links;
            }

            try
            {
                var documents = new string[links.Count];
                var sb = new StringBuilder(256);
                for (var i = 0; i < links.Count; i++)
                {
                    var link = links[i];
                    sb.Clear();
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Name: {link.Name}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Description: {link.Description}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"URL: {link.Url}");
                    if (link.Tags.Count > 0)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"Tags: {string.Join(", ", link.Tags)}");
                    }
                    if (!string.IsNullOrWhiteSpace(link.Collection))
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"Collection: {link.Collection}");
                    }

                    documents[i] = sb.ToString();
                }

                var rerankRequest = RerankRequest.Create(query, documents, _settingsManager.RerankModelName, documents.Length, false, true);
                var rerankResponse = await CallRerankApiAsync(rerankRequest, apiKey, cancellationToken).ConfigureAwait(false);
                return ApplyRerankOrder(links, rerankResponse);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Debug($"Rerank failed and original order will be used: {ex.Message}");
                return links;
            }
        }
```

- [ ] **Step 4: Add deterministic ordering helper**

Add this method in `RerankService` before `CallRerankApiAsync`:

```csharp
        internal static IReadOnlyList<LinkwardenLink> ApplyRerankOrder(IReadOnlyList<LinkwardenLink> links, RerankResponse? response)
        {
            if (response?.Results is null || response.Results.Length == 0)
            {
                return links;
            }

            var ordered = new List<LinkwardenLink>(links.Count);
            var used = new HashSet<int>();
            foreach (var result in response.Results)
            {
                if (result.Index >= 0 && result.Index < links.Count && used.Add(result.Index))
                {
                    ordered.Add(links[result.Index]);
                }
            }

            if (ordered.Count == 0)
            {
                return links;
            }

            for (var i = 0; i < links.Count; i++)
            {
                if (!used.Contains(i))
                {
                    ordered.Add(links[i]);
                }
            }

            return ordered;
        }
```

- [ ] **Step 5: Run rerank ordering tests**

Run:

```powershell
dotnet test "LinkSearch.Tests\LinkSearch.Tests.csproj" -p:Platform=x64 --filter RerankOrderingTests
```

Expected: PASS.

## Task 8: Add SearchResultPresenter

**Files:**

- Create: `LinkSearch/Presenters/SearchResultPresenter.cs`
- Modify: `LinkSearch/Pages/LinkSearchPage.cs`

- [ ] **Step 1: Add presenter implementation**

Create `LinkSearch/Presenters/SearchResultPresenter.cs` with:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using LinkSearch.Helpers;
using LinkSearch.Localization;
using LinkSearch.Models;

namespace LinkSearch.Presenters;

internal sealed class SearchResultPresenter
{
    private readonly SettingsManager _settingsManager;

    public SearchResultPresenter(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
    }

    public IListItem CreateEmptyQueryItem()
    {
        return new ListItem(new NoOpCommand())
        {
            Title = _settingsManager.Text(LocalizedTextKey.EmptyQueryTitle),
            Icon = Icons.LinkSearchExtIcon,
        };
    }

    public IListItem CreateEmptyResultItem()
    {
        return new ListItem(new NoOpCommand())
        {
            Title = _settingsManager.Text(LocalizedTextKey.EmptyResultTitle),
            Subtitle = _settingsManager.Text(LocalizedTextKey.EmptyResultSubtitle),
            Icon = Icons.LinkSearchExtIcon,
        };
    }

    public IReadOnlyList<IListItem> CreateErrorItems(LinkwardenSearchError error)
    {
        var title = error.StatusCode.HasValue
            ? _settingsManager.Format(error.MessageKey, error.StatusCode.Value)
            : _settingsManager.Text(error.MessageKey);

        return new[]
        {
            new ListItem(new NoOpCommand())
            {
                Title = title,
                Subtitle = _settingsManager.Text(LocalizedTextKey.RetryOrCheckSettings),
                Icon = Icons.LinkSearchExtIcon,
            },
        };
    }

    public IReadOnlyList<IListItem> CreateResultItems(IReadOnlyList<LinkwardenLink> links)
    {
        var items = new List<IListItem>();
        var maxResults = _settingsManager.MaxResults;
        foreach (var link in links)
        {
            if (items.Count >= maxResults)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(link.Url))
            {
                continue;
            }

            try
            {
                var collection = string.IsNullOrWhiteSpace(link.Collection) ? string.Empty : $" [{link.Collection}]";
                var tags = link.Tags.Count == 0 ? string.Empty : $" #{_settingsManager.Text(LocalizedTextKey.TagLabel)}: {string.Join(", ", link.Tags)}";
                items.Add(new ListItem(new OpenUrlCommand(link.Url, _settingsManager))
                {
                    Title = $"{link.Name}{collection}",
                    Subtitle = $"{link.Description}{tags}",
                    Icon = Icons.LinkSearchExtIcon,
                });
            }
            catch (ArgumentException ex)
            {
                Log.Debug($"Skipped invalid URL '{link.Url}': {ex.Message}");
            }
        }

        if (items.Count == 0)
        {
            items.Add(CreateEmptyResultItem());
        }

        return items;
    }
}
```

- [ ] **Step 2: Update OpenUrlCommand for localized command name**

In `LinkSearch/Pages/LinkSearchPage.cs`, replace the `OpenUrlCommand` constructors and `Name` property with this shape:

```csharp
        private readonly SettingsManager _settingsManager;

        public OpenUrlCommand(string url, SettingsManager settingsManager)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL cannot be empty", nameof(url));
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !AllowedSchemes.Contains(uri.Scheme))
            {
                throw new ArgumentException($"Invalid URL: {url}", nameof(url));
            }

            _url = url;
        }

        public override string Name => _settingsManager.Text(LocalizedTextKey.OpenLinkCommandName);
```

Delete the overload `OpenUrlCommand(string url, bool triggerPropertyChange = false)` unless a build error shows the toolkit still needs it.

- [ ] **Step 3: Build after presenter**

Run:

```powershell
dotnet build "LinkSearch.sln" -p:Platform=x64
```

Expected: build may fail only where old `new OpenUrlCommand(url)` call sites remain. Those call sites are removed in Task 9.

## Task 9: Shrink LinkSearchPage and Wire Services

**Files:**

- Modify: `LinkSearch/Pages/LinkSearchPage.cs`
- Modify: `LinkSearch/LinkSearchCommandsProvider.cs`

- [ ] **Step 1: Update LinkSearchPage fields and constructor**

In `LinkSearch/Pages/LinkSearchPage.cs`, add usings:

```csharp
using LinkSearch.Localization;
using LinkSearch.Models;
using LinkSearch.Presenters;
```

Replace service fields with:

```csharp
        private readonly SettingsManager _settingsManager;
        private readonly LinkwardenService _linkwardenService;
        private readonly RerankService _rerankService;
        private readonly RerankConnectionTestService _rerankConnectionTestService;
        private readonly SearchResultPresenter _presenter;
```

Replace constructors with:

```csharp
        public LinkSearchPage(SettingsManager settingsManager, LinkwardenService linkwardenService, RerankService rerankService, RerankConnectionTestService rerankConnectionTestService, SearchResultPresenter presenter)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _linkwardenService = linkwardenService ?? throw new ArgumentNullException(nameof(linkwardenService));
            _rerankService = rerankService ?? throw new ArgumentNullException(nameof(rerankService));
            _rerankConnectionTestService = rerankConnectionTestService ?? throw new ArgumentNullException(nameof(rerankConnectionTestService));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));

            Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
            Title = "LinkSearch";
            Name = _settingsManager.Text(LocalizedTextKey.PageName);
            PlaceholderText = _settingsManager.Text(LocalizedTextKey.SearchPlaceholder);
            EmptyContent = _presenter.CreateEmptyResultItem();

            _settingsManager.Settings.SettingsChanged += OnSettingsChanged;
            _syncContext = SynchronizationContext.Current;
        }
```

- [ ] **Step 2: Replace GetItemsAsync body**

Replace `GetItemsAsync` with:

```csharp
        private async Task<List<IListItem>> GetItemsAsync(string query, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return new List<IListItem> { _presenter.CreateEmptyQueryItem() };
                }

                var searchResult = await _linkwardenService.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                if (searchResult.Error is not null)
                {
                    return new List<IListItem>(_presenter.CreateErrorItems(searchResult.Error));
                }

                var links = searchResult.Links;
                if (_settingsManager.EnableRerank)
                {
                    links = await _rerankService.RerankLinksAsync(query, links, cancellationToken).ConfigureAwait(false);
                }

                return new List<IListItem>(_presenter.CreateResultItems(links));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Debug($"Search failed: {ex.Message}");
                var error = new LinkwardenSearchError(LinkwardenSearchErrorKind.Unexpected, LocalizedTextKey.ApiCallException, ex.Message);
                return new List<IListItem>(_presenter.CreateErrorItems(error));
            }
        }
```

- [ ] **Step 3: Localize connection test result**

In `TestRerankConnectionAsync`, replace returned strings with:

```csharp
                if (testResult.IsSuccess)
                {
                    return _settingsManager.Format(LocalizedTextKey.RerankConnectionSuccess, testResult.ResponseTimeMs);
                }

                return _settingsManager.Format(LocalizedTextKey.RerankConnectionFailed, testResult.ErrorType, testResult.ErrorMessage);
```

In the catch block, return:

```csharp
                return _settingsManager.Format(LocalizedTextKey.RerankConnectionException, ex.Message);
```

- [ ] **Step 4: Remove old parser/model helpers from page**

Delete these from `LinkSearchPage.cs` after the new build compiles without references:

- `LinkResult` class.
- `Tag` class.
- `Collection` class.
- `GetTagsArray` method.
- `GetTagsStringFromArray` method.
- Old Linkwarden HTTP/JSON parsing code inside the previous `GetItemsAsync`.

- [ ] **Step 5: Wire dependencies in provider**

In `LinkSearch/LinkSearchCommandsProvider.cs`, add `using LinkSearch.Presenters;` and fields:

```csharp
    private readonly SearchCache _searchCache;
    private readonly LinkwardenService _linkwardenService;
    private readonly SearchResultPresenter _presenter;
```

Replace service creation in the constructor with:

```csharp
        _searchCache = new SearchCache();
        _rerankService = new RerankService(_settingsManager);
        _rerankConnectionTestService = new RerankConnectionTestService(_settingsManager);
        _linkwardenService = new LinkwardenService(_settingsManager, _searchCache);
        _presenter = new SearchResultPresenter(_settingsManager);
        _page = new LinkSearchPage(_settingsManager, _linkwardenService, _rerankService, _rerankConnectionTestService, _presenter);
```

In the settings changed handler or provider constructor, clear cache when settings change:

```csharp
        _settingsManager.Settings.SettingsChanged += (_, _) => _searchCache.Clear();
```

- [ ] **Step 6: Fix disposal ownership**

In `LinkSearchPage.Dispose`, remove calls that dispose `_rerankService` and `_rerankConnectionTestService`. The provider owns and disposes shared services.

In `LinkSearchCommandsProvider.Dispose`, keep disposing `_page`, `_rerankService`, `_rerankConnectionTestService`, and `_disposeCts`. `SearchCache` does not need disposal.

- [ ] **Step 7: Build after wiring**

Run:

```powershell
dotnet build "LinkSearch.sln" -p:Platform=x64
```

Expected: build succeeds. If CA1852 warnings remain, Task 10 handles them.

## Task 10: Clean Warnings and Obsolete Runtime Text

**Files:**

- Modify: `LinkSearch/Pages/LinkSearchPage.cs`
- Modify: `LinkSearch/Services/RerankService.cs`
- Modify: `LinkSearch/Services/RerankConnectionTestService.cs`
- Modify: `LinkSearch/Models/RerankRequest.cs`
- Modify: `LinkSearch/Models/RerankResponse.cs`
- Modify: `LinkSearch/Models/RerankResult.cs`
- Modify: `LinkSearch/Models/RerankConnectionTestResult.cs`
- Modify: `README.md`
- Modify: `README.zh.md`

- [ ] **Step 1: Seal internal classes flagged by CA1852**

Change these class declarations if they still exist and have no subclasses:

```csharp
internal sealed partial class OpenUrlCommand : InvokableCommand
internal sealed partial class RerankService : IDisposable
internal sealed partial class RerankConnectionTestService : IDisposable
internal sealed class RerankRequest
internal sealed class RerankResponse
internal sealed class RerankResult
internal sealed class RerankConnectionTestResult
internal sealed class UsageInfo
```

If `LinkResult`, `Tag`, and `Collection` still exist in `LinkSearchPage.cs`, delete them because `LinkwardenLink` replaces them.

- [ ] **Step 2: Remove obsolete config file prompt from runtime strings**

Ensure no code path still shows `LinkSearch/config.json`. The missing token prompt should only show settings and environment variable guidance:

```csharp
new ListItem(new NoOpCommand()) { Title = _settingsManager.Text(LocalizedTextKey.MissingTokenTitle), Icon = Icons.LinkSearchExtIcon }
new ListItem(new NoOpCommand()) { Title = _settingsManager.Text(LocalizedTextKey.ConfigureApiKeyTitle), Icon = Icons.LinkSearchExtIcon }
new ListItem(new NoOpCommand()) { Title = _settingsManager.Text(LocalizedTextKey.EnvTokenHintTitle), Icon = Icons.LinkSearchExtIcon }
```

- [ ] **Step 3: Update README usage wording if needed**

If README files mention only Chinese settings or old config-file behavior, update them to say the extension supports Auto, Simplified Chinese, and English in the settings page. Keep the existing English/Chinese split README structure.

- [ ] **Step 4: Build and confirm warnings are reduced**

Run:

```powershell
dotnet build "LinkSearch.sln" -p:Platform=x64
```

Expected: build succeeds. CA1852 warnings for deleted or sealed classes should be gone.

## Task 11: Full Verification

**Files:**

- Verify: entire solution

- [ ] **Step 1: Run all tests**

Run:

```powershell
dotnet test "LinkSearch.sln" -p:Platform=x64
```

Expected: all tests pass.

- [ ] **Step 2: Run full build**

Run:

```powershell
dotnet build "LinkSearch.sln" -p:Platform=x64
```

Expected: build succeeds with 0 errors.

- [ ] **Step 3: Search for remaining hard-coded user-facing Chinese strings**

Run:

```powershell
rg "请输入|未找到|请检查|连接成功|连接失败|启用 Rerank|最大检索|搜索延迟|打开链接|标签" LinkSearch -g "*.cs"
```

Expected: matches should be in `LinkSearch/Localization/LocalizedStrings.cs` only, plus comments or logs that are not user-facing.

- [ ] **Step 4: Search for obsolete config prompt**

Run:

```powershell
rg "config\.json|LinkSearch/config" .
```

Expected: no runtime prompt references remain. README references should only remain if they are accurate.

- [ ] **Step 5: Check git status**

Run:

```powershell
git status --short
```

Expected: changed files match this plan. Do not commit unless the user asks.

## Self-Review

Spec coverage:

- Bilingual settings and prompts are covered by Tasks 2, 3, 8, and 9.
- Auto, Chinese, and English language behavior is covered by Tasks 2 and 3.
- Stable CmdPal registration and provider entry are preserved by Tasks 9 and 11.
- Linkwarden parsing/search extraction is covered by Tasks 4, 6, and 9.
- Rerank boundary cleanup and fallback are covered by Task 7.
- In-memory cache is covered by Task 5 and wired by Tasks 6 and 9.
- Automated tests are covered by Tasks 1 through 7 and full verification in Task 11.
- CA1852 warnings and disposal ownership are covered by Tasks 9 and 10.

Placeholder scan:

- No deferred-work markers or intentionally empty implementation steps are present.
- Every code-changing task includes concrete file paths, code snippets, and verification commands.

Type consistency:

- `LanguageMode`, `UiLanguage`, `LocalizedTextKey`, and `LocalizedStrings` names are consistent across tests, settings, and presenter tasks.
- `LinkwardenLink`, `LinkwardenSearchError`, and `LinkwardenSearchResult` names are consistent across parser, service, rerank, presenter, and tests.
- `SearchCache` method names are consistent across tests and service wiring.
