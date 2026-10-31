using System.Collections.Generic;

namespace LinkSearch.Models;

internal enum RankingStatus
{
    Skipped,
    Applied,
    Failed,
    ConfigurationError,
}

internal sealed record RankingOutcome(
    IReadOnlyList<LinkwardenLink> Links,
    RankingStatus Status,
    int? HttpStatusCode = null);
