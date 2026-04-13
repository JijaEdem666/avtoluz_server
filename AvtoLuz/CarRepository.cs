using System.Text;
using Dapper;
using Npgsql;
using AvtoLuz.Models;
using AvtoLuz.DTOs;

namespace AvtoLuz.Data;

/// <summary>
/// Репозиторий автомобилей.
/// Все запросы параметризованы — защита от SQL-инъекций.
/// Используется Dapper для маппинга строк PostgreSQL → объекты Car.
/// </summary>
public class CarRepository
{
    private readonly string _connectionString;

    // Whitelist сортировок — никогда не вставляем значения из запроса напрямую в SQL
    private static readonly Dictionary<string, string> SortMap = new()
    {
        ["id_asc"]      = "id ASC",
        ["price_asc"]   = "price ASC,  id ASC",
        ["price_desc"]  = "price DESC, id ASC",
        ["year_desc"]   = "year DESC,  id ASC",
        ["mileage_asc"] = "mileage ASC, id ASC",
    };

    public CarRepository(string connectionString)
    {
        _connectionString = connectionString;

        // Настройка маппинга: snake_case колонки PG → PascalCase свойства C#
        // Dapper использует DefaultTypeMap по умолчанию, нам нужен кастомный
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private NpgsqlConnection CreateConnection() =>
        new NpgsqlConnection(_connectionString);

    // ══════════════════════════════════════════════
    // GetAll — список с фильтрами, сортировкой, пагинацией
    // ══════════════════════════════════════════════

    public async Task<PagedResult<Car>> GetAllAsync(CarFilterParams p)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();
        int paramIndex = 1;

        // ── Фильтры ──────────────────────────────────────

        if (!string.IsNullOrWhiteSpace(p.Brand))
        {
            conditions.Add($"lower(brand) LIKE @p{paramIndex}");
            parameters.Add($"p{paramIndex++}", $"%{p.Brand.ToLower()}%");
        }
        if (!string.IsNullOrWhiteSpace(p.BodyType))
        {
            conditions.Add($"body_type = @p{paramIndex}::body_type");
            parameters.Add($"p{paramIndex++}", p.BodyType);
        }
        if (!string.IsNullOrWhiteSpace(p.FuelType))
        {
            conditions.Add($"fuel_type = @p{paramIndex}::fuel_type");
            parameters.Add($"p{paramIndex++}", p.FuelType);
        }
        if (!string.IsNullOrWhiteSpace(p.Transmission))
        {
            conditions.Add($"transmission = @p{paramIndex}::transmission_type");
            parameters.Add($"p{paramIndex++}", p.Transmission);
        }
        if (!string.IsNullOrWhiteSpace(p.Steering))
        {
            conditions.Add($"steering = @p{paramIndex}::steering_type");
            parameters.Add($"p{paramIndex++}", p.Steering);
        }
        if (!string.IsNullOrWhiteSpace(p.DriveType))
        {
            conditions.Add($"drive_type = @p{paramIndex}::drive_type");
            parameters.Add($"p{paramIndex++}", p.DriveType);
        }
        if (p.PriceMin.HasValue)
        {
            conditions.Add($"price >= @p{paramIndex}");
            parameters.Add($"p{paramIndex++}", p.PriceMin.Value);
        }
        if (p.PriceMax.HasValue)
        {
            conditions.Add($"price <= @p{paramIndex}");
            parameters.Add($"p{paramIndex++}", p.PriceMax.Value);
        }
        if (p.YearMin.HasValue)
        {
            conditions.Add($"year >= @p{paramIndex}");
            parameters.Add($"p{paramIndex++}", p.YearMin.Value);
        }
        if (p.YearMax.HasValue)
        {
            conditions.Add($"year <= @p{paramIndex}");
            parameters.Add($"p{paramIndex++}", p.YearMax.Value);
        }

        var where   = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : string.Empty;

        // ── Сортировка (только из whitelist) ─────────────

        var orderBy = SortMap.TryGetValue(p.Sort, out var sort)
            ? sort
            : SortMap["id_asc"];

        // ── Пагинация ─────────────────────────────────────

        var page  = Math.Max(1, p.Page);
        var limit = Math.Clamp(p.Limit, 1, 50);
        var offset = (page - 1) * limit;

        parameters.Add($"p{paramIndex}",     limit);
        parameters.Add($"p{paramIndex + 1}", offset);

        // ── Запросы ───────────────────────────────────────

        var countSql = $"SELECT COUNT(*)::int FROM cars {where}";

        var dataSql = $"""
            SELECT id, brand, model, year, mileage, price, color, steering,
                   description, image,
                   fuel_type, engine_description, engine_volume, power,
                   transmission, drive_type, acceleration, fuel_consumption,
                   body_type, doors, length_mm, width_mm, height_mm,
                   wheelbase_mm, trunk_volume,
                   climate_control, seat_adjustment, heated_seats, cruise_control,
                   multimedia, audio_system, leather_interior, sunroof,
                   airbags, camera, parking_sensors, abs, esp, sign_recognition,
                   created_at, updated_at
            FROM cars
            {where}
            ORDER BY {orderBy}
            LIMIT @p{paramIndex} OFFSET @p{paramIndex + 1}
            """;

        using var conn = CreateConnection();
        await conn.OpenAsync();

        var total = await conn.QuerySingleAsync<int>(countSql, parameters);
        var data  = await conn.QueryAsync<Car>(dataSql, parameters);

        return new PagedResult<Car>
        {
            Data  = data,
            Total = total,
            Page  = page,
            Pages = (int)Math.Ceiling((double)total / limit),
            Limit = limit,
        };
    }

