# ── Build stage ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore (cached layer)
COPY QrMenu.sln .
COPY QrMenu.Domain/QrMenu.Domain.csproj           QrMenu.Domain/
COPY QrMenu.Application/QrMenu.Application.csproj QrMenu.Application/
COPY QrMenu.Infrastructure/QrMenu.Infrastructure.csproj QrMenu.Infrastructure/
COPY QrMenu.Web/QrMenu.Web.csproj                 QrMenu.Web/

RUN dotnet restore QrMenu.Web/QrMenu.Web.csproj

# Copy source and publish
COPY . .
RUN dotnet publish QrMenu.Web/QrMenu.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime stage ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create upload + QR dirs
RUN mkdir -p /app/wwwroot/uploads/logos \
             /app/wwwroot/uploads/items \
             /app/wwwroot/qr \
             /data

COPY --from=build /app/publish .

# Railway injects PORT env var — bind to it
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "QrMenu.Web.dll"]
