namespace BlazorApp_AdminWeb.Options;

public sealed class AdminApiOptions
{
    public const string SectionName = "AdminApi";

    public string BaseUrl { get; set; } = "http://localhost:5123/";
}
