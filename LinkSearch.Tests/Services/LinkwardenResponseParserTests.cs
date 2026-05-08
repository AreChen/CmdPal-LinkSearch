using LinkSearch.Localization;
using LinkSearch.Models;
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

    [Fact]
    public void Parse_ignores_malformed_tag_entries()
    {
        const string json = """
        {
          "data": [
            {
              "name": "Link",
              "url": "https://example.com",
              "tags": [null, "windows", { "name": "valid" }]
            }
          ]
        }
        """;

        var result = LinkwardenResponseParser.Parse(json);

        Assert.Null(result.Error);
        var link = Assert.Single(result.Links);
        Assert.Equal(new[] { "valid" }, link.Tags);
    }

    [Fact]
    public void Parse_skips_malformed_link_entries()
    {
        const string json = """
        {
          "data": [
            null,
            "bad",
            { "name": "Link", "url": "https://example.com" }
          ]
        }
        """;

        var result = LinkwardenResponseParser.Parse(json);

        Assert.Null(result.Error);
        var link = Assert.Single(result.Links);
        Assert.Equal("Link", link.Name);
    }
}
