# ── Этап 1: сборка ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Сначала копируем только .csproj и восстанавливаем зависимости
# (Docker кэширует этот слой — повторная сборка быстрее если код не менялся)
COPY AvtoLuz/AvtoLuz.csproj AvtoLuz/
RUN dotnet restore AvtoLuz/AvtoLuz.csproj

# Копируем весь остальной исходный код и публикуем
COPY AvtoLuz/ AvtoLuz/
RUN dotnet publish AvtoLuz/AvtoLuz.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ── Этап 2: финальный образ (только runtime, без SDK) ─────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Копируем скомпилированное приложение из этапа сборки
COPY --from=build /app/publish .

# Render сам назначает порт через переменную окружения PORT.
# ASP.NET Core читает ASPNETCORE_URLS автоматически.
ENV ASPNETCORE_URLS=http://+:${PORT:-5000}

# Создаём папку для загружаемых фото
RUN mkdir -p wwwroot/images

EXPOSE 5000
ENTRYPOINT ["dotnet", "AvtoLuz.dll"]
