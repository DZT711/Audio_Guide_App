using Microsoft.Maui.Devices;

namespace MauiApp_Mobile.Services;

public static class MobileApiOptions
{
    public static string BaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5123/"
            : "http://localhost:5123/";
}
