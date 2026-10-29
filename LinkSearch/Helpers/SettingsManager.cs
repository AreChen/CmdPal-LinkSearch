// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LinkSearch.Localization;
using LinkSearch.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace LinkSearch.Helpers;

public class SettingsManager : JsonSettingsManager
{
    private static readonly string _namespace = "linkSearch";
    private const string DefaultOpenRouterUrl = "https://openrouter.ai/api/alpha/decisions";
    private const string DefaultOpenAiUrl = "https://api.openai.com/v1/decisions";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private readonly Func<CultureInfo> _cultureProvider;

    private readonly ChoiceSetSetting _language = new(
        Namespaced(nameof(LanguageMode)),
        new List<ChoiceSetSetting.Choice>
        {
            new("Auto", "auto"),
            new("Chinese", "zh-CN"),
            new("English", "en-US"),
        })
    {
        Value = "auto",
    };

    private readonly TextSetting _linkwardenBaseUrl = new(
        Namespaced(nameof(LinkwardenBaseUrl)),
        "Linkwarden Base URL",
        "Linkwarden server URL",
        string.Empty);

    private readonly TextSetting _linkwardenApiKey = new(
        Namespaced(nameof(LinkwardenApiKey)),
        "Linkwarden API Key",
        "Linkwarden API access key",
        string.Empty);

    private readonly ToggleSetting _enableRerank = new(
        Namespaced(nameof(EnableRerank)),
        "Enable Rerank",
        "Reorder search results using rerank",
        false);

    private readonly ChoiceSetSetting _rankingMode = new(
        Namespaced(nameof(RankingMode)),
        new List<ChoiceSetSetting.Choice>
        {
            new("Rerank API", "rerank"),
            new("Decision model: per item (Noul)", "decision-noul"),
            new("Decision model: candidate choice (Choice)", "decision-choice"),
        })
    {
        Value = "rerank",
    };

    private readonly TextSetting _rerankApiUrl = new(
        Namespaced(nameof(RerankApiUrl)),
        "Rerank API URL",
        "Rerank API URL",
        "https://api.siliconflow.cn/v1/rerank");

    private readonly TextSetting _rerankApiKey = new(
        Namespaced(nameof(RerankApiKey)),
        "Rerank API Key",
        "Rerank API access key",
        string.Empty);

    private readonly TextSetting _rerankModelName = new(
        Namespaced(nameof(RerankModelName)),
        "Rerank Model Name",
        "Model name used for rerank",
        "BAAI/bge-reranker-v2-m3");

    private readonly TextSetting _decisionApiUrl = new(
        Namespaced(nameof(DecisionApiUrl)), "Decision API URL", "System One endpoint URL", string.Empty);

    private readonly TextSetting _decisionApiKey = new(
        Namespaced(nameof(DecisionApiKey)), "Decision API Key", "Decision service API key", string.Empty);

    private readonly TextSetting _decisionModelName = new(
        Namespaced(nameof(DecisionModelName)), "Decision Model Name", "Decision model name", string.Empty);

    private readonly ChoiceSetSetting _decisionProvider = new(
        Namespaced(nameof(DecisionProvider)),
        new List<ChoiceSetSetting.Choice>
        {
            new("Custom System One", "system-one"),
            new("OpenRouter", "openrouter"),
            new("OpenAI", "openai"),
        });

    private readonly TextSetting _openRouterApiUrl = new(
        Namespaced(nameof(OpenRouterApiUrl)), "OpenRouter Decisions URL", "Full OpenRouter Decisions endpoint URL", DefaultOpenRouterUrl);
    private readonly TextSetting _openRouterApiKey = new(
        Namespaced(nameof(OpenRouterApiKey)), "OpenRouter API Key", "OpenRouter API key", string.Empty);
    private readonly TextSetting _openRouterModelName = new(
        Namespaced(nameof(OpenRouterModelName)), "OpenRouter Model", "OpenRouter decision model", "typesafe/jev-1.14");
    private readonly TextSetting _openAiApiUrl = new(
        Namespaced(nameof(OpenAiApiUrl)), "OpenAI Decisions URL", "Full OpenAI Decisions endpoint URL", DefaultOpenAiUrl);
    private readonly TextSetting _openAiApiKey = new(
        Namespaced(nameof(OpenAiApiKey)), "OpenAI API Key", "OpenAI API key", string.Empty);
    private readonly TextSetting _openAiModelName = new(
        Namespaced(nameof(OpenAiModelName)), "OpenAI Model", "OpenAI decision model", "gpt-6-luna");

