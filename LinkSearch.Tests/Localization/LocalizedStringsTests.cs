using System.Globalization;
using LinkSearch.Localization;
using LinkSearch.Models;
using Xunit;

namespace LinkSearch.Tests.Localization;

public sealed class LocalizedStringsTests
{
    [Theory]
    [InlineData(null, "auto")]
    [InlineData("", "auto")]
    [InlineData("auto", "auto")]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh", "zh-CN")]
    [InlineData("en-US", "en-US")]
    [InlineData("en", "en-US")]
    [InlineData("unexpected", "auto")]
    public void ParseLanguageMode_accepts_known_values(string? value, string expectedSettingValue)
    {
        Assert.Equal(expectedSettingValue, LocalizedStrings.ToSettingValue(LocalizedStrings.ParseLanguageMode(value)));
    }

    [Theory]
    [InlineData("auto", "zh-CN", "语言")]
    [InlineData("auto", "zh-Hans", "语言")]
    [InlineData("auto", "en-US", "Language")]
    [InlineData("zh-CN", "en-US", "语言")]
    [InlineData("en-US", "zh-CN", "Language")]
    public void ResolveUiLanguage_uses_manual_override_or_culture(string modeValue, string culture, string expectedLabel)
    {
        var mode = LocalizedStrings.ParseLanguageMode(modeValue);
        var language = LocalizedStrings.ResolveUiLanguage(mode, CultureInfo.GetCultureInfo(culture));

        Assert.Equal(expectedLabel, LocalizedStrings.Get(LocalizedTextKey.LanguageSettingLabel, language));
    }

    [Fact]
    public void Get_returns_selected_language_text()
    {
        Assert.Equal("语言", LocalizedStrings.Get(LocalizedTextKey.LanguageSettingLabel, UiLanguage.Chinese));
        Assert.Equal("Language", LocalizedStrings.Get(LocalizedTextKey.LanguageSettingLabel, UiLanguage.English));
    }

    [Fact]
    public void Get_has_text_for_every_key_in_each_language()
    {
        foreach (var key in System.Enum.GetValues<LocalizedTextKey>())
        {
            Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.Get(key, UiLanguage.Chinese)));
            Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.Get(key, UiLanguage.English)));
        }
    }

    [Fact]
    public void Rerank_connection_error_categories_are_localized()
    {
        Assert.True(System.Enum.TryParse("RerankConnectionConfigurationError", out LocalizedTextKey key));
        var chinese = LocalizedStrings.Get(key, UiLanguage.Chinese);
        var english = LocalizedStrings.Get(key, UiLanguage.English);

        Assert.False(string.IsNullOrWhiteSpace(chinese));
        Assert.False(string.IsNullOrWhiteSpace(english));
        Assert.DoesNotContain("ConfigurationError", chinese);
        Assert.Contains("configuration", english, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rerank_connection_failure_formatter_localizes_configuration_errors_for_chinese_ui()
    {
        var result = RerankConnectionTestResult.CreateFailure(
            "ConfigurationError",
            "Rerank API URL is not configured");

        var message = RerankConnectionMessageFormatter.FormatFailure(result, UiLanguage.Chinese);

        Assert.Contains("Rerank 连接失败", message);
        Assert.Contains("配置", message);
        Assert.DoesNotContain("ConfigurationError", message);
        Assert.DoesNotContain("Rerank API URL is not configured", message);
    }
}
