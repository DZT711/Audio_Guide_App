
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
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
builder.Services.AddSingleton<ManagedMediaArchiveMigrationService>();
builder.Services.AddSingleton<SharedAudioFileStorageService>();
builder.Services.AddSingleton<SharedImageFileStorageService>();
builder.Services.Configure<RoutePlanningOptions>(builder.Configuration.GetSection(RoutePlanningOptions.SectionName));
builder.Services.Configure<GeminiSpeechOptions>(builder.Configuration.GetSection(GeminiSpeechOptions.SectionName));
builder.Services.Configure<QrLinkOptions>(builder.Configuration.GetSection(QrLinkOptions.SectionName));
builder.Services.AddHttpClient<TtsPreviewService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient<GeminiSpeechService>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiSpeechOptions>>()
        .Value;

    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(10, options.TimeoutSeconds));
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
builder.Services.AddSingleton<ServerRuntimeInfoService>();
builder.Services.AddScoped<AdminRequestAuthorizationService>();
builder.Services.AddScoped<ChangeRequestWorkflowService>();
builder.Services.AddScoped<ActivityLogService>();
builder.Services.AddSingleton<AnalyticsDataFilterService>();
builder.Services.AddSingleton<LocationQrService>();
builder.Services.AddSingleton<AndroidApkPackagingService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor",
        policy => policy.WithOrigins(
                            "http://localhost:5229",
                            "https://localhost:7084")
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
});

var app = builder.Build();
var mediaArchiveMigration = app.Services.GetRequiredService<ManagedMediaArchiveMigrationService>();
mediaArchiveMigration.EnsureArchiveIsReady();
var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".wav"] = "audio/wav";
contentTypeProvider.Mappings[".mp3"] = "audio/mpeg";
contentTypeProvider.Mappings[".ogg"] = "audio/ogg";
contentTypeProvider.Mappings[".jfif"] = "image/jpeg";
contentTypeProvider.Mappings[".bmp"] = "image/bmp";

app.UseCors("AllowBlazor");
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(SharedStoragePaths.GetArchiveRoot(app.Environment.ContentRootPath)),
    RequestPath = SharedStoragePaths.ArchiveFolderName.StartsWith("/")
        ? SharedStoragePaths.ArchiveFolderName
        : $"/{SharedStoragePaths.ArchiveFolderName}",
    ContentTypeProvider = contentTypeProvider
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