    private readonly TextSetting _searchDelayMilliseconds = new(
        Namespaced(nameof(SearchDelayMilliseconds)),
        "Search Delay (milliseconds)",
        "Delay after search input to reduce unnecessary requests (300-2000 ms)",
        "600");

    private readonly TextSetting _maxResults = new(
        Namespaced(nameof(MaxResults)),
        "Maximum Search Results",
        "Maximum number of search results (1-200)",
        "50");

    public string LinkwardenBaseUrl
    {
        get
        {
            Log.Debug("开始获取LinkwardenBaseUrl");
            
            // 实现多层次配置优先级：插件界面设置 > 环境变量 > 默认值
            var baseUrl = _linkwardenBaseUrl.Value;
            Log.Debug($"插件界面设置的Base URL: {baseUrl}");
            
            // 如果插件界面设置不为空，则直接返回
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                Log.Debug("使用插件界面设置的Base URL");
                return ValidateAndFormatUrl(baseUrl);
            }
            
            // 尝试从环境变量读取
            baseUrl = Environment.GetEnvironmentVariable("LINKWARDEN_BASE_URL");
            Log.Debug($"环境变量中的Base URL: {baseUrl}");
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                Log.Debug("使用环境变量中的Base URL");
                return ValidateAndFormatUrl(baseUrl);
            }
            
