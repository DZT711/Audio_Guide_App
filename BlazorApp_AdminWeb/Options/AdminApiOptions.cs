namespace BlazorApp_AdminWeb.Options;

public sealed class AdminApiOptions
{
    public const string SectionName = "AdminApi";

    public string BaseUrl { get; set; } = "https://flashy-foothill-posting.ngrok-free.dev/";
    public string BaseUrlNgrok { get; set; } = "https://retype-roundworm-platter.ngrok-free.dev/";
    public string BaseUrlLocal { get; set; } = "http://localhost:5123/";
}
