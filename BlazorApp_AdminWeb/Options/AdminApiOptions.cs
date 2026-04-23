namespace BlazorApp_AdminWeb.Options;

public sealed class AdminApiOptions
{
    public const string SectionName = "AdminApi";

    public string BaseUrl { get; set; } = "https://flirt-zeppelin-dimness.ngrok-free.dev/";
}
