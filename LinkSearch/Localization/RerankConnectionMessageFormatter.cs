using LinkSearch.Models;

namespace LinkSearch.Localization;

internal static class RerankConnectionMessageFormatter
{
    internal static string FormatFailure(RerankConnectionTestResult result, UiLanguage language)
    {
        var detailKey = result.ErrorType switch
        {
            "ConfigurationError" => LocalizedTextKey.RerankConnectionConfigurationError,
            "AuthenticationError" => LocalizedTextKey.RerankConnectionAuthenticationError,
            "AuthorizationError" => LocalizedTextKey.RerankConnectionAuthorizationError,
            "EndpointError" or "HttpError" => LocalizedTextKey.RerankConnectionEndpointError,
            "RateLimitError" => LocalizedTextKey.RerankConnectionRateLimitError,
            "ServerError" => LocalizedTextKey.RerankConnectionServerError,
            "ResponseError" => LocalizedTextKey.RerankConnectionResponseError,
            "TimeoutError" => LocalizedTextKey.RerankConnectionTimeoutError,
            "SslError" => LocalizedTextKey.RerankConnectionSslError,
            "DnsError" => LocalizedTextKey.RerankConnectionDnsError,
            "ConnectionError" => LocalizedTextKey.RerankConnectionConnectionError,
            "NetworkError" => LocalizedTextKey.RerankConnectionNetworkError,
            "Canceled" => LocalizedTextKey.RerankConnectionCanceled,
            _ => LocalizedTextKey.RerankConnectionUnknownError,
        };

        var detail = LocalizedStrings.Get(detailKey, language);
        return LocalizedStrings.Format(LocalizedTextKey.RerankConnectionFailed, language, detail);
    }
}
