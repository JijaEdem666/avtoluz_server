using System.ComponentModel.DataAnnotations;

namespace AvtoLuz.DTOs;

// ──────────────────────────────────────────────────────
// Параметры фильтрации и пагинации (query-строка)
// ──────────────────────────────────────────────────────

public class CarFilterParams
{
    public string? Brand { get; set; }
    public string? BodyType { get; set; }
    public string? FuelType { get; set; }
    public string? Transmission { get; set; }
    public string? Steering { get; set; }
    public string? DriveType { get; set; }

    public int? PriceMin { get; set; }
    public int? PriceMax { get; set; }
    public int? YearMin { get; set; }
    public int? YearMax { get; set; }

    public string Sort { get; set; } = "id_asc";
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 6;
}

// ──────────────────────────────────────────────────────
// Постраничный ответ
// ──────────────────────────────────────────────────────

public class PagedResult<T>
{
    public IEnumerable<T> Data { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int Pages { get; set; }
    public int Limit { get; set; }
}

// ──────────────────────────────────────────────────────
// DTO создания / обновления автомобиля
// Серверная валидация через DataAnnotations
// ──────────────────────────────────────────────────────

public class CarWriteDto
{
    // ── Обязательные поля ──────────────────────────────

    [Required(ErrorMessage = "Марка обязательна")]
    [StringLength(50, MinimumLength = 2,
        ErrorMessage = "Марка: от 2 до 50 символов")]
    [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ0-9][a-zA-Zа-яА-ЯёЁ0-9\s\-]*[a-zA-Zа-яА-ЯёЁ0-9]$|^[a-zA-Zа-яА-ЯёЁ0-9]$",
        ErrorMessage = "Марка содержит недопустимые символы")]
    public string? Brand { get; set; }

    [Required(ErrorMessage = "Модель обязательна")]
    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "Модель: от 1 до 100 символов")]
    public string? Model { get; set; }

    [Required(ErrorMessage = "Год выпуска обязателен")]
    [Range(1990, 2025, ErrorMessage = "Год: от 1990 до 2025")]
    public short? Year { get; set; }

    [Required(ErrorMessage = "Пробег обязателен")]
    [Range(0, 1_000_000, ErrorMessage = "Пробег: от 0 до 1 000 000 км")]
    public int? Mileage { get; set; }

    [Required(ErrorMessage = "Цена обязательна")]
    [Range(1, 999_999_999, ErrorMessage = "Цена: от 1 до 999 999 999 руб")]
    public int? Price { get; set; }

    [Required(ErrorMessage = "Цвет обязателен")]
    [StringLength(50, MinimumLength = 2,
        ErrorMessage = "Цвет: от 2 до 50 символов")]
    public string? Color { get; set; }

    // Steering — клиент не передаёт (нет поля в форме), сервер ставит дефолт
    public string? Steering { get; set; } = "left";

    [Required(ErrorMessage = "Тип топлива обязателен")]
    [RegularExpression("^(petrol|diesel|hybrid|electric|gas)$",
        ErrorMessage = "Недопустимый тип топлива")]
    public string? FuelType { get; set; }

    [Required(ErrorMessage = "Коробка передач обязательна")]
    [RegularExpression("^(automatic|manual|robot|variator)$",
        ErrorMessage = "Недопустимый тип КПП")]
    public string? Transmission { get; set; }

    [Required(ErrorMessage = "Тип привода обязателен")]
    [RegularExpression("^(front|rear|all)$",
        ErrorMessage = "Недопустимый тип привода")]
    public string? DriveType { get; set; }

    [Required(ErrorMessage = "Тип кузова обязателен")]
    [RegularExpression("^(sedan|hatchback|suv|coupe|wagon|minivan|convertible|pickup)$",
        ErrorMessage = "Недопустимый тип кузова")]
    public string? BodyType { get; set; }

    [Required(ErrorMessage = "Описание обязательно")]
    [StringLength(2000, MinimumLength = 10,
        ErrorMessage = "Описание: от 10 до 2000 символов")]
    public string? Description { get; set; }

    // ── Двигатель ─────────────────────────────────────

    // engine заполняется автоматически на клиенте, но валидируется на сервере
    [StringLength(100, MinimumLength = 5,
        ErrorMessage = "Описание двигателя: от 5 до 100 символов")]
    public string? EngineDescription { get; set; }

    [Range(0.8, 8.0, ErrorMessage = "Объём двигателя: от 0.8 до 8.0 л")]
    public decimal? EngineVolume { get; set; }

    [Range(50, 2000, ErrorMessage = "Мощность: от 50 до 2000 л.с.")]
    public short? Power { get; set; }

    [Range(1.0, 20.0, ErrorMessage = "Разгон: от 1 до 20 сек")]
    public decimal? Acceleration { get; set; }

    [Range(0.0, 30.0, ErrorMessage = "Расход топлива: от 0 до 30 л/100км")]
    public decimal? FuelConsumption { get; set; }

    // ── Кузов ─────────────────────────────────────────

    public string? Image { get; set; }

    [Range(2, 6, ErrorMessage = "Количество дверей: от 2 до 6")]
    public short? Doors { get; set; }

    [Range(1000, 10000, ErrorMessage = "Длина: от 1000 до 10000 мм")]
    public short? LengthMm { get; set; }

    [Range(1000, 3000, ErrorMessage = "Ширина: от 1000 до 3000 мм")]
    public short? WidthMm { get; set; }

    [Range(1000, 3000, ErrorMessage = "Высота: от 1000 до 3000 мм")]
    public short? HeightMm { get; set; }

    [Range(1000, 4000, ErrorMessage = "Колёсная база: от 1000 до 4000 мм")]
    public short? WheelbaseMm { get; set; }

    [Range(100, 5000, ErrorMessage = "Объём багажника: от 100 до 5000 л")]
    public short? TrunkVolume { get; set; }

    // ── Комфорт ───────────────────────────────────────

    [RegularExpression("^(none|1-zone|2-zone|3-zone|4-zone)$",
        ErrorMessage = "Недопустимое значение климат-контроля")]
    public string? ClimateControl { get; set; }

    [RegularExpression("^(none|driver|driver_passenger)$",
        ErrorMessage = "Недопустимое значение регулировки сидений")]
    public string? SeatAdjustment { get; set; }

    [RegularExpression("^(none|front|rear|front_rear)$",
        ErrorMessage = "Недопустимое значение подогрева сидений")]
    public string? HeatedSeats { get; set; }

    [RegularExpression("^(none|regular|adaptive)$",
        ErrorMessage = "Недопустимое значение круиз-контроля")]
    public string? CruiseControl { get; set; }

    [StringLength(100, ErrorMessage = "Мультимедиа: до 100 символов")]
    public string? Multimedia { get; set; }

    [StringLength(100, ErrorMessage = "Аудиосистема: до 100 символов")]
    public string? AudioSystem { get; set; }

    public bool? LeatherInterior { get; set; }
    public bool? Sunroof { get; set; }

    // ── Безопасность ──────────────────────────────────

    [Range(0, 20, ErrorMessage = "Подушки безопасности: от 0 до 20")]
    public short? Airbags { get; set; }

    [RegularExpression("^(none|rear|surround)$",
        ErrorMessage = "Недопустимое значение камеры")]
    public string? Camera { get; set; }

    [RegularExpression("^(none|front|rear|front_rear)$",
        ErrorMessage = "Недопустимое значение парктроников")]
    public string? ParkingSensors { get; set; }

    public bool? Abs { get; set; }
    public bool? Esp { get; set; }
    public bool? SignRecognition { get; set; }
}

// ──────────────────────────────────────────────────────
// Ответ с ошибкой
// ──────────────────────────────────────────────────────

public record ErrorResponse(string Error);

// ──────────────────────────────────────────────────────
// Ответ с ошибками валидации (поле → список ошибок)
// ──────────────────────────────────────────────────────

public record ValidationErrorResponse(string Error, Dictionary<string, string[]> Fields);