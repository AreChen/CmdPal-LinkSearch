using System;
using System.Globalization;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;
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