            // 如果以上方式都未获取到有效的地址，则回退到使用默认的硬编码地址
            Log.Debug("使用默认的Base URL");
            return ValidateAndFormatUrl("https://cloud.linkwarden.app");
        }
    }

    public string LinkwardenApiKey
    {
        get
        {
            Log.Debug("开始获取LinkwardenApiKey");
            
            // 实现多层次配置优先级：插件界面设置 > 环境变量 > 默认值
            var apiKey = _linkwardenApiKey.Value;
            Log.Debug($"插件界面设置的API Key: {(string.IsNullOrEmpty(apiKey) ? "未设置" : "已设置")}");
            
            // 如果插件界面设置不为空，则直接返回
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                Log.Debug("使用插件界面设置的API Key");
                return apiKey;
            }
            
            // 尝试从环境变量读取
            apiKey = Environment.GetEnvironmentVariable("LINKWARDEN_API_KEY");
            Log.Debug($"环境变量中的API Key: {(string.IsNullOrEmpty(apiKey) ? "未设置" : "已设置")}");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                Log.Debug("使用环境变量中的API Key");
                return apiKey;
            }
            
            // 如果以上方式都未获取到有效的API Key，则返回空字符串
            Log.Debug("未获取到有效的API Key，返回空字符串");
            return string.Empty;
        }
    }

    public bool EnableRerank
    {
        get
        {
            Log.Debug("开始获取EnableRerank");
            
            // 实现多层次配置优先级：插件界面设置 > 环境变量 > 默认值
            var enableRerank = _enableRerank.Value;
            Log.Debug($"插件界面设置的EnableRerank: {enableRerank}");
            
            // 如果插件界面设置不为空，则直接返回
            if (enableRerank)
            {
                Log.Debug("使用插件界面设置的EnableRerank: true");
                return true;
            }
            
            // 尝试从环境变量读取
            var envValue = Environment.GetEnvironmentVariable("LINKSEARCH_ENABLE_RERANK");
            Log.Debug($"环境变量中的EnableRerank: {envValue}");
            if (!string.IsNullOrWhiteSpace(envValue) && bool.TryParse(envValue, out var envEnableRerank))
            {
                Log.Debug("使用环境变量中的EnableRerank");
                return envEnableRerank;
            }
            
            // 如果以上方式都未获取到有效的设置，则返回默认值false
            Log.Debug("使用默认的EnableRerank: false");
            return false;
        }
    }

    internal RankingMode RankingMode => _rankingMode.Value switch
    {
        "decision-noul" => RankingMode.DecisionNoul,
        "decision-choice" => RankingMode.DecisionChoice,
        _ => RankingMode.Rerank,
    };

    internal DecisionProvider DecisionProvider => _decisionProvider.Value switch
    {
        "openrouter" => DecisionProvider.OpenRouter,
        "openai" => DecisionProvider.OpenAi,
        _ => DecisionProvider.SystemOne,
    };

    internal string OpenRouterApiUrl => ValidateAndFormatUrl(
        string.IsNullOrWhiteSpace(_openRouterApiUrl.Value) ? DefaultOpenRouterUrl : _openRouterApiUrl.Value);

    internal string OpenAiApiUrl => ValidateAndFormatUrl(
        string.IsNullOrWhiteSpace(_openAiApiUrl.Value) ? DefaultOpenAiUrl : _openAiApiUrl.Value);

    internal string OpenRouterApiKey => !string.IsNullOrWhiteSpace(_openRouterApiKey.Value)
        ? _openRouterApiKey.Value
        : Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? string.Empty;

    internal string OpenRouterModelName => !string.IsNullOrWhiteSpace(_openRouterModelName.Value)
        ? _openRouterModelName.Value
        : Environment.GetEnvironmentVariable("LINKSEARCH_OPENROUTER_MODEL_NAME") ?? string.Empty;

    internal string OpenAiApiKey => !string.IsNullOrWhiteSpace(_openAiApiKey.Value)
        ? _openAiApiKey.Value
        : Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;

    internal string OpenAiModelName => !string.IsNullOrWhiteSpace(_openAiModelName.Value)
        ? _openAiModelName.Value
        : Environment.GetEnvironmentVariable("LINKSEARCH_OPENAI_MODEL_NAME") ?? string.Empty;

    internal string DecisionApiUrl => GetDecisionApiUrl(DecisionProvider);

    internal string GetDecisionApiUrl(DecisionProvider provider)
    {
        if (provider == DecisionProvider.OpenRouter)
        {
            return OpenRouterApiUrl;
        }

        if (provider == DecisionProvider.OpenAi)
        {
            return OpenAiApiUrl;
        }

        var value = _decisionApiUrl.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            value = Environment.GetEnvironmentVariable("LINKSEARCH_DECISION_API_URL");
        }

        return string.IsNullOrWhiteSpace(value) ? string.Empty : ValidateAndFormatUrl(value);
    }

    internal string DecisionApiKey => GetDecisionApiKey(DecisionProvider);

    internal string GetDecisionApiKey(DecisionProvider provider) => provider switch
    {
        DecisionProvider.OpenRouter => OpenRouterApiKey,
        DecisionProvider.OpenAi => OpenAiApiKey,
        _ => !string.IsNullOrWhiteSpace(_decisionApiKey.Value)
            ? _decisionApiKey.Value
            : Environment.GetEnvironmentVariable("LINKSEARCH_DECISION_API_KEY") ?? string.Empty,
    };

    internal string DecisionModelName => GetDecisionModelName(DecisionProvider);

    internal string GetDecisionModelName(DecisionProvider provider) => provider switch
    {
        DecisionProvider.OpenRouter => OpenRouterModelName,
        DecisionProvider.OpenAi => OpenAiModelName,
        _ => !string.IsNullOrWhiteSpace(_decisionModelName.Value)
            ? _decisionModelName.Value
            : Environment.GetEnvironmentVariable("LINKSEARCH_DECISION_MODEL_NAME") ?? string.Empty,
    };

    public string RerankApiUrl
    {
        get
        {
            Log.Debug("开始获取RerankApiUrl");
            
            // 实现多层次配置优先级：插件界面设置 > 环境变量 > 默认值
            var apiUrl = _rerankApiUrl.Value;
            Log.Debug($"插件界面设置的RerankApiUrl: {apiUrl}");
            
            // 如果插件界面设置不为空，则直接返回
            if (!string.IsNullOrWhiteSpace(apiUrl))
            {
                Log.Debug("使用插件界面设置的RerankApiUrl");
                return ValidateAndFormatUrl(apiUrl);
            }
            
            // 尝试从环境变量读取
            apiUrl = Environment.GetEnvironmentVariable("LINKSEARCH_RERANK_API_URL");
            Log.Debug($"环境变量中的RerankApiUrl: {apiUrl}");
            if (!string.IsNullOrWhiteSpace(apiUrl))
            {
                Log.Debug("使用环境变量中的RerankApiUrl");
                return ValidateAndFormatUrl(apiUrl);
            }
            
            // 如果以上方式都未获取到有效的地址，则回退到使用默认的硬编码地址
            Log.Debug("使用默认的RerankApiUrl");
            return ValidateAndFormatUrl("https://api.siliconflow.cn/v1/rerank");
        }
    }

    public string RerankApiKey
    {
        get
        {
            Log.Debug("开始获取RerankApiKey");
            
            // 实现多层次配置优先级：插件界面设置 > 环境变量 > 默认值
            var apiKey = _rerankApiKey.Value;
            Log.Debug($"插件界面设置的RerankApiKey: {(string.IsNullOrEmpty(apiKey) ? "未设置" : "已设置")}");
            
            // 如果插件界面设置不为空，则直接返回
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                Log.Debug("使用插件界面设置的RerankApiKey");
                return apiKey;
            }
            
            // 尝试从环境变量读取
            apiKey = Environment.GetEnvironmentVariable("LINKSEARCH_RERANK_API_KEY");
            Log.Debug($"环境变量中的RerankApiKey: {(string.IsNullOrEmpty(apiKey) ? "未设置" : "已设置")}");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                Log.Debug("使用环境变量中的RerankApiKey");
                return apiKey;
            }
            
            // 如果以上方式都未获取到有效的API Key，则返回空字符串
            Log.Debug("未获取到有效的RerankApiKey，返回空字符串");
            return string.Empty;
        }
    }

    public string RerankModelName
    {
        get
        {
            Log.Debug("开始获取RerankModelName");
            
            // 实现多层次配置优先级：插件界面设置 > 环境变量 > 默认值
            var modelName = _rerankModelName.Value;
            Log.Debug($"插件界面设置的RerankModelName: {modelName}");
            
            // 如果插件界面设置不为空，则直接返回
            if (!string.IsNullOrWhiteSpace(modelName))
            {
                Log.Debug("使用插件界面设置的RerankModelName");
                return modelName;
            }
            
            // 尝试从环境变量读取
            modelName = Environment.GetEnvironmentVariable("LINKSEARCH_RERANK_MODEL_NAME");
            Log.Debug($"环境变量中的RerankModelName: {modelName}");
            if (!string.IsNullOrWhiteSpace(modelName))
            {
                Log.Debug("使用环境变量中的RerankModelName");
                return modelName;
            }
            
            // 如果以上方式都未获取到有效的模型名称，则回退到使用默认的硬编码值
            Log.Debug("使用默认的RerankModelName");
            return "BAAI/bge-reranker-v2-m3";
        }
    }

    public int SearchDelayMilliseconds
    {
        get
        {
            Log.Debug("开始获取SearchDelayMilliseconds");
            
            // 实现多层次配置优先级：插件界面设置 > 环境变量 > 默认值
            var delayText = _searchDelayMilliseconds.Value;
            Log.Debug($"插件界面设置的SearchDelayMilliseconds: {delayText}");
            
            int delay = 600; // 默认值，从500ms增加到600ms
            
            // 如果插件界面设置不为空，则尝试解析
            if (!string.IsNullOrWhiteSpace(delayText) && int.TryParse(delayText, out var parsedDelay))
            {
                // 验证范围，最小值从100ms增加到300ms
                if (parsedDelay >= 300 && parsedDelay <= 2000)
                {
                    Log.Debug("使用插件界面设置的SearchDelayMilliseconds");
                    return parsedDelay;
                }
                else
                {
                    Log.Debug($"插件界面设置的SearchDelayMilliseconds超出范围: {parsedDelay}");
                    // 如果设置值小于最小值，则使用最小值
                    if (parsedDelay < 300)
                    {
                        Log.Debug($"插件界面设置的SearchDelayMilliseconds小于最小值，使用最小值: 300");
                        return 300;
                    }
                    // 如果设置值大于最大值，则使用最大值
                    if (parsedDelay > 2000)
                    {
                        Log.Debug($"插件界面设置的SearchDelayMilliseconds大于最大值，使用最大值: 2000");
                        return 2000;
                    }
                }
            }
            
            // 尝试从环境变量读取
            var envDelay = Environment.GetEnvironmentVariable("LINKSEARCH_SEARCH_DELAY_MS");
            Log.Debug($"环境变量中的SearchDelayMilliseconds: {envDelay}");
            if (!string.IsNullOrWhiteSpace(envDelay) && int.TryParse(envDelay, out var parsedEnvDelay))
            {
                // 验证范围，最小值从100ms增加到300ms
                if (parsedEnvDelay >= 300 && parsedEnvDelay <= 2000)
                {
                    Log.Debug("使用环境变量中的SearchDelayMilliseconds");
                    return parsedEnvDelay;
                }
                else
                {
                    Log.Debug($"环境变量中的SearchDelayMilliseconds超出范围: {parsedEnvDelay}");
                    // 如果设置值小于最小值，则使用最小值
                    if (parsedEnvDelay < 300)
                    {
                        Log.Debug($"环境变量中的SearchDelayMilliseconds小于最小值，使用最小值: 300");
                        return 300;
                    }
                    // 如果设置值大于最大值，则使用最大值
                    if (parsedEnvDelay > 2000)
                    {
                        Log.Debug($"环境变量中的SearchDelayMilliseconds大于最大值，使用最大值: 2000");
                        return 2000;
                    }
                }
            }
            
            // 如果以上方式都未获取到有效的延迟时间，则返回默认值
            Log.Debug("使用默认的SearchDelayMilliseconds: 600");
            return delay;
        }
    }

    public int MaxResults
    {
        get
        {
            Log.Debug("开始获取MaxResults");
            
            // 实现多层次配置优先级：插件界面设置 > 环境变量 > 默认值
            var maxResultsText = _maxResults.Value;
            Log.Debug($"插件界面设置的MaxResults: {maxResultsText}");
            
            int maxResults = 50; // 默认值
            
            // 如果插件界面设置不为空，则尝试解析
            if (!string.IsNullOrWhiteSpace(maxResultsText) && int.TryParse(maxResultsText, out var parsedMaxResults))
            {
                // 验证范围，确保在1-200之间
                if (parsedMaxResults >= 1 && parsedMaxResults <= 200)
                {
                    Log.Debug("使用插件界面设置的MaxResults");
                    return parsedMaxResults;
                }
                else
                {
                    Log.Debug($"插件界面设置的MaxResults超出范围: {parsedMaxResults}");
                    // 如果设置值小于最小值，则使用最小值
                    if (parsedMaxResults < 1)
                    {
                        Log.Debug($"插件界面设置的MaxResults小于最小值，使用最小值: 1");
                        return 1;
                    }
                    // 如果设置值大于最大值，则使用最大值
                    if (parsedMaxResults > 200)
                    {
                        Log.Debug($"插件界面设置的MaxResults大于最大值，使用最大值: 200");
                        return 200;
                    }
                }
            }
            
            // 尝试从环境变量读取
            var envMaxResults = Environment.GetEnvironmentVariable("LINKSEARCH_MAX_RESULTS");
            Log.Debug($"环境变量中的MaxResults: {envMaxResults}");
            if (!string.IsNullOrWhiteSpace(envMaxResults) && int.TryParse(envMaxResults, out var parsedEnvMaxResults))
            {
                // 验证范围，确保在1-200之间
                if (parsedEnvMaxResults >= 1 && parsedEnvMaxResults <= 200)
                {
                    Log.Debug("使用环境变量中的MaxResults");
                    return parsedEnvMaxResults;
                }
                else
                {
                    Log.Debug($"环境变量中的MaxResults超出范围: {parsedEnvMaxResults}");
                    // 如果设置值小于最小值，则使用最小值
                    if (parsedEnvMaxResults < 1)
                    {
                        Log.Debug($"环境变量中的MaxResults小于最小值，使用最小值: 1");
                        return 1;
                    }
                    // 如果设置值大于最大值，则使用最大值
                    if (parsedEnvMaxResults > 200)
                    {
                        Log.Debug($"环境变量中的MaxResults大于最大值，使用最大值: 200");
                        return 200;
                    }
                }
            }
            
            // 如果以上方式都未获取到有效的最大结果数量，则返回默认值
            Log.Debug("使用默认的MaxResults: 50");
            return maxResults;
        }
    }

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("LinkSearch");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
        : this(null, null)
    {
    }

    internal SettingsManager(string? settingsFilePath, Func<CultureInfo>? cultureProvider)
    {
        FilePath = settingsFilePath ?? SettingsJsonPath();
        _cultureProvider = cultureProvider ?? (() => CultureInfo.CurrentUICulture);

        Settings.Add(_language);
        Settings.Add(_linkwardenBaseUrl);
        Settings.Add(_linkwardenApiKey);
        Settings.Add(_enableRerank);
        Settings.Add(_rankingMode);
        Settings.Add(_decisionProvider);
        Settings.Add(_rerankApiUrl);
        Settings.Add(_rerankApiKey);
        Settings.Add(_rerankModelName);
        Settings.Add(_decisionApiUrl);
        Settings.Add(_decisionApiKey);
        Settings.Add(_decisionModelName);
        Settings.Add(_openRouterApiUrl);
        Settings.Add(_openRouterApiKey);
        Settings.Add(_openRouterModelName);
        Settings.Add(_openAiApiUrl);
        Settings.Add(_openAiApiKey);
        Settings.Add(_openAiModelName);
        Settings.Add(_searchDelayMilliseconds);
        Settings.Add(_maxResults);

        // Load settings from file upon initialization
        LoadSettings();
        MigrateLegacyChoiceValues();
        ApplyLocalizedSettingText();

        Settings.SettingsChanged += (s, a) =>
        {
            ApplyLocalizedSettingText();
            this.SaveSettings();
        };
    }

    internal LanguageMode LanguageMode => LocalizedStrings.ParseLanguageMode(_language.Value);

    internal UiLanguage CurrentUiLanguage => LocalizedStrings.ResolveUiLanguage(LanguageMode, _cultureProvider());

    internal string Text(LocalizedTextKey key) => LocalizedStrings.Get(key, CurrentUiLanguage);

    internal string Format(LocalizedTextKey key, params object[] args) => LocalizedStrings.Format(key, CurrentUiLanguage, args);

    private void MigrateLegacyChoiceValues()
    {
        var language = _language.Value;
        var rankingMode = _rankingMode.Value;

        _language.Value = language switch
        {
            "Auto" => "auto",
            "Chinese" => "zh-CN",
            "English" => "en-US",
            _ => language,
        };
        _rankingMode.Value = rankingMode switch
        {
            "Rerank API" => "rerank",
            "Decision model: per item (Noul)" => "decision-noul",
            "Decision model: candidate choice (Choice)" => "decision-choice",
            _ => rankingMode,
        };

        if (_language.Value != language || _rankingMode.Value != rankingMode)
        {
            SaveSettings();
        }
    }

    internal void SetForTest(string key, string value)
    {
        if (key == _decisionProvider.Key) { _decisionProvider.Value = value; return; }
        if (key == _openRouterApiUrl.Key) { _openRouterApiUrl.Value = value; return; }
        if (key == _openRouterApiKey.Key) { _openRouterApiKey.Value = value; return; }
        if (key == _openRouterModelName.Key) { _openRouterModelName.Value = value; return; }
        if (key == _openAiApiUrl.Key) { _openAiApiUrl.Value = value; return; }
        if (key == _openAiApiKey.Key) { _openAiApiKey.Value = value; return; }
        if (key == _openAiModelName.Key) { _openAiModelName.Value = value; return; }

        if (key == _rankingMode.Key)
        {
            _rankingMode.Value = value;
            return;
        }

        if (key == _decisionApiUrl.Key)
        {
            _decisionApiUrl.Value = value;
            return;
        }

        if (key == _decisionApiKey.Key)
        {
            _decisionApiKey.Value = value;
            return;
        }

        if (key == _decisionModelName.Key)
        {
            _decisionModelName.Value = value;
            return;
        }

        if (key == _linkwardenBaseUrl.Key)
        {
            _linkwardenBaseUrl.Value = value;
            return;
        }

        if (key == _linkwardenApiKey.Key)
        {
            _linkwardenApiKey.Value = value;
            return;
        }

        if (key == _rerankApiUrl.Key)
        {
            _rerankApiUrl.Value = value;
            return;
        }

        if (key == _rerankApiKey.Key)
        {
            _rerankApiKey.Value = value;
            return;
        }

        if (key == _rerankModelName.Key)
        {
            _rerankModelName.Value = value;
            return;
        }

        if (key == _searchDelayMilliseconds.Key)
        {
            _searchDelayMilliseconds.Value = value;
            return;
        }

        if (key == _maxResults.Key)
        {
            _maxResults.Value = value;
            return;
        }

        if (key == _language.Key)
        {
            _language.Value = value;
            ApplyLocalizedSettingText();
            return;
        }

        throw new InvalidOperationException($"Setting key was not found: {key}");
    }

    internal T GetSettingForTest<T>(string key)
        where T : class
    {
        object? setting = key switch
        {
            _ when key == _language.Key => _language,
            _ when key == _linkwardenBaseUrl.Key => _linkwardenBaseUrl,
            _ when key == _linkwardenApiKey.Key => _linkwardenApiKey,
            _ when key == _enableRerank.Key => _enableRerank,
            _ when key == _rankingMode.Key => _rankingMode,
            _ when key == _rerankApiUrl.Key => _rerankApiUrl,
            _ when key == _rerankApiKey.Key => _rerankApiKey,
            _ when key == _rerankModelName.Key => _rerankModelName,
            _ when key == _decisionApiUrl.Key => _decisionApiUrl,
            _ when key == _decisionApiKey.Key => _decisionApiKey,
            _ when key == _decisionModelName.Key => _decisionModelName,
            _ when key == _decisionProvider.Key => _decisionProvider,
            _ when key == _openRouterApiUrl.Key => _openRouterApiUrl,
            _ when key == _openRouterApiKey.Key => _openRouterApiKey,
            _ when key == _openRouterModelName.Key => _openRouterModelName,
            _ when key == _openAiApiUrl.Key => _openAiApiUrl,
            _ when key == _openAiApiKey.Key => _openAiApiKey,
            _ when key == _openAiModelName.Key => _openAiModelName,
            _ when key == _searchDelayMilliseconds.Key => _searchDelayMilliseconds,
            _ when key == _maxResults.Key => _maxResults,
            _ => null,
        };

        return setting as T ?? throw new InvalidOperationException($"Setting key was not found or has a different type: {key}");
    }

    private void ApplyLocalizedSettingText()
    {
        _language.Label = Text(LocalizedTextKey.LanguageSettingLabel);
        _language.Description = Text(LocalizedTextKey.LanguageSettingDescription);
        _language.Choices[0].Title = Text(LocalizedTextKey.LanguageAutoChoice);
        _language.Choices[1].Title = Text(LocalizedTextKey.LanguageChineseChoice);
        _language.Choices[2].Title = Text(LocalizedTextKey.LanguageEnglishChoice);

        _linkwardenBaseUrl.Label = Text(LocalizedTextKey.LinkwardenBaseUrlLabel);
        _linkwardenBaseUrl.Description = Text(LocalizedTextKey.LinkwardenBaseUrlDescription);
        _linkwardenApiKey.Label = Text(LocalizedTextKey.LinkwardenApiKeyLabel);
        _linkwardenApiKey.Description = Text(LocalizedTextKey.LinkwardenApiKeyDescription);
        _enableRerank.Label = Text(LocalizedTextKey.EnableRerankLabel);
        _enableRerank.Description = Text(LocalizedTextKey.EnableRerankDescription);
        _rankingMode.Label = Text(LocalizedTextKey.RankingModeLabel);
        _rankingMode.Description = Text(LocalizedTextKey.RankingModeDescription);
        _rankingMode.Choices[0].Title = Text(LocalizedTextKey.RankingModeRerank);
        _rankingMode.Choices[1].Title = Text(LocalizedTextKey.RankingModeDecisionNoul);
        _rankingMode.Choices[2].Title = Text(LocalizedTextKey.RankingModeDecisionChoice);
        _decisionProvider.Label = Text(LocalizedTextKey.DecisionProviderLabel);
        _decisionProvider.Description = Text(LocalizedTextKey.DecisionProviderDescription);
        _decisionProvider.Choices[0].Title = Text(LocalizedTextKey.DecisionProviderSystemOne);
        _rerankApiUrl.Label = Text(LocalizedTextKey.RerankApiUrlLabel);
        _rerankApiUrl.Description = Text(LocalizedTextKey.RerankApiUrlDescription);
        _rerankApiKey.Label = Text(LocalizedTextKey.RerankApiKeyLabel);
        _rerankApiKey.Description = Text(LocalizedTextKey.RerankApiKeyDescription);
        _rerankModelName.Label = Text(LocalizedTextKey.RerankModelNameLabel);
        _rerankModelName.Description = Text(LocalizedTextKey.RerankModelNameDescription);
        _decisionApiUrl.Label = Text(LocalizedTextKey.DecisionApiUrlLabel);
        _decisionApiUrl.Description = Text(LocalizedTextKey.DecisionApiUrlDescription);
        _decisionApiKey.Label = Text(LocalizedTextKey.DecisionApiKeyLabel);
        _decisionApiKey.Description = Text(LocalizedTextKey.DecisionApiKeyDescription);
        _decisionModelName.Label = Text(LocalizedTextKey.DecisionModelNameLabel);
        _decisionModelName.Description = Text(LocalizedTextKey.DecisionModelNameDescription);
        _openRouterApiUrl.Label = Text(LocalizedTextKey.OpenRouterApiUrlLabel);
        _openRouterApiUrl.Description = Text(LocalizedTextKey.OpenRouterApiUrlLabel);
        _openRouterApiKey.Label = Text(LocalizedTextKey.OpenRouterApiKeyLabel);
        _openRouterApiKey.Description = Text(LocalizedTextKey.OpenRouterApiKeyLabel);
        _openRouterModelName.Label = Text(LocalizedTextKey.OpenRouterModelLabel);
        _openRouterModelName.Description = Text(LocalizedTextKey.OpenRouterModelLabel);
        _openAiApiUrl.Label = Text(LocalizedTextKey.OpenAiApiUrlLabel);
        _openAiApiUrl.Description = Text(LocalizedTextKey.OpenAiApiUrlLabel);
        _openAiApiKey.Label = Text(LocalizedTextKey.OpenAiApiKeyLabel);
        _openAiApiKey.Description = Text(LocalizedTextKey.OpenAiApiKeyLabel);
        _openAiModelName.Label = Text(LocalizedTextKey.OpenAiModelLabel);
        _openAiModelName.Description = Text(LocalizedTextKey.OpenAiModelLabel);
        _searchDelayMilliseconds.Label = Text(LocalizedTextKey.SearchDelayLabel);
        _searchDelayMilliseconds.Description = Text(LocalizedTextKey.SearchDelayDescription);
        _maxResults.Label = Text(LocalizedTextKey.MaxResultsLabel);
        _maxResults.Description = Text(LocalizedTextKey.MaxResultsDescription);
    }

    /// <summary>
    /// 验证并格式化URL
    /// </summary>
    /// <param name="url">要验证的URL</param>
    /// <returns>格式化后的URL</returns>
    private static string ValidateAndFormatUrl(string url)
    {
        // 调试日志：记录原始URL
        Log.Debug($"验证原始URL: {url}");
        
        if (string.IsNullOrWhiteSpace(url))
        {
            Log.Debug("URL为空或空白");
            return string.Empty;
        }

        // 如果URL不以http://或https://开头，则添加https://
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
            Log.Debug($"添加协议前缀后的URL: {url}");
        }

        // 移除URL末尾的斜杠
        url = url.TrimEnd('/');
        Log.Debug($"移除末尾斜杠后的URL: {url}");

        // 验证URL格式
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            Log.Debug($"URL格式无效，无法创建Uri对象: {url}");
            return string.Empty;
        }

        // 验证URL方案
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            Log.Debug($"URL方案无效: {uri.Scheme}");
            return string.Empty;
        }

        Log.Debug($"URL验证成功: {url}");
        return url;
    }

    /// <summary>
    /// 验证API Key格式
    /// </summary>
    /// <param name="apiKey">要验证的API Key</param>
    /// <returns>是否有效</returns>
    public static bool ValidateApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        // 基本格式验证：API Key通常是一串较长的字符串
        // 这里可以根据实际API Key的格式要求进行调整
        return apiKey.Length >= 10;
    }
}
