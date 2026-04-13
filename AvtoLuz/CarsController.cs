using Microsoft.AspNetCore.Mvc;
using AvtoLuz.Data;
using AvtoLuz.DTOs;
using Npgsql;

namespace AvtoLuz.Controllers;


[ApiController]
public class CarsController : ControllerBase
{
    private readonly CarRepository _cars;
    private readonly FavouriteRepository _favs;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CarsController> _logger;

    // Разрешённые MIME-типы для загрузки изображений
    private static readonly HashSet<string> _allowedMime =
        ["image/jpeg", "image/png", "image/webp"];

    // Максимальный размер одного файла — 10 МБ
    private const long MaxFileSize = 10 * 1024 * 1024;

    public CarsController(
        CarRepository cars,
        FavouriteRepository favs,
        IWebHostEnvironment env,
        ILogger<CarsController> logger)
    {
        _cars = cars;
        _favs = favs;
        _env = env;
        _logger = logger;
    }

    // ══════════════════════════════════════════════
    // IMAGES — POST /api/images
    // Принимает multipart/form-data, поле "files" (1–10 файлов).
    // Сохраняет в wwwroot/images/ с уникальным именем.
    // Возвращает: { uploaded: ["images/имя1.jpg", "images/имя2.jpg"] }
    // ══════════════════════════════════════════════

    [HttpPost("api/images")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100 МБ на весь запрос
    public async Task<IActionResult> UploadImages(List<IFormFile> files)
    {
        if (files is null || files.Count == 0)
            return BadRequest(new ErrorResponse("Не переданы файлы (поле 'files')"));

        if (files.Count > 10)
            return BadRequest(new ErrorResponse("Максимум 10 файлов за раз"));

        // Серверная проверка каждого файла —────────────────────────────
        foreach (var file in files)
        {
            if (file.Length == 0)
                return BadRequest(new ErrorResponse($"Файл '{file.FileName}' пуст"));

            if (file.Length > MaxFileSize)
                return BadRequest(new ErrorResponse(
                    $"Файл '{file.FileName}' превышает 10 МБ"));

            if (!_allowedMime.Contains(file.ContentType.ToLower()))
                return BadRequest(new ErrorResponse(
                    $"Недопустимый тип файла '{file.ContentType}'. Разрешены: JPG, PNG, WEBP"));
        }

        // Папка wwwroot/images/ — WebRootPath может быть null если wwwroot не создан,
        // поэтому fallback на ContentRootPath/wwwroot
        var webRoot = _env.WebRootPath
                        ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var imagesDir = Path.Combine(webRoot, "images");
        Directory.CreateDirectory(imagesDir);

        var uploaded = new List<string>();

        foreach (var file in files)
        {
            // Генерируем уникальное имя: безопасный_стем + 8 hex символов + расширение
            var ext = Path.GetExtension(file.FileName).ToLower(); // .jpg
            var stem = Path.GetFileNameWithoutExtension(file.FileName);
            var safeStem = string.Concat(stem
                .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                .ToLower();
            if (safeStem.Length == 0) safeStem = "image";
            if (safeStem.Length > 40) safeStem = safeStem[..40];

            // Guid.NewGuid().ToString("N") даёт 32 hex-символа; берём первые 8
            var uniqueSuffix = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8);
            var uniqueName = $"{safeStem}_{uniqueSuffix}{ext}"; // напр. bmw_x5_a1b2c3d4.jpg
            var fullPath = Path.Combine(imagesDir, uniqueName);

            await using var stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream);

            // Путь, который будет храниться в БД и использоваться в <img src="">
            uploaded.Add($"images/{uniqueName}");

            _logger.LogInformation("Загружен файл: {Name} ({Size} байт)", uniqueName, file.Length);
        }

        return Ok(new { uploaded });
    }

    // ══════════════════════════════════════════════
    // CARS — /api/cars
    // ══════════════════════════════════════════════

