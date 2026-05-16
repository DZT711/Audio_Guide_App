using System.Net.Http;

namespace MauiApp_Mobile.Services;

public static class FriendlyMessageService
{
    public static string Resolve(Exception? exception, string fallbackMessage)
    {
        if (exception is null)
        {
            return fallbackMessage;
        }

        if (IsServerFailure(exception))
        {
            return "Server connect failure";
        }

        var message = exception.Message?.Trim();
        return string.IsNullOrWhiteSpace(message) ? fallbackMessage : message;
    }

    public static bool IsServerFailure(Exception? exception)
    {
        if (exception is null)
        {
            return false;
        }

        if (exception is HttpRequestException || exception is TaskCanceledException || exception is TimeoutException)
        {
            return true;
        }

        var message = exception.Message ?? string.Empty;
        return message.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("No such host", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("actively refused", StringComparison.OrdinalIgnoreCase);
    }
}
