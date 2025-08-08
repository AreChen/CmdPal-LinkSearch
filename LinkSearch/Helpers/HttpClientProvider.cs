using System;
using System.Net.Http;

namespace LinkSearch.Helpers
{
    /// <summary>
    /// 提供全局共享的 HttpClient 实例，使用 SocketsHttpHandler 进行合理的连接池配置。
    /// 生存期与应用一致。
    /// </summary>
    internal static class HttpClientProvider
    {
        private static readonly Lazy<HttpClient> s_client = new Lazy<HttpClient>(() =>
        {
            // 使用 SocketsHttpHandler 来优化连接复用与空闲回收
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                // 根据你的使用场景调整并发连接数（默认 20 是常见的折中）
                MaxConnectionsPerServer = 20,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };

            var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // 可选：设置默认 User-Agent，便于服务端统计/诊断
            try
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LinkSearch/1.0");
            }
            catch
            {
                // 忽略解析 header 的异常，避免影响客户端功能
            }

            return client;
        });

        public static HttpClient Shared => s_client.Value;
    }
}