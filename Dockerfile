# syntax=docker/dockerfile:1
# Multi-stage build (SEC-12): the SDK never reaches the runtime image.

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, from project files and lock files only, so the layer is cached until dependencies change.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/ShipManagement.Domain/ShipManagement.Domain.csproj src/ShipManagement.Domain/packages.lock.json src/ShipManagement.Domain/
COPY src/ShipManagement.Application/ShipManagement.Application.csproj src/ShipManagement.Application/packages.lock.json src/ShipManagement.Application/
COPY src/ShipManagement.Infrastructure/ShipManagement.Infrastructure.csproj src/ShipManagement.Infrastructure/packages.lock.json src/ShipManagement.Infrastructure/
COPY src/ShipManagement.Api/ShipManagement.Api.csproj src/ShipManagement.Api/packages.lock.json src/ShipManagement.Api/
RUN dotnet restore src/ShipManagement.Api/ShipManagement.Api.csproj --locked-mode

COPY src/ src/
RUN dotnet publish src/ShipManagement.Api/ShipManagement.Api.csproj \
      --configuration Release --no-restore --output /app /p:UseAppHost=false

# ---- Runtime ----
# Chiseled Ubuntu: no shell, no package manager, non-root by default. The "extra" variant includes ICU,
# which Microsoft.Data.SqlClient requires (it does not support globalization-invariant mode).
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "ShipManagement.Api.dll"]
