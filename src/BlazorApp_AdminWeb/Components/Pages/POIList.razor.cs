using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BlazorApp_AdminWeb.Models;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;

namespace BlazorApp_AdminWeb.Components.Pages;

public partial class POIList
{
    private readonly HashSet<int> _selectedQrLocationIds = [];
    private readonly LocationQrBulkRequest _bulkQrRequest = new();
    private bool _isBulkQrModalOpen;
    private bool _isBulkQrExporting;

    private bool CanViewQr => SessionState.HasPermission(AdminPermissions.QrRead);
    private bool CanGenerateQr => SessionState.HasPermission(AdminPermissions.QrManage);
    private bool CanBulkGenerateQr => SessionState.HasPermission(AdminPermissions.QrBulk);
    private int SelectedQrLocationCount => _selectedQrLocationIds.Count;
    private bool IsCurrentPageSelectedForBulkQr =>
        PagedLocations.Count > 0 && PagedLocations.All(item => _selectedQrLocationIds.Contains(item.Id));

    private IReadOnlyList<LocationDto> SelectedBulkQrLocations => _locations
        .Where(item => _selectedQrLocationIds.Contains(item.Id))
        .OrderBy(item => item.Name)
        .ToList();

    private LocationDto? QrEditorLocation => _editingId is int editingId
        ? _locations.FirstOrDefault(item => item.Id == editingId) ?? _selectedLocation
        : null;

    private bool IsLocationSelectedForBulkQr(int locationId) =>
        _selectedQrLocationIds.Contains(locationId);

    private void SetLocationQrSelection(int locationId, bool isSelected)
    {
        if (!CanBulkGenerateQr || locationId <= 0)
        {
            return;
        }

        if (isSelected)
        {
            _selectedQrLocationIds.Add(locationId);
            return;
        }

        _selectedQrLocationIds.Remove(locationId);
    }

    private void SetCurrentPageQrSelection(bool isSelected)
    {
        if (!CanBulkGenerateQr)
        {
            return;
        }

        foreach (var location in PagedLocations)
        {
            if (isSelected)
            {
                _selectedQrLocationIds.Add(location.Id);
            }
            else
            {
                _selectedQrLocationIds.Remove(location.Id);
            }
        }
    }

    private async Task OpenQrAsync(LocationDto location)
    {
        if (!CanViewQr)
        {
            Notifications.Warning("Your role cannot access QR tools for locations.", "QR unavailable");
            return;
        }

        await StartEditAsync(location);
        _editorTab = "settings";
    }

    private void OpenBulkQrModal()
    {
        if (!CanBulkGenerateQr)
        {
            Notifications.Warning("Your role cannot export bulk QR files.", "QR unavailable");
            return;
        }

        TrimBulkQrSelection();

        if (_selectedQrLocationIds.Count == 0)
        {
            Notifications.Info("Select at least one location from the table before exporting a QR ZIP.", "No locations selected");
            return;
        }

        _bulkQrRequest.LocationIds = _selectedQrLocationIds
            .OrderBy(item => item)
            .ToList();
        _isBulkQrModalOpen = true;
    }

    private Task CloseBulkQrModalAsync()
    {
        _isBulkQrModalOpen = false;
        return InvokeAsync(StateHasChanged);
    }

    private async Task ExportBulkQrAsync()
    {
        if (_isBulkQrExporting)
        {
            return;
        }

        if (!CanBulkGenerateQr)
        {
            Notifications.Warning("Your role cannot export bulk QR files.", "QR unavailable");
            return;
        }

        var selectedLocationIds = _selectedQrLocationIds
            .Where(item => item > 0)
            .Distinct()
            .ToList();
        if (selectedLocationIds.Count == 0)
        {
            Notifications.Info("Select at least one location before exporting the QR ZIP.", "No locations selected");
            return;
        }

        if (selectedLocationIds.Count > 100)
        {
            Notifications.Warning("Bulk QR export supports up to 100 locations at a time. Reduce the selection and try again.", "Selection too large");
            return;
        }

        _isBulkQrExporting = true;
        _bulkQrRequest.LocationIds = selectedLocationIds;

        try
        {
            var file = await ApiClient.GenerateBulkLocationQrAsync(_bulkQrRequest);
            await TriggerAdminDownloadAsync(file);

            var failedCountHeader = file.GetHeader("X-SmartTour-Qr-Failed-Count");
            if (int.TryParse(failedCountHeader, out var failedCount) && failedCount > 0)
            {
                var failedIds = file.GetHeader("X-SmartTour-Qr-Failed-Ids") ?? "unknown";
                Notifications.Warning(
                    $"The ZIP was downloaded, but {failedCount} location(s) could not be exported. Failed IDs: {failedIds}.",
                    "Bulk QR completed with warnings",
                    durationMs: 5600);
            }
            else
            {
                Notifications.Success(
                    $"The QR ZIP for {selectedLocationIds.Count} location(s) is ready.",
                    "Bulk QR export complete");
            }

            _isBulkQrModalOpen = false;
        }
        catch (Exception ex)
        {
            Notifications.Error(ex.Message, "Bulk QR export failed");
        }
        finally
        {
            _isBulkQrExporting = false;
        }
    }

    private void TrimBulkQrSelection()
    {
        var accessibleIds = _locations
            .Select(item => item.Id)
            .ToHashSet();
        _selectedQrLocationIds.RemoveWhere(item => !accessibleIds.Contains(item));
    }

    private async Task TriggerAdminDownloadAsync(DownloadedAdminFile file)
    {
        var payload = Convert.ToBase64String(file.Content);
        await JS.InvokeVoidAsync("smartTourAdmin.download.base64", file.FileName, payload, file.ContentType);
    }
}
