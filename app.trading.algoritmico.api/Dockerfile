# syntax=docker/dockerfile:1.7

# ─── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/AppTradingAlgoritmico.Domain/*.csproj         ./src/AppTradingAlgoritmico.Domain/
COPY src/AppTradingAlgoritmico.Application/*.csproj    ./src/AppTradingAlgoritmico.Application/
COPY src/AppTradingAlgoritmico.Infrastructure/*.csproj ./src/AppTradingAlgoritmico.Infrastructure/
COPY src/AppTradingAlgoritmico.WebAPI/*.csproj         ./src/AppTradingAlgoritmico.WebAPI/

RUN dotnet restore src/AppTradingAlgoritmico.WebAPI/AppTradingAlgoritmico.WebAPI.csproj

COPY src/ ./src/

RUN dotnet publish src/AppTradingAlgoritmico.WebAPI/AppTradingAlgoritmico.WebAPI.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ─── Runtime stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=false

EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "AppTradingAlgoritmico.WebAPI.dll"]
