# syntax=docker/dockerfile:1
#
# Dockerfile fuer appointment-service (AppointmentService.Web).
#
# WICHTIG: Build-Context ist das Repo-ROOT (nicht Services/appointment-service/), da
# AppointmentService.Web.csproj zusaetzlich AppHost/Crm.ServiceDefaults referenziert
# (OpenTelemetry/Health-Checks/Service-Discovery-Defaults, siehe AppHost/README.md).
# Build-Aufruf (siehe docker-compose.prod.yml):
#   docker build -f Services/appointment-service/src/AppointmentService.Web/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Services/appointment-service/src/AppointmentService.Domain/AppointmentService.Domain.csproj Services/appointment-service/src/AppointmentService.Domain/
COPY Services/appointment-service/src/AppointmentService.Application/AppointmentService.Application.csproj Services/appointment-service/src/AppointmentService.Application/
COPY Services/appointment-service/src/AppointmentService.Infrastructure/AppointmentService.Infrastructure.csproj Services/appointment-service/src/AppointmentService.Infrastructure/
COPY Services/appointment-service/src/AppointmentService.Web/AppointmentService.Web.csproj Services/appointment-service/src/AppointmentService.Web/
COPY AppHost/Crm.ServiceDefaults/Crm.ServiceDefaults.csproj AppHost/Crm.ServiceDefaults/

RUN dotnet restore Services/appointment-service/src/AppointmentService.Web/AppointmentService.Web.csproj

COPY Services/appointment-service/src/ Services/appointment-service/src/
COPY AppHost/Crm.ServiceDefaults/ AppHost/Crm.ServiceDefaults/

RUN dotnet publish Services/appointment-service/src/AppointmentService.Web/AppointmentService.Web.csproj \
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

ENTRYPOINT ["dotnet", "AppointmentService.Web.dll"]
