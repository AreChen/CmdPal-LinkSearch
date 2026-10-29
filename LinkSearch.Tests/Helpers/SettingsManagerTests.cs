using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using LinkSearch.Helpers;
using LinkSearch.Localization;
using LinkSearch.Models;
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

        Assert.Equal("Language", settings.GetSettingForTest<ChoiceSetSetting>("linkSearch.LanguageMode").Label);
        Assert.Equal("Linkwarden Base URL", settings.GetSettingForTest<TextSetting>("linkSearch.LinkwardenBaseUrl").Label);
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

    [Fact]
    public void Saved_language_mode_overrides_new_manager_culture()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.SetForTest("linkSearch.LanguageMode", "zh-CN");
        settings.SaveSettings();

        var reloaded = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal(UiLanguage.Chinese, reloaded.CurrentUiLanguage);
        Assert.Equal("语言", reloaded.GetSettingForTest<ChoiceSetSetting>("linkSearch.LanguageMode").Label);
    }

    [Fact]
    public void SetForTest_language_mode_updates_localized_setting_text()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("Language", settings.GetSettingForTest<ChoiceSetSetting>("linkSearch.LanguageMode").Label);

        settings.SetForTest("linkSearch.LanguageMode", "zh-CN");

        var language = settings.GetSettingForTest<ChoiceSetSetting>("linkSearch.LanguageMode");
        Assert.Equal("语言", language.Label);
        Assert.Equal("自动", language.Choices[0].Title);
    }

    [Fact]
    public void SetForTest_throws_for_unknown_key()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));

        Assert.Throws<InvalidOperationException>(() => settings.SetForTest("linkSearch.DoesNotExist", "value"));
    }

    [Fact]
    public void Existing_settings_default_to_rerank()
    {
        using var temp = new TempSettingsFile();
        var existing = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        existing.SetForTest("linkSearch.RerankApiKey", "old-key");
        existing.SaveSettings();
        var oldSettings = JsonNode.Parse(File.ReadAllText(temp.Path))!.AsObject();
        Assert.True(oldSettings.Remove("linkSearch.RankingMode"));
        Assert.True(oldSettings.Remove("linkSearch.DecisionApiUrl"));
        Assert.True(oldSettings.Remove("linkSearch.DecisionApiKey"));
        Assert.True(oldSettings.Remove("linkSearch.DecisionModelName"));
        File.WriteAllText(temp.Path, oldSettings.ToJsonString());
        var reloaded = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal(RankingMode.Rerank, reloaded.RankingMode);
        Assert.Equal("old-key", reloaded.RerankApiKey);
    }

    [Fact]
    public void Decision_settings_are_persisted_without_overwriting_rerank_settings()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.SetForTest("linkSearch.RerankApiKey", "rerank-key");
        settings.SetForTest("linkSearch.RankingMode", "decision-choice");
        settings.SetForTest("linkSearch.DecisionApiUrl", "https://decision.example/v1/systemone");
        settings.SetForTest("linkSearch.DecisionApiKey", "decision-key");
        settings.SetForTest("linkSearch.DecisionModelName", "decision-model");
        settings.SaveSettings();

        var reloaded = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal(RankingMode.DecisionChoice, reloaded.RankingMode);
        Assert.Equal("https://decision.example/v1/systemone", reloaded.DecisionApiUrl);
        Assert.Equal("decision-key", reloaded.DecisionApiKey);
        Assert.Equal("decision-model", reloaded.DecisionModelName);
        Assert.Equal("rerank-key", reloaded.RerankApiKey);

        reloaded.SetForTest("linkSearch.RankingMode", "decision-noul");
        Assert.Equal(RankingMode.DecisionNoul, reloaded.RankingMode);
        reloaded.SetForTest("linkSearch.RankingMode", "rerank");
        Assert.Equal(RankingMode.Rerank, reloaded.RankingMode);
        Assert.Equal("decision-key", reloaded.DecisionApiKey);
    }

    [Fact]
    public void Decision_providers_keep_endpoints_and_credentials_separate()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.SetForTest("linkSearch.DecisionApiUrl", "https://custom.example/v1/systemone");
        settings.SetForTest("linkSearch.DecisionApiKey", "custom-key");
        settings.SetForTest("linkSearch.DecisionModelName", "custom-model");
        settings.SetForTest("linkSearch.OpenRouterApiKey", "router-key");
        settings.SetForTest("linkSearch.OpenRouterModelName", "router-model");
        settings.SetForTest("linkSearch.OpenAiApiKey", "openai-key");
        settings.SetForTest("linkSearch.OpenAiModelName", "openai-model");
        settings.SetForTest("linkSearch.DecisionProvider", "openai");
        settings.SaveSettings();

        var reloaded = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal(DecisionProvider.OpenAi, reloaded.DecisionProvider);
        Assert.Equal("https://api.openai.com/v1/decisions", reloaded.DecisionApiUrl);
        Assert.Equal("openai-key", reloaded.DecisionApiKey);
        Assert.Equal("openai-model", reloaded.DecisionModelName);
        reloaded.SetForTest("linkSearch.DecisionProvider", "openrouter");
        Assert.Equal("https://openrouter.ai/api/alpha/decisions", reloaded.DecisionApiUrl);
        Assert.Equal("router-key", reloaded.DecisionApiKey);
        Assert.Equal("router-model", reloaded.DecisionModelName);
        reloaded.SetForTest("linkSearch.DecisionProvider", "system-one");
        Assert.Equal("https://custom.example/v1/systemone", reloaded.DecisionApiUrl);
        Assert.Equal("custom-key", reloaded.DecisionApiKey);
        Assert.Equal("custom-model", reloaded.DecisionModelName);
        Assert.Equal("https://api.openai.com/v1/decisions", reloaded.GetDecisionApiUrl(DecisionProvider.OpenAi));
        Assert.Equal("openai-key", reloaded.GetDecisionApiKey(DecisionProvider.OpenAi));
        Assert.Equal("openai-model", reloaded.GetDecisionModelName(DecisionProvider.OpenAi));

        reloaded.SetForTest("linkSearch.OpenAiApiUrl", "https://gateway.example/v1/decisions");
        reloaded.SetForTest("linkSearch.OpenRouterApiUrl", "https://gateway.example/api/alpha/decisions");
        reloaded.SaveSettings();
        var customEndpoints = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal("https://gateway.example/v1/decisions", customEndpoints.GetDecisionApiUrl(DecisionProvider.OpenAi));
        Assert.Equal("https://gateway.example/api/alpha/decisions", customEndpoints.GetDecisionApiUrl(DecisionProvider.OpenRouter));
        Assert.Equal("https://custom.example/v1/systemone", customEndpoints.GetDecisionApiUrl(DecisionProvider.SystemOne));
        customEndpoints.SetForTest("linkSearch.OpenAiApiUrl", "");
        customEndpoints.SetForTest("linkSearch.OpenRouterApiUrl", "");
        Assert.Equal("https://api.openai.com/v1/decisions", customEndpoints.GetDecisionApiUrl(DecisionProvider.OpenAi));
        Assert.Equal("https://openrouter.ai/api/alpha/decisions", customEndpoints.GetDecisionApiUrl(DecisionProvider.OpenRouter));
    }

    [Fact]
    public void Existing_decision_configuration_defaults_to_custom_system_one()
    {
        using var temp = new TempSettingsFile();
        var old = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        old.SetForTest("linkSearch.RankingMode", "decision-noul");
        old.SetForTest("linkSearch.DecisionApiUrl", "https://custom.example/v1/systemone");
        old.SetForTest("linkSearch.DecisionApiKey", "legacy-key");
        old.SetForTest("linkSearch.DecisionModelName", "legacy-model");
        old.SaveSettings();
        var state = JsonNode.Parse(File.ReadAllText(temp.Path))!.AsObject();
        foreach (var name in new[] { "DecisionProvider", "OpenRouterApiUrl", "OpenRouterApiKey", "OpenRouterModelName", "OpenAiApiUrl", "OpenAiApiKey", "OpenAiModelName" })
        {
            state.Remove($"linkSearch.{name}");
        }
        File.WriteAllText(temp.Path, state.ToJsonString());

        var reloaded = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal(RankingMode.DecisionNoul, reloaded.RankingMode);
        Assert.Equal(DecisionProvider.SystemOne, reloaded.DecisionProvider);
        Assert.Equal("https://custom.example/v1/systemone", reloaded.DecisionApiUrl);
        Assert.Equal("legacy-key", reloaded.DecisionApiKey);
        Assert.Equal("legacy-model", reloaded.DecisionModelName);
    }

    [Fact]
    public void Settings_layout_hides_ranking_until_enabled_and_masks_credentials()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        using var view = new LinkSearchSettingsView(settings);
        var form = Assert.IsAssignableFrom<FormContent>(Assert.Single(view.SettingsPage.GetContent()));
        var card = JsonNode.Parse(form.TemplateJson)!;
        var body = card["body"]!.AsArray();
        Assert.Contains(body, item => item?["text"]?.GetValue<string>() == "Linkwarden Connection");
        Assert.Contains(body, item => item?["text"]?.GetValue<string>() == "Search Result Ranking");
        Assert.Contains(body, item => item?["text"]?.GetValue<string>() == "Search and Interface");
        Assert.Equal("Save", card["actions"]![0]!["title"]!.GetValue<string>());
        Assert.Equal("password", FindField(card, "linkSearch.LinkwardenApiKey")!["style"]!.GetValue<string>());
        Assert.Null(FindField(card, "linkSearch.RerankApiUrl"));
        Assert.Null(FindField(card, "linkSearch.DecisionApiUrl"));
        Assert.Null(FindField(card, "linkSearch.OpenRouterApiUrl"));
        Assert.Null(FindField(card, "linkSearch.OpenAiApiUrl"));
        Assert.Contains("Enable ranking", card.ToJsonString());

        var templateChanges = 0;
        form.PropChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FormContent.TemplateJson)) templateChanges++;
        };
        form.SubmitForm("{\"linkSearch.LinkwardenBaseUrl\":\"https://draft.example\"}", "{\"linksearchAction\":\"toggle\"}");
        Assert.True(templateChanges > 0);
        card = JsonNode.Parse(form.TemplateJson)!;
        Assert.NotNull(FindField(card, "linkSearch.RerankApiUrl"));
        Assert.Equal("password", FindField(card, "linkSearch.RerankApiKey")!["style"]!.GetValue<string>());
        Assert.Null(FindField(card, "linkSearch.OpenAiApiUrl"));
        Assert.Equal("https://draft.example", FindField(card, "linkSearch.LinkwardenBaseUrl")!["value"]!.GetValue<string>());
        Assert.False(settings.EnableRerank);

        form.SubmitForm("{\"linkSearch.RerankApiKey\":\"draft-rerank-key\"}", "{\"linksearchAction\":\"toggle\"}");
        card = JsonNode.Parse(form.TemplateJson)!;
        Assert.Null(FindField(card, "linkSearch.RerankApiKey"));
        form.SubmitForm("{}", "{\"linksearchAction\":\"toggle\"}");
        card = JsonNode.Parse(form.TemplateJson)!;
        Assert.Equal("draft-rerank-key", FindField(card, "linkSearch.RerankApiKey")!["value"]!.GetValue<string>());
    }

    [Fact]
    public void Settings_form_switches_services_in_one_action_and_preserves_unsaved_profiles()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("zh-CN"));
        settings.SetForTest("linkSearch.DecisionApiKey", "existing-custom-key");
        settings.SetForTest("linkSearch.RerankApiKey", "existing-rerank-key");
        using var view = new LinkSearchSettingsView(settings);
        var form = Assert.IsAssignableFrom<FormContent>(Assert.Single(view.SettingsPage.GetContent()));
        form.SubmitForm("{}", "{\"linksearchAction\":\"toggle\"}");
        form.SubmitForm("{}", "{\"linksearchAction\":\"mode\",\"value\":\"decision-noul\"}");
        var card = JsonNode.Parse(form.TemplateJson)!;
        Assert.NotNull(FindField(card, "linkSearch.DecisionApiUrl"));
        Assert.Null(FindField(card, "linkSearch.RerankApiUrl"));

        form.SubmitForm("{}", "{\"linksearchAction\":\"provider\",\"value\":\"openai\"}");
        card = JsonNode.Parse(form.TemplateJson)!;
        Assert.NotNull(FindField(card, "linkSearch.OpenAiApiUrl"));
        Assert.Null(FindField(card, "linkSearch.DecisionApiUrl"));
        Assert.Equal("password", FindField(card, "linkSearch.OpenAiApiKey")!["style"]!.GetValue<string>());
        Assert.Contains("https://developers.openai.com/api/docs/quickstart", card.ToJsonString());
        Assert.Contains("https://developers.openai.com/api/docs/guides/decisions", card.ToJsonString());

        form.SubmitForm("{\"linkSearch.OpenAiApiKey\":\"new-openai-key\"}", "{\"linksearchAction\":\"provider\",\"value\":\"openrouter\"}");
        card = JsonNode.Parse(form.TemplateJson)!;
        Assert.NotNull(FindField(card, "linkSearch.OpenRouterApiUrl"));
        Assert.Null(FindField(card, "linkSearch.OpenAiApiUrl"));
        Assert.Equal("password", FindField(card, "linkSearch.OpenRouterApiKey")!["style"]!.GetValue<string>());
        Assert.Contains("https://openrouter.ai/docs/quickstart", card.ToJsonString());
        Assert.Contains("https://openrouter.ai/docs/api/api-reference/alphadecisions/submit-a-decisions-request", card.ToJsonString());

        form.SubmitForm("{\"linkSearch.OpenRouterApiKey\":\"new-router-key\"}", "{\"linksearchAction\":\"provider\",\"value\":\"openai\"}");
        card = JsonNode.Parse(form.TemplateJson)!;
        Assert.Equal("new-openai-key", FindField(card, "linkSearch.OpenAiApiKey")!["value"]!.GetValue<string>());
        Assert.Equal(DecisionProvider.SystemOne, settings.DecisionProvider);
        form.SubmitForm("{}", "{\"linksearchAction\":\"save\"}");

        var reloaded = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("zh-CN"));
        Assert.Equal(RankingMode.DecisionNoul, reloaded.RankingMode);
        Assert.True(reloaded.EnableRerank);
        Assert.Equal(DecisionProvider.OpenAi, reloaded.DecisionProvider);
        Assert.Equal("new-openai-key", reloaded.DecisionApiKey);
        Assert.Equal("new-router-key", reloaded.GetDecisionApiKey(DecisionProvider.OpenRouter));
        Assert.Equal("existing-rerank-key", reloaded.RerankApiKey);
        reloaded.SetForTest("linkSearch.DecisionProvider", "system-one");
        Assert.Equal("existing-custom-key", reloaded.DecisionApiKey);
        var refreshed = Assert.IsAssignableFrom<FormContent>(Assert.Single(view.SettingsPage.GetContent()));
        card = JsonNode.Parse(refreshed.TemplateJson)!;
        Assert.NotNull(FindField(card, "linkSearch.OpenAiApiUrl"));
        Assert.Null(FindField(card, "linkSearch.DecisionApiUrl"));
        Assert.Equal("保存", card["actions"]![0]!["title"]!.GetValue<string>());
    }

    [Fact]
    public void Settings_selection_buttons_share_the_same_style_and_mark_the_current_choice()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        using var view = new LinkSearchSettingsView(settings);
        var form = Assert.IsAssignableFrom<FormContent>(Assert.Single(view.SettingsPage.GetContent()));

        form.SubmitForm("{}", "{\"linksearchAction\":\"toggle\"}");
        var card = JsonNode.Parse(form.TemplateJson)!;
        AssertSelection(card, "mode", "rerank", "\u2713 Rerank");
        AssertSelection(card, "mode", "decision-noul", "Per item");
        AssertSelection(card, "mode", "decision-choice", "Choice");

        form.SubmitForm("{}", "{\"linksearchAction\":\"mode\",\"value\":\"decision-choice\"}");
        card = JsonNode.Parse(form.TemplateJson)!;
        AssertSelection(card, "mode", "rerank", "Rerank");
        AssertSelection(card, "mode", "decision-choice", "\u2713 Choice");
        AssertSelection(card, "provider", "system-one", "\u2713 Custom");
        AssertSelection(card, "provider", "openrouter", "OpenRouter");
        AssertSelection(card, "provider", "openai", "OpenAI");

        form.SubmitForm("{}", "{\"linksearchAction\":\"provider\",\"value\":\"openrouter\"}");
        card = JsonNode.Parse(form.TemplateJson)!;
        AssertSelection(card, "provider", "system-one", "Custom");
        AssertSelection(card, "provider", "openrouter", "\u2713 OpenRouter");
        AssertSelection(card, "provider", "openai", "OpenAI");
    }

    [Theory]
    [InlineData("zh-CN", "启用重排序", "停用重排序", "重排序模式", "逐项判断", "决策模型服务", "自建", "官方配置教程", "决策 API 文档", "保存")]
    [InlineData("en-US", "Enable ranking", "Disable ranking", "Ranking Mode", "Per item", "Decision provider", "Custom", "Setup guide", "Decisions API", "Save")]
    public void Settings_form_renders_controls_in_the_selected_language(
        string culture, string enable, string disable, string rankingLabel, string perItem,
        string providerLabel, string custom, string setupGuide, string decisionsGuide, string save)
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo(culture));
        using var view = new LinkSearchSettingsView(settings);
        var form = Assert.IsAssignableFrom<FormContent>(Assert.Single(view.SettingsPage.GetContent()));
        var card = JsonNode.Parse(form.TemplateJson)!;
        Assert.Equal(enable, FindAction(card, "toggle", null)["title"]!.GetValue<string>());
        Assert.Equal(save, card["actions"]![0]!["title"]!.GetValue<string>());

        form.SubmitForm("{}", "{\"linksearchAction\":\"toggle\"}");
        form.SubmitForm("{}", "{\"linksearchAction\":\"mode\",\"value\":\"decision-noul\"}");
        card = JsonNode.Parse(form.TemplateJson)!;
        Assert.Equal(disable, FindAction(card, "toggle", null)["title"]!.GetValue<string>());
        Assert.Contains(card["body"]!.AsArray(), item => item?["text"]?.GetValue<string>() == rankingLabel);
        Assert.Contains(card["body"]!.AsArray(), item => item?["text"]?.GetValue<string>() == providerLabel);
        Assert.Equal($"\u2713 {perItem}", FindAction(card, "mode", "decision-noul")["title"]!.GetValue<string>());
        Assert.Equal($"\u2713 {custom}", FindAction(card, "provider", "system-one")["title"]!.GetValue<string>());

        foreach (var provider in new[] { "openrouter", "openai" })
        {
            form.SubmitForm("{}", $"{{\"linksearchAction\":\"provider\",\"value\":\"{provider}\"}}");
            card = JsonNode.Parse(form.TemplateJson)!;
            var guides = card["body"]!.AsArray()
                .OfType<JsonObject>()
                .Where(item => item["type"]?.GetValue<string>() == "ActionSet")
                .SelectMany(item => item["actions"]!.AsArray().OfType<JsonObject>())
                .Where(action => action["type"]?.GetValue<string>() == "Action.OpenUrl")
                .ToArray();
            Assert.Equal(new[] { setupGuide, decisionsGuide }, guides.Select(action => action["title"]!.GetValue<string>()));
            var field = provider == "openrouter" ? "OpenRouterApiUrl" : "OpenAiApiUrl";
            var expectedLabel = culture == "zh-CN"
                ? $"{(provider == "openrouter" ? "OpenRouter" : "OpenAI")} Decisions 接口完整地址"
                : $"{(provider == "openrouter" ? "OpenRouter" : "OpenAI")} Decisions endpoint URL";
            Assert.Equal(expectedLabel, FindField(card, $"linkSearch.{field}")!["label"]!.GetValue<string>());
        }
    }

    [Theory]
    [InlineData("en-US", "zh-CN", "启用重排序", "保存")]
    [InlineData("zh-CN", "en-US", "Enable ranking", "Save")]
    public void Saving_the_language_override_refreshes_the_settings_form(
        string systemCulture, string language, string enable, string save)
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo(systemCulture));
        using var view = new LinkSearchSettingsView(settings);
        var form = Assert.IsAssignableFrom<FormContent>(Assert.Single(view.SettingsPage.GetContent()));

        form.SubmitForm($"{{\"linkSearch.LanguageMode\":\"{language}\"}}", "{\"linksearchAction\":\"save\"}");
        form = Assert.IsAssignableFrom<FormContent>(Assert.Single(view.SettingsPage.GetContent()));
        var card = JsonNode.Parse(form.TemplateJson)!;
        Assert.Equal(enable, FindAction(card, "toggle", null)["title"]!.GetValue<string>());
        Assert.Equal(save, card["actions"]![0]!["title"]!.GetValue<string>());
    }

    private static void AssertSelection(JsonNode card, string operation, string value, string title)
    {
        var action = FindAction(card, operation, value);
        Assert.Equal(title, action["title"]!.GetValue<string>());
        Assert.Equal("default", action["style"]!.GetValue<string>());
    }

    private static JsonObject FindAction(JsonNode card, string operation, string? value) =>
        card["body"]!.AsArray()
            .OfType<JsonObject>()
            .Where(item => item["type"]?.GetValue<string>() == "ActionSet")
            .SelectMany(item => item["actions"]!.AsArray().OfType<JsonObject>())
            .Single(action => action["data"]?["linksearchAction"]?.GetValue<string>() == operation
                && action["data"]?["value"]?.GetValue<string>() == value);

    [Fact]
    public void Disabling_ranking_hides_fields_and_preserves_configuration_when_saved()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.GetSettingForTest<ToggleSetting>("linkSearch.EnableRerank").Value = true;
        settings.SetForTest("linkSearch.RankingMode", "decision-choice");
        settings.SetForTest("linkSearch.DecisionProvider", "openrouter");
        settings.SetForTest("linkSearch.OpenRouterApiKey", "old-router-key");
        using var view = new LinkSearchSettingsView(settings);
        var form = Assert.IsAssignableFrom<FormContent>(Assert.Single(view.SettingsPage.GetContent()));
        Assert.NotNull(FindField(JsonNode.Parse(form.TemplateJson), "linkSearch.OpenRouterApiKey"));

        form.SubmitForm("{\"linkSearch.OpenRouterApiKey\":\"updated-router-key\"}", "{\"linksearchAction\":\"toggle\"}");
        Assert.Null(FindField(JsonNode.Parse(form.TemplateJson), "linkSearch.OpenRouterApiKey"));
        form.SubmitForm("{}", "{\"linksearchAction\":\"save\"}");
        var reloaded = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        Assert.False(reloaded.EnableRerank);
        Assert.Equal(RankingMode.DecisionChoice, reloaded.RankingMode);
        Assert.Equal(DecisionProvider.OpenRouter, reloaded.DecisionProvider);
        Assert.Equal("updated-router-key", reloaded.DecisionApiKey);
    }

    [Fact]
    public void Settings_view_can_be_marshalled_as_a_command_palette_settings_object()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        using var view = new LinkSearchSettingsView(settings);
        var pointer = WinRT.MarshalInterface<ICommandSettings>.FromManaged(view);
        try
        {
            Assert.NotEqual(IntPtr.Zero, pointer);
            var projected = WinRT.MarshalInterface<ICommandSettings>.FromAbi(pointer);
            var content = Assert.IsAssignableFrom<IFormContent>(Assert.Single(projected.SettingsPage.GetContent()));
            var formPointer = WinRT.MarshalInterface<IFormContent>.FromManaged(content);
            try
            {
                var projectedForm = WinRT.MarshalInterface<IFormContent>.FromAbi(formPointer);
                projectedForm.SubmitForm("{}", "{\"linksearchAction\":\"toggle\"}");
                Assert.NotNull(FindField(JsonNode.Parse(projectedForm.TemplateJson), "linkSearch.RerankApiUrl"));
            }
            finally
            {
                WinRT.MarshalInterface<IFormContent>.DisposeAbi(formPointer);
            }
        }
        finally
        {
            WinRT.MarshalInterface<ICommandSettings>.DisposeAbi(pointer);
        }
    }

    private static JsonNode? FindField(JsonNode? node, string id)
    {
        if (node is JsonObject obj)
        {
            if (obj["id"]?.GetValue<string>() == id) return obj;
            foreach (var pair in obj)
            {
                if (FindField(pair.Value, id) is { } found) return found;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (FindField(item, id) is { } found) return found;
            }
        }

        return null;
    }

    [Theory]
    [InlineData(0, "rerank", (int)RankingMode.Rerank)]
    [InlineData(1, "decision-noul", (int)RankingMode.DecisionNoul)]
    [InlineData(2, "decision-choice", (int)RankingMode.DecisionChoice)]
    public void Ranking_mode_choices_use_stable_values_and_survive_reload(int index, string value, int expected)
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("zh-CN"));
        var choice = settings.GetSettingForTest<ChoiceSetSetting>("linkSearch.RankingMode");

        Assert.Equal(value, choice.Choices[index].Value);
        choice.Update(new JsonObject { [choice.Key] = choice.Choices[index].Value });
        Assert.Equal((RankingMode)expected, settings.RankingMode);
        settings.SaveSettings();

        var reloaded = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal((RankingMode)expected, reloaded.RankingMode);
    }

    [Theory]
    [InlineData(0, "auto", (int)LanguageMode.Auto)]
    [InlineData(1, "zh-CN", (int)LanguageMode.Chinese)]
    [InlineData(2, "en-US", (int)LanguageMode.English)]
    public void Language_choices_use_stable_values_and_survive_reload(int index, string value, int expected)
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        var choice = settings.GetSettingForTest<ChoiceSetSetting>("linkSearch.LanguageMode");

        Assert.Equal(value, choice.Choices[index].Value);
        choice.Update(new JsonObject { [choice.Key] = choice.Choices[index].Value });
        Assert.Equal((LanguageMode)expected, settings.LanguageMode);
        settings.SaveSettings();

        var reloaded = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("zh-CN"));
        Assert.Equal((LanguageMode)expected, reloaded.LanguageMode);
    }

    [Theory]
    [InlineData("Rerank API", "rerank", (int)RankingMode.Rerank)]
    [InlineData("Decision model: per item (Noul)", "decision-noul", (int)RankingMode.DecisionNoul)]
    [InlineData("Decision model: candidate choice (Choice)", "decision-choice", (int)RankingMode.DecisionChoice)]
    public void Legacy_ranking_mode_values_are_migrated(string legacyValue, string migratedValue, int expected)
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("zh-CN"));
        settings.SetForTest("linkSearch.RankingMode", legacyValue);
        settings.SaveSettings();

        var reloaded = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal((RankingMode)expected, reloaded.RankingMode);
        Assert.Equal(migratedValue, reloaded.GetSettingForTest<ChoiceSetSetting>("linkSearch.RankingMode").Value);
        Assert.Equal(migratedValue, JsonNode.Parse(File.ReadAllText(temp.Path))!["linkSearch.RankingMode"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("Auto", "auto", (int)LanguageMode.Auto)]
    [InlineData("Chinese", "zh-CN", (int)LanguageMode.Chinese)]
    [InlineData("English", "en-US", (int)LanguageMode.English)]
    public void Legacy_language_values_are_migrated(string legacyValue, string migratedValue, int expected)
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.SetForTest("linkSearch.LanguageMode", legacyValue);
        settings.SaveSettings();

        var reloaded = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("zh-CN"));
        Assert.Equal((LanguageMode)expected, reloaded.LanguageMode);
        Assert.Equal(migratedValue, reloaded.GetSettingForTest<ChoiceSetSetting>("linkSearch.LanguageMode").Value);
        Assert.Equal(migratedValue, JsonNode.Parse(File.ReadAllText(temp.Path))!["linkSearch.LanguageMode"]!.GetValue<string>());
    }

    [Fact]
    public void Legacy_choice_migration_preserves_other_settings()
    {
        using var temp = new TempSettingsFile();
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        settings.SetForTest("linkSearch.LanguageMode", "Chinese");
        settings.SetForTest("linkSearch.RankingMode", "Decision model: per item (Noul)");
        settings.SetForTest("linkSearch.LinkwardenApiKey", "linkwarden-key");
        settings.SetForTest("linkSearch.RerankApiKey", "rerank-key");
        settings.SetForTest("linkSearch.DecisionApiKey", "decision-key");
        settings.SaveSettings();
        var state = JsonNode.Parse(File.ReadAllText(temp.Path))!.AsObject();
        state["other.setting"] = "preserved";
        File.WriteAllText(temp.Path, state.ToJsonString());

        var reloaded = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal(LanguageMode.Chinese, reloaded.LanguageMode);
        Assert.Equal(RankingMode.DecisionNoul, reloaded.RankingMode);
        Assert.Equal("linkwarden-key", reloaded.LinkwardenApiKey);
        Assert.Equal("rerank-key", reloaded.RerankApiKey);
        Assert.Equal("decision-key", reloaded.DecisionApiKey);
        state = JsonNode.Parse(File.ReadAllText(temp.Path))!.AsObject();
        Assert.Equal("preserved", state["other.setting"]!.GetValue<string>());
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
