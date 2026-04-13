using System.Text.Json.Serialization;

namespace AvtoLuz.Models;

/// <summary>
/// Модель автомобиля — отражает таблицу cars в PostgreSQL.
/// Имена свойств: PascalCase в C#, сериализуются в camelCase для JSON-клиента.
/// </summary>
public class Car
{
    // ── Идентификатор ─────────────────────────────────────
    public int Id { get; set; }

    // ── Основная информация ───────────────────────────────
    public string Brand       { get; set; } = string.Empty;
    public string Model       { get; set; } = string.Empty;
    public short  Year        { get; set; }
    public int    Mileage     { get; set; }
    public int    Price       { get; set; }
    public string Color       { get; set; } = string.Empty;
    public string Steering    { get; set; } = "left";
    public string? Description { get; set; }
    public string? Image       { get; set; }

    // ── Двигатель и трансмиссия ───────────────────────────
    public string  FuelType           { get; set; } = string.Empty;
    public string? EngineDescription  { get; set; }
    public decimal? EngineVolume      { get; set; }
    public short?   Power             { get; set; }
    public string   Transmission      { get; set; } = string.Empty;
    public string   DriveType         { get; set; } = string.Empty;
    public decimal? Acceleration      { get; set; }
    public decimal? FuelConsumption   { get; set; }

    // ── Кузов и габариты ─────────────────────────────────
    public string  BodyType      { get; set; } = string.Empty;
    public short?  Doors         { get; set; }
    public short?  LengthMm      { get; set; }
    public short?  WidthMm       { get; set; }
    public short?  HeightMm      { get; set; }
    public short?  WheelbaseMm   { get; set; }
    public short?  TrunkVolume   { get; set; }

    // ── Комфорт ───────────────────────────────────────────
    public string? ClimateControl  { get; set; }
    public string? SeatAdjustment  { get; set; }
    public string? HeatedSeats     { get; set; }
    public string? CruiseControl   { get; set; }
    public string? Multimedia      { get; set; }
    public string? AudioSystem     { get; set; }
    public bool    LeatherInterior { get; set; }
    public bool    Sunroof         { get; set; }

    // ── Безопасность ──────────────────────────────────────
    public short?  Airbags         { get; set; }
    public string? Camera          { get; set; }
    public string? ParkingSensors  { get; set; }
    public bool    Abs             { get; set; }
    public bool    Esp             { get; set; }
    public bool    SignRecognition { get; set; }

    // ── Метаданные ────────────────────────────────────────
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
