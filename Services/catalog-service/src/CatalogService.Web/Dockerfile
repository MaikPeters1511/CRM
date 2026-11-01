# syntax=docker/dockerfile:1
#
# Dockerfile fuer catalog-service (CatalogService.Web).
#
# WICHTIG: Build-Context ist das Repo-ROOT (nicht Services/catalog-service/), da
# CatalogService.Web.csproj zusaetzlich AppHost/Crm.ServiceDefaults referenziert
# (OpenTelemetry/Health-Checks/Service-Discovery-Defaults, siehe AppHost/README.md).
# Build-Aufruf (siehe docker-compose.prod.yml):
#   docker build -f Services/catalog-service/src/CatalogService.Web/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Services/catalog-service/src/CatalogService.Domain/CatalogService.Domain.csproj Services/catalog-service/src/CatalogService.Domain/
COPY Services/catalog-service/src/CatalogService.Application/CatalogService.Application.csproj Services/catalog-service/src/CatalogService.Application/
COPY Services/catalog-service/src/CatalogService.Infrastructure/CatalogService.Infrastructure.csproj Services/catalog-service/src/CatalogService.Infrastructure/
COPY Services/catalog-service/src/CatalogService.Web/CatalogService.Web.csproj Services/catalog-service/src/CatalogService.Web/
COPY AppHost/Crm.ServiceDefaults/Crm.ServiceDefaults.csproj AppHost/Crm.ServiceDefaults/

RUN dotnet restore Services/catalog-service/src/CatalogService.Web/CatalogService.Web.csproj

COPY Services/catalog-service/src/ Services/catalog-service/src/
COPY AppHost/Crm.ServiceDefaults/ AppHost/Crm.ServiceDefaults/

RUN dotnet publish Services/catalog-service/src/CatalogService.Web/CatalogService.Web.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Das aspnet-Basisimage bringt bereits einen fertigen non-root User mit (app, uid/gid 1654,
# siehe /etc/passwd im Image) - kein eigenes groupadd/useradd notwendig.
COPY --from=build /app/publish .
RUN chown -R app:app /app
USER app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CatalogService.Web.dll"]
