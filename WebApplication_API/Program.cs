using Microsoft.Extensions.FileProviders;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using Project_SharedClassLibrary.Storage;
using WebApplication_API.Data;
using WebApplication_API.ModelBinding;
using WebApplication_API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddControllers(options =>
{
    options.ModelBinderProviders.Insert(0, new FlexibleDoubleModelBinderProvider());
});
builder.AddDataToDatabase();
builder.Services.AddSingleton<SharedAudioFileStorageService>();
builder.Services.AddSingleton<SharedImageFileStorageService>();
builder.Services.Configure<RoutePlanningOptions>(builder.Configuration.GetSection(RoutePlanningOptions.SectionName));
builder.Services.AddHttpClient<TtsPreviewService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient<WalkingRouteService>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<RoutePlanningOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.RequestTimeoutSeconds));
    client.DefaultRequestVersion = HttpVersion.Version11;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SmartTourismRoutePlanner/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    SslOptions = new SslClientAuthenticationOptions
    {
        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
    }
});
builder.Services.AddScoped<TourRoutePlanningService>();
builder.Services.AddSingleton<AdminSessionTokenService>();
builder.Services.AddScoped<AdminRequestAuthorizationService>();
builder.Services.AddScoped<ChangeRequestWorkflowService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor",
        policy => policy.WithOrigins(
                            "http://localhost:5229",
                            "https://localhost:7084")
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

var app = builder.Build();
var sharedAudioDirectory = SharedStoragePaths.GetAudioDirectory(app.Environment.ContentRootPath);
var sharedImageDirectory = SharedStoragePaths.GetImageDirectory(app.Environment.ContentRootPath);
Directory.CreateDirectory(sharedAudioDirectory);
Directory.CreateDirectory(sharedImageDirectory);

app.UseCors("AllowBlazor");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(sharedAudioDirectory),
    RequestPath = SharedStoragePaths.AudioRequestPath
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(sharedImageDirectory),
    RequestPath = SharedStoragePaths.ImageRequestPath
});
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
// app.UseHttpsRedirection();
app.MapControllers();
// app.MapLocationEndpoints();
// app.MapCategoryEndpoints();
// app.MapVoiceEndpoints();
app.MigrateDb();
app.Run();

