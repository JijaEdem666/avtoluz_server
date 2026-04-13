# АвтоЛуз — C# ASP.NET Core + PostgreSQL

## Стек

| Компонент    | Технология                          |
|--------------|-------------------------------------|
| Язык         | C# 12 / .NET 8                      |
| Фреймворк    | ASP.NET Core (Minimal API + Controllers) |
| БД           | PostgreSQL                          |
| Драйвер PG   | Npgsql 8                            |
| Micro-ORM    | Dapper 2 (маппинг строк → объекты)  |
| Фронтенд     | Статика из `wwwroot/` (catalogue.html) |

---

## Структура проекта

```
AvtoLuz/
├── Program.cs                  ← Точка входа, DI, middleware, запуск
├── AvtoLuz.csproj              ← Зависимости: Npgsql + Dapper
├── appsettings.json            ← Строка подключения к PostgreSQL, порт
│
├── Controllers/
│   └── CarsController.cs       ← REST API: GET/POST/PUT/DELETE /api/cars
│
├── Data/
│   └── CarRepository.cs        ← Все SQL-запросы (параметризованные)
│
├── Models/
│   └── Car.cs                  ← Модель таблицы cars
│
├── DTOs/
│   └── CarDtos.cs              ← CarFilterParams, CarWriteDto, PagedResult<T>
│
└── wwwroot/                    ← Статические файлы (сюда кладём catalogue.html)
    ├── catalogue.html
    ├── style.css
    ├── style_catalogue.css
    └── *.jpg / *.png
```

---

## Быстрый старт

### 1. Требования
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PostgreSQL 14+

### 2. Подготовить БД
```bash
# Создать базу данных
psql -U postgres -c "CREATE DATABASE avtoluz;"

# Применить схему и загрузить данные
psql -U postgres -d avtoluz -f schema.sql
psql -U postgres -d avtoluz -f seed.sql
```

### 3. Настроить подключение
Открыть `appsettings.json` и вписать пароль:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=avtoluz;Username=postgres;Password=ВАШ_ПАРОЛЬ"
  }
}
```

### 4. Скопировать статику
```bash
cp catalogue.html AvtoLuz/wwwroot/
cp style.css      AvtoLuz/wwwroot/
cp *.jpg *.png    AvtoLuz/wwwroot/   # изображения автомобилей
```

### 5. Запустить сервер
```bash
cd AvtoLuz
dotnet run
```

Или в режиме разработки с горячей перезагрузкой:
```bash
dotnet watch run
```

Откройте в браузере: **http://localhost:5000/catalogue.html**

---

## REST API

### `GET /api/cars` — список автомобилей

| Query-параметр | Тип    | Описание |
|----------------|--------|---------|
| `brand`        | string | Марка (частичное совпадение) |
| `bodyType`     | string | `sedan` / `suv` / `hatchback` / ... |
| `fuelType`     | string | `petrol` / `diesel` / `hybrid` / `electric` / `gas` |
| `transmission` | string | `automatic` / `manual` / `robot` / `variator` |
| `driveType`    | string | `front` / `rear` / `all` |
| `steering`     | string | `left` / `right` |
| `priceMin`     | int    | Цена от |
| `priceMax`     | int    | Цена до |
| `yearMin`      | int    | Год от |
| `yearMax`      | int    | Год до |
| `sort`         | string | `id_asc` / `price_asc` / `price_desc` / `year_desc` / `mileage_asc` |
| `page`         | int    | Страница (default: 1) |
| `limit`        | int    | Размер страницы (default: 6, max: 50) |

**Ответ:**
```json
{
  "data":  [...],
  "total": 20,
  "page":  1,
  "pages": 4,
  "limit": 6
}
```

### `GET /api/cars/1`
### `POST /api/cars` — тело запроса JSON (CarWriteDto)
### `PUT /api/cars/1` — частичное обновление
### `DELETE /api/cars/1`

---

## Публикация (деплой)

```bash
# Сборка в релизном режиме
dotnet publish -c Release -o ./publish

# Запуск опубликованного бинарника
./publish/AvtoLuz
```

### Docker (опционально)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY publish/ .
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "AvtoLuz.dll"]
```

### Строка подключения через переменную окружения

```bash
export ConnectionStrings__DefaultConnection="Host=db;Port=5432;Database=avtoluz;Username=postgres;Password=secret"
dotnet run
```
