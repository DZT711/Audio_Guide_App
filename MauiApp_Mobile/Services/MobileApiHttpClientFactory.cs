using System.Net;

namespace MauiApp_Mobile.Services;

internal static class MobileApiHttpClientFactory
{
    public static HttpClient Create(TimeSpan timeout, int maxConnectionsPerServer)
    {
        var socketsHandler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = maxConnectionsPerServer
        };

        return new HttpClient(new MobileApiLoggingHandler(socketsHandler))
        {
            BaseAddress = MobileApiOptions.PlaceholderBaseUri,
            Timeout = timeout
        };
    }
}
