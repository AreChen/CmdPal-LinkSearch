using System;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
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