    // ══════════════════════════════════════════════
    // GetById
    // ══════════════════════════════════════════════

    public async Task<Car?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM cars WHERE id = @Id";
        using var conn = CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Car>(sql, new { Id = id });
    }

    // ══════════════════════════════════════════════
    // Create
    // ══════════════════════════════════════════════

    public async Task<Car> CreateAsync(CarWriteDto dto)
    {
        // Динамически строим INSERT только по непустым полям
        var cols   = new List<string>();
        var vals   = new List<string>();
        var prms   = new DynamicParameters();

        void Add(string col, object? value, string? cast = null)
        {
            if (value is null) return;
            cols.Add(col);
            var placeholder = cast is not null ? $"@{col}::{cast}" : $"@{col}";
            vals.Add(placeholder);
            prms.Add(col, value);
        }

        Add("brand",              dto.Brand);
        Add("model",              dto.Model);
        Add("year",               dto.Year);
        Add("mileage",            dto.Mileage);
        Add("price",              dto.Price);
        Add("color",              dto.Color);
        Add("steering",           dto.Steering,     "steering_type");
        Add("description",        dto.Description);
        Add("image",              dto.Image);
        Add("fuel_type",          dto.FuelType,     "fuel_type");
        Add("engine_description", dto.EngineDescription);
        Add("engine_volume",      dto.EngineVolume);
        Add("power",              dto.Power);
        Add("transmission",       dto.Transmission,  "transmission_type");
        Add("drive_type",         dto.DriveType,     "drive_type");
        Add("acceleration",       dto.Acceleration);
        Add("fuel_consumption",   dto.FuelConsumption);
        Add("body_type",          dto.BodyType,      "body_type");
        Add("doors",              dto.Doors);
        Add("length_mm",          dto.LengthMm);
        Add("width_mm",           dto.WidthMm);
        Add("height_mm",          dto.HeightMm);
        Add("wheelbase_mm",       dto.WheelbaseMm);
        Add("trunk_volume",       dto.TrunkVolume);
        Add("climate_control",    dto.ClimateControl,  "climate_type");
        Add("seat_adjustment",    dto.SeatAdjustment,  "seat_adjustment_type");
        Add("heated_seats",       dto.HeatedSeats,     "heated_seats_type");
        Add("cruise_control",     dto.CruiseControl,   "cruise_type");
        Add("multimedia",         dto.Multimedia);
        Add("audio_system",       dto.AudioSystem);
        Add("leather_interior",   dto.LeatherInterior);
        Add("sunroof",            dto.Sunroof);
        Add("airbags",            dto.Airbags);
        Add("camera",             dto.Camera,           "camera_type");
        Add("parking_sensors",    dto.ParkingSensors,   "parking_sensors_type");
        Add("abs",                dto.Abs);
        Add("esp",                dto.Esp);
        Add("sign_recognition",   dto.SignRecognition);

        if (cols.Count == 0)
            throw new ArgumentException("Нет данных для записи");

        var sql = $"""
            INSERT INTO cars ({string.Join(", ", cols)})
            VALUES ({string.Join(", ", vals)})
            RETURNING *
            """;

        using var conn = CreateConnection();
        return await conn.QuerySingleAsync<Car>(sql, prms);
    }

    // ══════════════════════════════════════════════
    // Update
    // ══════════════════════════════════════════════

    public async Task<Car?> UpdateAsync(int id, CarWriteDto dto)
    {
        var sets = new List<string>();
        var prms = new DynamicParameters();

        void Add(string col, object? value, string? cast = null)
        {
            if (value is null) return;
            var placeholder = cast is not null ? $"@{col}::{cast}" : $"@{col}";
            sets.Add($"{col} = {placeholder}");
            prms.Add(col, value);
        }

        Add("brand",              dto.Brand);
        Add("model",              dto.Model);
        Add("year",               dto.Year);
        Add("mileage",            dto.Mileage);
        Add("price",              dto.Price);
        Add("color",              dto.Color);
        Add("steering",           dto.Steering,     "steering_type");
        Add("description",        dto.Description);
        Add("image",              dto.Image);
        Add("fuel_type",          dto.FuelType,     "fuel_type");
        Add("engine_description", dto.EngineDescription);
        Add("engine_volume",      dto.EngineVolume);
        Add("power",              dto.Power);
        Add("transmission",       dto.Transmission, "transmission_type");
        Add("drive_type",         dto.DriveType,    "drive_type");
        Add("acceleration",       dto.Acceleration);
        Add("fuel_consumption",   dto.FuelConsumption);
        Add("body_type",          dto.BodyType,     "body_type");
        Add("doors",              dto.Doors);
        Add("length_mm",          dto.LengthMm);
        Add("width_mm",           dto.WidthMm);
        Add("height_mm",          dto.HeightMm);
        Add("wheelbase_mm",       dto.WheelbaseMm);
        Add("trunk_volume",       dto.TrunkVolume);
        Add("climate_control",    dto.ClimateControl, "climate_type");
        Add("seat_adjustment",    dto.SeatAdjustment, "seat_adjustment_type");
        Add("heated_seats",       dto.HeatedSeats,    "heated_seats_type");
        Add("cruise_control",     dto.CruiseControl,  "cruise_type");
        Add("multimedia",         dto.Multimedia);
        Add("audio_system",       dto.AudioSystem);
        Add("leather_interior",   dto.LeatherInterior);
        Add("sunroof",            dto.Sunroof);
        Add("airbags",            dto.Airbags);
        Add("camera",             dto.Camera,          "camera_type");
        Add("parking_sensors",    dto.ParkingSensors,  "parking_sensors_type");
        Add("abs",                dto.Abs);
        Add("esp",                dto.Esp);
        Add("sign_recognition",   dto.SignRecognition);

        if (sets.Count == 0)
            throw new ArgumentException("Нет данных для обновления");

        prms.Add("id", id);

        var sql = $"""
            UPDATE cars
            SET {string.Join(", ", sets)}
            WHERE id = @id
            RETURNING *
            """;

        using var conn = CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Car>(sql, prms);
    }

    // ══════════════════════════════════════════════
    // Delete
    // ══════════════════════════════════════════════

    public async Task<Car?> DeleteAsync(int id)
    {
        const string sql = "DELETE FROM cars WHERE id = @Id RETURNING *";
        using var conn = CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Car>(sql, new { Id = id });
    }

    // ══════════════════════════════════════════════
    // Ping — проверка соединения
    // ══════════════════════════════════════════════

    public async Task PingAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.QuerySingleAsync<int>("SELECT 1");
    }
}

