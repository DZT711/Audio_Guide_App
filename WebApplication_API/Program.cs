using WebApplication_API.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddControllers();
builder.AddDataToDatabase();

// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("AllowBlazor",
//         policy => policy.WithOrigins("http://localhost:5229") // Đúng Port của FE bạn gửi
//                         .AllowAnyMethod()
//                         .AllowAnyHeader());
// });

var app = builder.Build();
// app.UseCors("AllowBlazor");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
// app.UseHttpsRedirection();
app.MapControllers();
// app.MapUserEndpoints();
// app.MapRolesEndpoints();
app.MigrateDb();
app.Run();

