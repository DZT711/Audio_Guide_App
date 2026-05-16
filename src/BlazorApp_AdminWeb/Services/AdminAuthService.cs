using System.Text.Json;
using Microsoft.JSInterop;
using Project_SharedClassLibrary.Contracts;

namespace BlazorApp_AdminWeb.Services;

public sealed class AdminAuthService(
    IJSRuntime jsRuntime,
    AdminSessionState sessionState,
    AdminApiClient apiClient)
{
    private const string AuthStorageKey = "smartTourAdmin.auth";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string? LastErrorMessage { get; private set; }

    public async Task<bool> IsAuthenticatedAsync()
    {
        if (sessionState.IsAuthenticated)
        {
            LastErrorMessage = null;
            return true;
        }

        var persistedSession = await ReadPersistedSessionAsync();
        if (persistedSession is null)
        {
            return false;
        }

        sessionState.SetSession(persistedSession);

        try
        {
            var currentSession = await apiClient.GetCurrentSessionAsync();
            sessionState.SetSession(currentSession);
            await PersistSessionAsync(currentSession);
            LastErrorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            sessionState.Clear();
            await ClearPersistedSessionAsync();
            return false;
        }
    }

    public async Task<bool> LoginAsync(string userName, string password)
    {
        try
        {
            var response = await apiClient.LoginAsync(userName, password);
            sessionState.SetSession(response);
            await PersistSessionAsync(response);
            LastErrorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            sessionState.Clear();
            await ClearPersistedSessionAsync();
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            if (sessionState.IsAuthenticated)
            {
                await apiClient.LogoutAsync();
            }
        }
        catch
        {
        }
        finally
        {
            LastErrorMessage = null;
            sessionState.Clear();
            await ClearPersistedSessionAsync();
        }
    }

    private async Task PersistSessionAsync(AdminLoginResponse response)
    {
        try
        {
            var payload = JsonSerializer.Serialize(response, JsonOptions);
            await jsRuntime.InvokeVoidAsync("smartTourAdmin.storage.set", AuthStorageKey, payload);
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
    }

    private async Task<AdminLoginResponse?> ReadPersistedSessionAsync()
    {
        try
        {
            var payload = await jsRuntime.InvokeAsync<string?>("smartTourAdmin.storage.get", AuthStorageKey);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            return JsonSerializer.Deserialize<AdminLoginResponse>(payload, JsonOptions);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
        catch (JSException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private async Task ClearPersistedSessionAsync()
    {
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
        catch (TaskCanceledException)
        {
        }
    }
}
