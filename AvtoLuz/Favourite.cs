namespace AvtoLuz.Models;

/// <summary>
/// Запись избранного — связь session_id ↔ car_id.
/// </summary>
public class Favourite
{
    public int      Id        { get; set; }
    public int      CarId     { get; set; }
    public string   SessionId { get; set; } = string.Empty;
    public DateTime AddedAt   { get; set; }

    /// <summary>
    /// Данные автомобиля — заполняются JOIN-ом при выборке списка.
    /// </summary>
    public Car? Car { get; set; }
}
