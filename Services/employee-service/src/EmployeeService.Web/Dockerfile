# syntax=docker/dockerfile:1
#
# Dockerfile fuer employee-service (EmployeeService.Web).
#
# WICHTIG: Build-Context ist das Repo-ROOT (nicht Services/employee-service/), da
# EmployeeService.Web.csproj zusaetzlich AppHost/Crm.ServiceDefaults referenziert
# (OpenTelemetry/Health-Checks/Service-Discovery-Defaults, siehe AppHost/README.md).
# Build-Aufruf (siehe docker-compose.prod.yml):
#   docker build -f Services/employee-service/src/EmployeeService.Web/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Services/employee-service/src/EmployeeService.Domain/EmployeeService.Domain.csproj Services/employee-service/src/EmployeeService.Domain/
COPY Services/employee-service/src/EmployeeService.Application/EmployeeService.Application.csproj Services/employee-service/src/EmployeeService.Application/
COPY Services/employee-service/src/EmployeeService.Infrastructure/EmployeeService.Infrastructure.csproj Services/employee-service/src/EmployeeService.Infrastructure/
COPY Services/employee-service/src/EmployeeService.Web/EmployeeService.Web.csproj Services/employee-service/src/EmployeeService.Web/
COPY AppHost/Crm.ServiceDefaults/Crm.ServiceDefaults.csproj AppHost/Crm.ServiceDefaults/

RUN dotnet restore Services/employee-service/src/EmployeeService.Web/EmployeeService.Web.csproj

COPY Services/employee-service/src/ Services/employee-service/src/
COPY AppHost/Crm.ServiceDefaults/ AppHost/Crm.ServiceDefaults/

RUN dotnet publish Services/employee-service/src/EmployeeService.Web/EmployeeService.Web.csproj \
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

ENTRYPOINT ["dotnet", "EmployeeService.Web.dll"]
