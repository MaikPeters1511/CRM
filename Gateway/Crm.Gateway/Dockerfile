# syntax=docker/dockerfile:1
#
# Dockerfile fuer Crm.Gateway (YARP Reverse-Proxy, einziger oeffentlicher HTTP-Eintrittspunkt).
#
# WICHTIG: Build-Context ist das Repo-ROOT (nicht Gateway/), da Crm.Gateway.csproj zusaetzlich
# AppHost/Crm.ServiceDefaults referenziert (OpenTelemetry/Health-Checks/Service-Discovery-Defaults,
# siehe AppHost/README.md).
# Build-Aufruf (siehe docker-compose.prod.yml):
#   docker build -f Gateway/Crm.Gateway/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Gateway/Crm.Gateway/Crm.Gateway.csproj Gateway/Crm.Gateway/
COPY AppHost/Crm.ServiceDefaults/Crm.ServiceDefaults.csproj AppHost/Crm.ServiceDefaults/

RUN dotnet restore Gateway/Crm.Gateway/Crm.Gateway.csproj

COPY Gateway/Crm.Gateway/ Gateway/Crm.Gateway/
COPY AppHost/Crm.ServiceDefaults/ AppHost/Crm.ServiceDefaults/

RUN dotnet publish Gateway/Crm.Gateway/Crm.Gateway.csproj \
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

ENTRYPOINT ["dotnet", "Crm.Gateway.dll"]
