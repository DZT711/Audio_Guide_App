using Microsoft.AspNetCore.DataProtection;
using BlazorApp_AdminWeb.Components;
using BlazorApp_AdminWeb.Options;
using BlazorApp_AdminWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dataProtectionKeyPath = Path.Combine(
    Path.GetTempPath(),
    "SmartTourismAdminWeb",
    "DataProtection-Keys");

builder.Services.AddDataProtection()
    .SetApplicationName("SmartTourismAdminWeb")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));

builder.Services.Configure<AdminApiOptions>(builder.Configuration.GetSection(AdminApiOptions.SectionName));
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AdminSessionState>();
builder.Services.AddScoped<AdminShellState>();
builder.Services.AddScoped<InboxState>();
builder.Services.AddScoped<ModerationState>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddHttpClient<AdminApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminApiOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
