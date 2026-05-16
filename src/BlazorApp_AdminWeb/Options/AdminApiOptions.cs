namespace BlazorApp_AdminWeb.Options;

public sealed class AdminApiOptions
{
    public const string SectionName = "AdminApi";

    public string BaseUrl { get; set; } = " https://expletive-cried-decimeter.ngrok-free.dev/";
    public string BaseUrlNgrok { get; set; } = " https://expletive-cried-decimeter.ngrok-free.dev/";
    public string BaseUrlLocal { get; set; } = "http://localhost:5123/";
}

