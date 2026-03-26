using Microsoft.Extensions.FileProviders;
using Project_SharedClassLibrary.Storage;
using WebApplication_API.Data;
using WebApplication_API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddControllers();
builder.AddDataToDatabase();
builder.Services.AddSingleton<SharedAudioFileStorageService>();
builder.Services.AddSingleton<AdminSessionTokenService>();
builder.Services.AddScoped<AdminRequestAuthorizationService>();

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
Directory.CreateDirectory(sharedAudioDirectory);

app.UseCors("AllowBlazor");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(sharedAudioDirectory),
    RequestPath = SharedStoragePaths.AudioRequestPath
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

