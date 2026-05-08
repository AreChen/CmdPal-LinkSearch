using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LinkSearch.Helpers;
using LinkSearch.Localization;
using LinkSearch.Models;
using LinkSearch.Presenters;
using Xunit;

namespace LinkSearch.Tests.Presenters;

public sealed class SearchResultPresenterTests
{
    [Fact]
    public void CreateResultItems_caps_valid_results_at_max_results()
    {
        using var temp = new TempSettingsFile();
        var settings = CreateEnglishSettings(temp);
        settings.SetForTest("linkSearch.MaxResults", "2");
        var presenter = new SearchResultPresenter(settings);

        var items = presenter.CreateResultItems(new[]
        {
            new LinkwardenLink("First", "", "https://first.test", "", Array.Empty<string>()),
            new LinkwardenLink("Second", "", "https://second.test", "", Array.Empty<string>()),
            new LinkwardenLink("Third", "", "https://third.test", "", Array.Empty<string>()),
        });

        Assert.Equal(2, items.Count);
        Assert.Equal("First", items[0].Title);
        Assert.Equal("Second", items[1].Title);
    }

    [Fact]
    public void CreateResultItems_skips_invalid_and_blank_urls_and_returns_empty_item_when_all_invalid()
    {
        using var temp = new TempSettingsFile();
        var presenter = new SearchResultPresenter(CreateEnglishSettings(temp));

        var items = presenter.CreateResultItems(new LinkwardenLink?[]
        {
            new LinkwardenLink("Blank", "", " ", "", Array.Empty<string>()),
            new LinkwardenLink("Invalid", "", "not a url", "", Array.Empty<string>()),
            null,
        }!);

        Assert.Single(items);
        Assert.Equal("No results found", items[0].Title);
    }

    [Fact]
    public void CreateResultItems_returns_empty_item_for_empty_input()
    {
        using var temp = new TempSettingsFile();
        var presenter = new SearchResultPresenter(CreateEnglishSettings(temp));

        var nullItems = presenter.CreateResultItems(null);
        var emptyItems = presenter.CreateResultItems(Array.Empty<LinkwardenLink>());

        Assert.Single(nullItems);
        Assert.Single(emptyItems);
        Assert.Equal("No results found", nullItems[0].Title);
        Assert.Equal("No results found", emptyItems[0].Title);
    }

    [Fact]
    public void CreateResultItems_handles_nullish_fields_and_tags()
    {
        using var temp = new TempSettingsFile();
        var presenter = new SearchResultPresenter(CreateEnglishSettings(temp));

        var exception = Record.Exception(() => presenter.CreateResultItems(new[]
        {
            new LinkwardenLink(null!, null!, "https://example.test", null!, null!),
        }));

        Assert.Null(exception);
    }

    [Fact]
    public void CreateErrorItems_formats_detail_and_status_code_errors()
    {
        using var temp = new TempSettingsFile();
        var presenter = new SearchResultPresenter(CreateEnglishSettings(temp));

        var exceptionItems = presenter.CreateErrorItems(new LinkwardenSearchError(LinkwardenSearchErrorKind.Unexpected, LocalizedTextKey.ApiCallException, "boom"));
        var requestItems = presenter.CreateErrorItems(new LinkwardenSearchError(LinkwardenSearchErrorKind.ApiFailure, LocalizedTextKey.ApiRequestFailed, StatusCode: 503));

        Assert.Equal("API call exception: boom", exceptionItems[0].Title);
        Assert.Equal("API request failed: 503", requestItems[0].Title);
    }

    [Fact]
    public void OpenUrlCommand_Name_uses_injected_english_settings_language()
    {
        using var temp = new TempSettingsFile();
        var settings = CreateEnglishSettings(temp);

        var command = new OpenUrlCommand("https://example.test", settings);

        Assert.Equal("Open link", command.Name);
    }

    private static SettingsManager CreateEnglishSettings(TempSettingsFile temp)
    {
        var settings = new SettingsManager(temp.Path, () => CultureInfo.GetCultureInfo("zh-CN"));
        settings.SetForTest("linkSearch.LanguageMode", "en-US");
        return settings;
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
