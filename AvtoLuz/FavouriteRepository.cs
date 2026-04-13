using Dapper;
using Npgsql;
using AvtoLuz.Models;

namespace AvtoLuz.Fv;

/// <summary>
/// Репозиторий избранного.
/// Идентификатор пользователя — session_id, который клиент хранит в localStorage
/// и передаёт в заголовке X-Session-Id при каждом запросе.
/// Это простой способ без авторизации: у каждого браузера своё избранное.
/// </summary>
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

        using var conn  = CreateConnection();
        var affected    = await conn.ExecuteAsync(sql, new { CarId = carId, SessionId = sessionId });
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
        var affected   = await conn.ExecuteAsync(sql, new { SessionId = sessionId, CarId = carId });
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
