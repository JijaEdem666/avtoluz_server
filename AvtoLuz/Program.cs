using System.Text.Json;
using System.Text.Json.Serialization;
using AvtoLuz.Data;
using AvtoLuz.DTOs;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Render передаёт строку подключения через переменную окружения DATABASE_URL
// в формате postgres://user:password@host:port/dbname
// Если переменной нет — берём из appsettings.json (локальная разработка)
var connectionString = ConvertDatabaseUrl(Environment.GetEnvironmentVariable("DATABASE_URL"))
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Строка подключения не найдена. Укажите DATABASE_URL или DefaultConnection в appsettings.json.");

// Конвертирует postgres://user:pass@host:port/db → Host=...;Username=...;Password=...;Database=...
static string? ConvertDatabaseUrl(string? url)
{
    if (string.IsNullOrWhiteSpace(url)) return null;
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':');
    return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};" +
           $"Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}

builder.Services.AddSingleton(new CarRepository(connectionString));
builder.Services.AddSingleton(new FavouriteRepository(connectionString));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
    });

// Заменяем стандартный 400-ответ [ApiController] на наш ValidationErrorResponse
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ctx =>
    {
        var fields = ctx.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => char.ToLower(e.Key[0]) + e.Key[1..], // camelCase ключ
                e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray()
            );
        var response = new ValidationErrorResponse("Ошибка валидации", fields);
        return new BadRequestObjectResult(response);
    };
});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// Проверка соединения с PostgreSQL
try
{
    await app.Services.GetRequiredService<CarRepository>().PingAsync();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✅  PostgreSQL: соединение установлено");
    Console.ResetColor();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"❌  PostgreSQL недоступен: {ex.Message}");
    Console.ResetColor();
    Environment.Exit(1);
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();   // wwwroot/ — отдаёт только реальные файлы по точному имени
app.MapControllers();   // /api/*

app.Run($"http://localhost:{builder.Configuration.GetValue<int>("App:Port", 5000)}");