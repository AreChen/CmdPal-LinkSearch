using System;
using System.Linq;
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

    [Fact]
    public void ApplyRerankOrder_ignores_duplicate_and_out_of_range_indices()
    {
        var links = new[] { Link("A"), Link("B"), Link("C") };
        var response = new RerankResponse
        {
            Results = new[]
            {
                new RerankResult { Index = 1 },
                new RerankResult { Index = 1 },
                new RerankResult { Index = 99 },
                new RerankResult { Index = -1 },
                new RerankResult { Index = 0 },
            },
        };

        var ordered = RerankService.ApplyRerankOrder(links, response);

        Assert.Equal(new[] { "B", "A", "C" }, ordered.Select(link => link.Name));
    }

    private static LinkwardenLink Link(string name) => new(name, string.Empty, $"https://example.com/{name}", string.Empty, Array.Empty<string>());
}
