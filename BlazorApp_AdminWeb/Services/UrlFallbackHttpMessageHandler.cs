using System.Net.Http;
using System.Net.Sockets;

namespace BlazorApp_AdminWeb.Services;

/// <summary>
/// Automatically retries failed requests to the configured Admin API URL
/// with a local fallback base URL when the primary endpoint is unavailable.
/// </summary>
public sealed class UrlFallbackHttpMessageHandler : DelegatingHandler
{
    private const string HostHeaderName = "Host";
    private readonly ILogger<UrlFallbackHttpMessageHandler> _logger;
    private readonly string? _fallbackBaseUrl;

    public UrlFallbackHttpMessageHandler(
        ILogger<UrlFallbackHttpMessageHandler> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _fallbackBaseUrl = configuration["AdminApiForLocal:BaseUrl"];
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var fallbackRequest = await CreateFallbackRequestAsync(request, cancellationToken);

        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex) when (IsConnectionError(ex) && fallbackRequest is not null)
        {
            _logger.LogWarning(
                ex,
                "Primary request to {PrimaryUrl} failed with a connection error. Retrying with fallback URL {FallbackBaseUrl}.",
                request.RequestUri,
                _fallbackBaseUrl);

            try
            {
                return await base.SendAsync(fallbackRequest, cancellationToken);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(
                    fallbackEx,
                    "Fallback request to {FallbackUrl} also failed after primary URL failure.",
                    fallbackRequest.RequestUri);
                throw;
            }
        }
    }

    private async Task<HttpRequestMessage?> CreateFallbackRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is null || string.IsNullOrWhiteSpace(_fallbackBaseUrl))
        {
            return null;
        }

        var fallbackRequestUri = ReplaceBaseUri(request.RequestUri, _fallbackBaseUrl);
        if (fallbackRequestUri is null)
        {
            return null;
        }

        var fallbackRequest = new HttpRequestMessage(request.Method, fallbackRequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            if (string.Equals(header.Key, HostHeaderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            fallbackRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken);
            var bodyBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            fallbackRequest.Content = new ByteArrayContent(bodyBytes);

            foreach (var contentHeader in request.Content.Headers)
            {
                fallbackRequest.Content.Headers.TryAddWithoutValidation(contentHeader.Key, contentHeader.Value);
            }
        }

        return fallbackRequest;
    }

    private static bool IsConnectionError(HttpRequestException ex)
    {
        if (ex.InnerException is null)
        {
            return true;
        }

        return HasKnownConnectionException(ex.InnerException);
    }

    private static bool HasKnownConnectionException(Exception exception) =>
        exception switch
        {
            SocketException => true,
            IOException => true,
            TimeoutException => true,
            OperationCanceledException => true,
            HttpRequestException innerHttpException => innerHttpException.InnerException is null
                || HasKnownConnectionException(innerHttpException.InnerException),
            _ => exception.InnerException is not null && HasKnownConnectionException(exception.InnerException)
        };

    private static Uri? ReplaceBaseUri(Uri originalUri, string fallbackBaseUrl)
    {
        if (!Uri.TryCreate(fallbackBaseUrl, UriKind.Absolute, out var fallbackBaseUri))
        {
            return null;
        }

        var pathAndQuery = $"{originalUri.AbsolutePath.TrimStart('/')}{originalUri.Query}";
        return new Uri(fallbackBaseUri, pathAndQuery);
    }
}