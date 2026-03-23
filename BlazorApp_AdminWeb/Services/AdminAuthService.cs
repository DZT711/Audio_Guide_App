using Microsoft.JSInterop;

namespace BlazorApp_AdminWeb.Services;

public sealed class AdminAuthService(IJSRuntime jsRuntime, AdminSessionState sessionState)
{
    private const string AuthStorageKey = "smartTourAdmin.auth";

    public async Task<bool> IsAuthenticatedAsync()
    {
        if (sessionState.IsAuthenticated)
        {
            return true;
        }

        try
        {
            var value = await jsRuntime.InvokeAsync<string?>("smartTourAdmin.storage.get", AuthStorageKey);
            sessionState.IsAuthenticated = string.Equals(value, "admin", StringComparison.Ordinal);
            return sessionState.IsAuthenticated;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
        catch (JSException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    public async Task<bool> LoginAsync(string userName, string password)
    {
        var isValid = string.Equals(userName, "admin", StringComparison.OrdinalIgnoreCase)
            && string.Equals(password, "admin", StringComparison.Ordinal);

        if (!isValid)
        {
            return false;
        }

        sessionState.IsAuthenticated = true;

        try
        {
            await jsRuntime.InvokeVoidAsync("smartTourAdmin.storage.set", AuthStorageKey, "admin");
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
        catch (TaskCanceledException)
        {
        }

        return true;
    }

    public async Task LogoutAsync()
    {
        sessionState.IsAuthenticated = false;

        try
        {
            await jsRuntime.InvokeVoidAsync("smartTourAdmin.storage.remove", AuthStorageKey);
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
    }
}
