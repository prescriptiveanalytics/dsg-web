# syntax=docker/dockerfile:1

# ---- Build stage ---------------------------------------------------------
# The build stage always runs natively; the framework-dependent output runs on every target platform.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore in its own layer so package downloads are cached across source changes.
COPY src/DsgWeb/DsgWeb.csproj src/DsgWeb/
RUN dotnet restore src/DsgWeb/DsgWeb.csproj

COPY src/DsgWeb/ src/DsgWeb/
RUN dotnet publish src/DsgWeb/DsgWeb.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# ---- Runtime stage -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

# curl is used by the container health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Run as the non-root user provided by the .NET base image.
USER $APP_UID

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=15s --retries=5 \
    CMD curl -fsS http://127.0.0.1:8080/api/stream/status || exit 1

ENTRYPOINT ["dotnet", "DsgWeb.dll"]
