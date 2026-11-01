# syntax=docker/dockerfile:1
#
# Dockerfile fuer customer-service (CustomerService.Web).
#
# WICHTIG: Build-Context ist das Repo-ROOT (nicht Services/customer-service/), da
# CustomerService.Web.csproj zusaetzlich AppHost/Crm.ServiceDefaults referenziert
# (OpenTelemetry/Health-Checks/Service-Discovery-Defaults, siehe AppHost/README.md).
# Build-Aufruf (siehe docker-compose.prod.yml):
#   docker build -f Services/customer-service/src/CustomerService.Web/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Nur die .csproj-Dateien zuerst kopieren (Docker-Layer-Caching: "dotnet restore" wird nur neu
# ausgefuehrt, wenn sich Projektreferenzen/Paketreferenzen aendern, nicht bei jeder Code-Aenderung).
COPY Services/customer-service/src/CustomerService.Domain/CustomerService.Domain.csproj Services/customer-service/src/CustomerService.Domain/
COPY Services/customer-service/src/CustomerService.Application/CustomerService.Application.csproj Services/customer-service/src/CustomerService.Application/
COPY Services/customer-service/src/CustomerService.Infrastructure/CustomerService.Infrastructure.csproj Services/customer-service/src/CustomerService.Infrastructure/
COPY Services/customer-service/src/CustomerService.Web/CustomerService.Web.csproj Services/customer-service/src/CustomerService.Web/
COPY AppHost/Crm.ServiceDefaults/Crm.ServiceDefaults.csproj AppHost/Crm.ServiceDefaults/

RUN dotnet restore Services/customer-service/src/CustomerService.Web/CustomerService.Web.csproj

# Restlichen Quellcode kopieren und publishen.
COPY Services/customer-service/src/ Services/customer-service/src/
COPY AppHost/Crm.ServiceDefaults/ AppHost/Crm.ServiceDefaults/

RUN dotnet publish Services/customer-service/src/CustomerService.Web/CustomerService.Web.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Non-root User (OWASP-Haertung, kein Container-Prozess als root).
# Das aspnet-Basisimage bringt bereits einen fertigen non-root User mit (app, uid/gid 1654,
# siehe /etc/passwd im Image) - kein eigenes groupadd/useradd notwendig.
COPY --from=build /app/publish .
RUN chown -R app:app /app
USER app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CustomerService.Web.dll"]