public class FavouriteRepository
{
    private readonly string _connectionString;

    public FavouriteRepository(string connectionString)
    {
        _connectionString = connectionString;
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private NpgsqlConnection CreateConnection() =>
        new NpgsqlConnection(_connectionString);

    // ══════════════════════════════════════════════
    // Список избранных автомобилей для сессии
    // Возвращает полные данные автомобиля через JOIN
    // ══════════════════════════════════════════════

    public async Task<IEnumerable<Car>> GetBySessionAsync(string sessionId)
    {
        const string sql = """
            SELECT c.*
            FROM   favourites f
            JOIN   cars c ON c.id = f.car_id
            WHERE  f.session_id = @SessionId
            ORDER  BY f.added_at DESC
            """;

        using var conn = CreateConnection();
        return await conn.QueryAsync<Car>(sql, new { SessionId = sessionId });
    }

    // ══════════════════════════════════════════════
    // Добавить в избранное
    // Если запись уже есть — ничего не делаем (ON CONFLICT DO NOTHING)
    // ══════════════════════════════════════════════

    public async Task<bool> AddAsync(string sessionId, int carId)
    {
        const string sql = """
            INSERT INTO favourites (car_id, session_id)
            VALUES (@CarId, @SessionId)
            ON CONFLICT (car_id, session_id) DO NOTHING
            """;

        using var conn = CreateConnection();
        var affected = await conn.ExecuteAsync(sql, new { CarId = carId, SessionId = sessionId });
        return affected > 0;   // false = уже было в избранном
    }

    // ══════════════════════════════════════════════
    // Удалить из избранного
    // ══════════════════════════════════════════════

    public async Task<bool> RemoveAsync(string sessionId, int carId)
    {
        const string sql = """
            DELETE FROM favourites
            WHERE session_id = @SessionId AND car_id = @CarId
            """;

        using var conn = CreateConnection();
        var affected = await conn.ExecuteAsync(sql, new { SessionId = sessionId, CarId = carId });
        return affected > 0;
    }

    // ══════════════════════════════════════════════
    // Проверить: есть ли авто в избранном у сессии
    // ══════════════════════════════════════════════

    public async Task<bool> ExistsAsync(string sessionId, int carId)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM favourites
                WHERE session_id = @SessionId AND car_id = @CarId
            )
            """;

        using var conn = CreateConnection();
        return await conn.QuerySingleAsync<bool>(sql, new { SessionId = sessionId, CarId = carId });
    }

    // ══════════════════════════════════════════════
    // Получить множество id избранных авто для сессии
    // (используется для подсветки кнопок ♥ в каталоге)
    // ══════════════════════════════════════════════

    public async Task<IEnumerable<int>> GetCarIdsAsync(string sessionId)
    {
        const string sql = """
            SELECT car_id FROM favourites
            WHERE  session_id = @SessionId
            """;

        using var conn = CreateConnection();
        return await conn.QueryAsync<int>(sql, new { SessionId = sessionId });
    }
}

