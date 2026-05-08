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
