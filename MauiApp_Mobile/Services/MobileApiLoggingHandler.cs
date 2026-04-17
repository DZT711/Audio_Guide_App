using System.Diagnostics;
using System.Net.Http;

namespace MauiApp_Mobile.Services;

internal sealed class MobileApiLoggingHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not null && MobileApiOptions.IsPlaceholderHost(request.RequestUri.Host))
        {
            request.RequestUri = MobileApiOptions.RewriteToCurrentBaseUri(request.RequestUri);
        }

        var targetUri = request.RequestUri;
        var shouldLog = ShouldLogApiRequest(targetUri);
        var startedAt = Stopwatch.StartNew();

        if (shouldLog)
        {
            Debug.WriteLine($"[MobileApi] Connecting to API: {request.Method} {targetUri}");
        }

        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (shouldLog)
            {
                Debug.WriteLine(
                    $"[MobileApi] API response: {request.Method} {targetUri} -> {(int)response.StatusCode} {response.ReasonPhrase} in {startedAt.ElapsedMilliseconds} ms");
            }

            if (response.IsSuccessStatusCode && targetUri is not null)
            {
                var builder = new UriBuilder(targetUri)
                {
                    Path = "/",
                    Query = string.Empty
                };
                MobileApiOptions.SetLastKnownWorkingBaseUrl(builder.Uri.AbsoluteUri);
            }

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (shouldLog)
            {
                Debug.WriteLine(
                    $"[MobileApi] API request failed: {request.Method} {targetUri} after {startedAt.ElapsedMilliseconds} ms. {ex.Message}");
            }

            throw;
        }
    }

    private static bool ShouldLogApiRequest(Uri? requestUri)
    {
        if (requestUri is null || !requestUri.IsAbsoluteUri)
        {
            return true;
        }

        return string.Equals(requestUri.Host, MobileApiOptions.BaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
               MobileApiOptions.IsPlaceholderHost(requestUri.Host);
    }
}
