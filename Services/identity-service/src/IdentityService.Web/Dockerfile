# syntax=docker/dockerfile:1
#
# Dockerfile fuer identity-service (IdentityService.Web). Kein ConnectionString-Bedarf
# (In-Memory-Seed-Nutzer, keine eigene DB, siehe README.md).
#
# WICHTIG: Build-Context ist das Repo-ROOT (nicht Services/identity-service/), da
# IdentityService.Web.csproj zusaetzlich AppHost/Crm.ServiceDefaults referenziert
# (OpenTelemetry/Health-Checks/Service-Discovery-Defaults, siehe AppHost/README.md).
# Build-Aufruf (siehe docker-compose.prod.yml):
#   docker build -f Services/identity-service/src/IdentityService.Web/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Services/identity-service/src/IdentityService.Domain/IdentityService.Domain.csproj Services/identity-service/src/IdentityService.Domain/
COPY Services/identity-service/src/IdentityService.Application/IdentityService.Application.csproj Services/identity-service/src/IdentityService.Application/
COPY Services/identity-service/src/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj Services/identity-service/src/IdentityService.Infrastructure/
COPY Services/identity-service/src/IdentityService.Web/IdentityService.Web.csproj Services/identity-service/src/IdentityService.Web/
COPY AppHost/Crm.ServiceDefaults/Crm.ServiceDefaults.csproj AppHost/Crm.ServiceDefaults/

RUN dotnet restore Services/identity-service/src/IdentityService.Web/IdentityService.Web.csproj

COPY Services/identity-service/src/ Services/identity-service/src/
COPY AppHost/Crm.ServiceDefaults/ AppHost/Crm.ServiceDefaults/

RUN dotnet publish Services/identity-service/src/IdentityService.Web/IdentityService.Web.csproj \
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

ENTRYPOINT ["dotnet", "IdentityService.Web.dll"]