    [HttpGet("api/cars")]
    public async Task<IActionResult> GetAll([FromQuery] CarFilterParams filters)
    {
        try { return Ok(await _cars.GetAllAsync(filters)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка GetAll");
            return StatusCode(500, new ErrorResponse(MapError(ex)));
        }
    }

    [HttpGet("api/cars/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var car = await _cars.GetByIdAsync(id);
            return car is null
                ? NotFound(new ErrorResponse("Автомобиль не найден"))
                : Ok(car);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка GetById id={Id}", id);
            return StatusCode(500, new ErrorResponse(MapError(ex)));
        }
    }

    [HttpPost("api/cars")]
    public async Task<IActionResult> Create([FromBody] CarWriteDto dto)
    {
        var errors = ValidateRequired(dto);
        if (errors.Count > 0)
            return BadRequest(new { error = "Ошибка валидации", fields = errors });
        try
        {
            var car = await _cars.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = car.Id }, car);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка Create");
            return StatusCode(500, new ErrorResponse(MapError(ex)));
        }
    }

    [HttpPut("api/cars/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CarWriteDto dto)
    {
        try
        {
            var car = await _cars.UpdateAsync(id, dto);
            return car is null
                ? NotFound(new ErrorResponse("Автомобиль не найден"))
                : Ok(car);
        }
        catch (ArgumentException ex) { return BadRequest(new ErrorResponse(ex.Message)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка Update id={Id}", id);
            return StatusCode(500, new ErrorResponse(MapError(ex)));
        }
    }

    [HttpDelete("api/cars/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var car = await _cars.DeleteAsync(id);
            return car is null
                ? NotFound(new ErrorResponse("Автомобиль не найден"))
                : Ok(new { deleted = car });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка Delete id={Id}", id);
            return StatusCode(500, new ErrorResponse(MapError(ex)));
        }
    }

    // ══════════════════════════════════════════════
    // FAVOURITES — /api/favourites
    // Session ID передаётся query-параметром ?sid=
    // ══════════════════════════════════════════════

    [HttpGet("api/favourites")]
    public async Task<IActionResult> FavGetAll([FromQuery] string? sid)
    {
        if (string.IsNullOrWhiteSpace(sid))
            return BadRequest(new ErrorResponse("Параметр sid обязателен"));
        try
        {
            var cars = await _favs.GetBySessionAsync(sid);
            return Ok(new { data = cars, total = cars.Count() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка FavGetAll");
            return StatusCode(500, new ErrorResponse(ex.Message));
        }
    }

    [HttpGet("api/favourites/ids")]
    public async Task<IActionResult> FavGetIds([FromQuery] string? sid)
    {
        if (string.IsNullOrWhiteSpace(sid))
            return BadRequest(new ErrorResponse("Параметр sid обязателен"));
        try
        {
            return Ok(await _favs.GetCarIdsAsync(sid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка FavGetIds");
            return StatusCode(500, new ErrorResponse(ex.Message));
        }
    }

    [HttpPost("api/favourites/{carId:int}")]
    public async Task<IActionResult> FavAdd(int carId, [FromQuery] string? sid)
    {
        if (string.IsNullOrWhiteSpace(sid))
            return BadRequest(new ErrorResponse("Параметр sid обязателен"));
        try
        {
            var added = await _favs.AddAsync(sid, carId);
            return Ok(new { added, carId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка FavAdd carId={CarId}", carId);
            return StatusCode(500, new ErrorResponse(ex.Message));
        }
    }

    [HttpDelete("api/favourites/{carId:int}")]
    public async Task<IActionResult> FavRemove(int carId, [FromQuery] string? sid)
    {
        if (string.IsNullOrWhiteSpace(sid))
            return BadRequest(new ErrorResponse("Параметр sid обязателен"));
        try
        {
            var removed = await _favs.RemoveAsync(sid, carId);
            return removed
                ? Ok(new { removed = true, carId })
                : NotFound(new ErrorResponse("Автомобиль не найден в избранном"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка FavRemove carId={CarId}", carId);
            return StatusCode(500, new ErrorResponse(ex.Message));
        }
    }

    // ══════════════════════════════════════════════
    // Вспомогательные методы
    // ══════════════════════════════════════════════

    private static List<string> ValidateRequired(CarWriteDto dto)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.Brand)) e.Add("brand обязателен");
        if (string.IsNullOrWhiteSpace(dto.Model)) e.Add("model обязателен");
        if (dto.Year is null) e.Add("year обязателен");
        if (dto.Mileage is null) e.Add("mileage обязателен");
        if (dto.Price is null) e.Add("price обязателен");
        if (string.IsNullOrWhiteSpace(dto.Color)) e.Add("color обязателен");
        if (string.IsNullOrWhiteSpace(dto.FuelType)) e.Add("fuelType обязателен");
        if (string.IsNullOrWhiteSpace(dto.Transmission)) e.Add("transmission обязателен");
        if (string.IsNullOrWhiteSpace(dto.DriveType)) e.Add("driveType обязателен");
        if (string.IsNullOrWhiteSpace(dto.BodyType)) e.Add("bodyType обязателен");
        return e;
    }

    private static string MapError(Exception ex) =>
        ex is PostgresException pg ? pg.SqlState switch
        {
            "23502" => "Обязательное поле не заполнено",
            "23514" => "Нарушение ограничения: недопустимое значение",
            "22P02" => "Недопустимый тип данных",
            "23505" => "Запись с такими данными уже существует",
            _ => pg.MessageText
        } : ex.Message;
}