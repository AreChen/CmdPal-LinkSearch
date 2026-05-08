using System;
using System.Collections.Generic;
using System.Globalization;

namespace LinkSearch.Localization;

internal static class LocalizedStrings
{
    private static readonly Dictionary<LocalizedTextKey, (string Chinese, string English)> Text =
        new Dictionary<LocalizedTextKey, (string Chinese, string English)>
        {
            [LocalizedTextKey.LanguageSettingLabel] = ("语言", "Language"),
            [LocalizedTextKey.LanguageSettingDescription] = ("选择界面显示语言。自动会跟随系统语言。", "Choose the interface language. Auto follows the system language."),
            [LocalizedTextKey.LanguageAutoChoice] = ("自动", "Auto"),
            [LocalizedTextKey.LanguageChineseChoice] = ("中文", "Chinese"),
            [LocalizedTextKey.LanguageEnglishChoice] = ("English", "English"),
            [LocalizedTextKey.LinkwardenBaseUrlLabel] = ("Linkwarden 地址", "Linkwarden Base URL"),
            [LocalizedTextKey.LinkwardenBaseUrlDescription] = ("Linkwarden 实例的基础地址。", "Base URL of your Linkwarden instance."),
            [LocalizedTextKey.LinkwardenApiKeyLabel] = ("Linkwarden API 密钥", "Linkwarden API Key"),
            [LocalizedTextKey.LinkwardenApiKeyDescription] = ("用于访问 Linkwarden API 的令牌。", "Token used to access the Linkwarden API."),
            [LocalizedTextKey.EnableRerankLabel] = ("启用重排序", "Enable Rerank"),
            [LocalizedTextKey.EnableRerankDescription] = ("使用重排序服务优化搜索结果。", "Use the rerank service to improve search results."),
            [LocalizedTextKey.RerankApiUrlLabel] = ("重排序 API 地址", "Rerank API URL"),
            [LocalizedTextKey.RerankApiUrlDescription] = ("重排序服务的 API 地址。", "API URL for the rerank service."),
            [LocalizedTextKey.RerankApiKeyLabel] = ("重排序 API 密钥", "Rerank API Key"),
            [LocalizedTextKey.RerankApiKeyDescription] = ("用于访问重排序服务的密钥。", "API key used to access the rerank service."),
            [LocalizedTextKey.RerankModelNameLabel] = ("重排序模型名称", "Rerank Model Name"),
            [LocalizedTextKey.RerankModelNameDescription] = ("重排序服务使用的模型名称。", "Model name used by the rerank service."),
            [LocalizedTextKey.SearchDelayLabel] = ("搜索延迟", "Search Delay"),
            [LocalizedTextKey.SearchDelayDescription] = ("输入停止后开始搜索前等待的时间。", "Time to wait after typing stops before searching."),
            [LocalizedTextKey.MaxResultsLabel] = ("最大结果数", "Max Results"),
            [LocalizedTextKey.MaxResultsDescription] = ("每次搜索显示的最大链接数量。", "Maximum number of links to show for each search."),
            [LocalizedTextKey.PageName] = ("LinkSearch", "LinkSearch"),
            [LocalizedTextKey.SearchPlaceholder] = ("搜索链接...", "Search links..."),
            [LocalizedTextKey.EmptyResultTitle] = ("没有找到结果", "No results found"),
            [LocalizedTextKey.EmptyResultSubtitle] = ("尝试其他关键词。", "Try different keywords."),
            [LocalizedTextKey.EmptyQueryTitle] = ("输入关键词开始搜索", "Type to start searching"),
            [LocalizedTextKey.OpenLinkCommandName] = ("打开链接", "Open link"),
            [LocalizedTextKey.SearchFailed] = ("搜索失败", "Search failed"),
            [LocalizedTextKey.RetryOrCheckSettings] = ("请重试或检查设置。", "Retry or check your settings."),
            [LocalizedTextKey.MissingTokenTitle] = ("缺少 API 令牌", "Missing API token"),
            [LocalizedTextKey.ConfigureApiKeyTitle] = ("请在设置中配置 API 密钥。", "Configure the API key in settings."),
            [LocalizedTextKey.EnvTokenHintTitle] = ("也可以通过环境变量提供令牌。", "You can also provide the token through an environment variable."),
            [LocalizedTextKey.InvalidApiKeyTitle] = ("API 密钥无效", "Invalid API key"),
            [LocalizedTextKey.InvalidApiKeySubtitle] = ("请检查 Linkwarden API 密钥。", "Check your Linkwarden API key."),
            [LocalizedTextKey.InvalidBaseUrlTitle] = ("服务地址无效", "Invalid base URL"),
            [LocalizedTextKey.InvalidBaseUrlSubtitle] = ("请检查 Linkwarden 服务地址。", "Check your Linkwarden server address."),
            [LocalizedTextKey.ApiRequestFailed] = ("API 请求失败：{0}", "API request failed: {0}"),
            [LocalizedTextKey.MissingDataNode] = ("响应缺少 data 节点。", "Response is missing the data node."),
            [LocalizedTextKey.InvalidDataNode] = ("响应中的 data 节点无效。", "The data node in the response is invalid."),
            [LocalizedTextKey.LinksNotArray] = ("链接数据不是数组。", "Links data is not an array."),
            [LocalizedTextKey.NetworkRequestFailed] = ("网络请求失败", "Network request failed"),
            [LocalizedTextKey.CheckServerAndNetwork] = ("请检查服务器地址和网络连接。", "Check the server address and network connection."),
            [LocalizedTextKey.RequestTimeout] = ("请求超时", "Request timed out"),
            [LocalizedTextKey.RetryLater] = ("请稍后重试。", "Try again later."),
            [LocalizedTextKey.UrlFormatError] = ("URL 格式错误", "URL format error"),
            [LocalizedTextKey.CheckServerAddress] = ("请检查服务器地址。", "Check the server address."),
            [LocalizedTextKey.ApiCallException] = ("API 调用异常：{0}", "API call exception: {0}"),
            [LocalizedTextKey.TagLabel] = ("标签", "Tag"),
            [LocalizedTextKey.RerankConnectionSuccess] = ("Rerank 连接成功，响应时间：{0}ms", "Rerank connection succeeded. Response time: {0}ms"),
            [LocalizedTextKey.RerankConnectionFailed] = ("Rerank 连接失败：{0}", "Rerank connection failed: {0}"),
            [LocalizedTextKey.RerankConnectionConfigurationError] = ("请检查 Rerank 配置。", "Check the Rerank configuration."),
            [LocalizedTextKey.RerankConnectionAuthenticationError] = ("API 密钥无效或已过期。", "The API key is invalid or expired."),
            [LocalizedTextKey.RerankConnectionAuthorizationError] = ("当前 API 密钥无权访问该服务。", "The API key is not authorized to access the service."),
            [LocalizedTextKey.RerankConnectionEndpointError] = ("API 地址不存在。", "The API endpoint does not exist."),
            [LocalizedTextKey.RerankConnectionRateLimitError] = ("请求过于频繁，请稍后重试。", "The request rate limit was exceeded. Try again later."),
            [LocalizedTextKey.RerankConnectionServerError] = ("服务器返回内部错误。", "The server returned an internal error."),
            [LocalizedTextKey.RerankConnectionResponseError] = ("API 响应格式无效。", "The API response format is invalid."),
            [LocalizedTextKey.RerankConnectionTimeoutError] = ("请求超时，请检查服务状态或稍后重试。", "The request timed out. Check the service status or try again later."),
            [LocalizedTextKey.RerankConnectionNetworkError] = ("网络请求失败，请检查服务器地址和网络连接。", "The network request failed. Check the server address and network connection."),
            [LocalizedTextKey.RerankConnectionSslError] = ("SSL 连接失败，请检查服务器证书。", "The SSL connection failed. Check the server certificate."),
            [LocalizedTextKey.RerankConnectionDnsError] = ("DNS 解析失败，请检查服务器地址。", "DNS resolution failed. Check the server address."),
            [LocalizedTextKey.RerankConnectionConnectionError] = ("无法连接到服务器，请检查服务是否可用。", "Unable to connect to the server. Check whether the service is available."),
            [LocalizedTextKey.RerankConnectionCanceled] = ("连接测试已取消。", "The connection test was canceled."),
            [LocalizedTextKey.RerankConnectionUnknownError] = ("发生未知错误，请稍后重试。", "An unknown error occurred. Try again later."),
            [LocalizedTextKey.RerankConnectionException] = ("重排序服务连接异常：{0}", "Rerank service connection exception: {0}"),
        };

    internal static LanguageMode ParseLanguageMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return LanguageMode.Auto;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "auto" => LanguageMode.Auto,
            "zh" or "zh-cn" or "zh-hans" or "chinese" => LanguageMode.Chinese,
            "en" or "en-us" or "english" => LanguageMode.English,
            _ => LanguageMode.Auto,
        };
    }

    internal static string ToSettingValue(LanguageMode mode)
    {
        return mode switch
        {
            LanguageMode.Chinese => "zh-CN",
            LanguageMode.English => "en-US",
            _ => "auto",
        };
    }

    internal static UiLanguage ResolveUiLanguage(LanguageMode mode, CultureInfo culture)
    {
        return mode switch
        {
            LanguageMode.Chinese => UiLanguage.Chinese,
            LanguageMode.English => UiLanguage.English,
            _ when culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) => UiLanguage.Chinese,
            _ => UiLanguage.English,
        };
    }

    internal static string Get(LocalizedTextKey key, UiLanguage language)
    {
        var text = Text[key];
        return language == UiLanguage.Chinese ? text.Chinese : text.English;
    }

    internal static string Format(LocalizedTextKey key, UiLanguage language, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key, language), args);
    }
}
