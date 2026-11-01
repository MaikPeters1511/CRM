# syntax=docker/dockerfile:1
#
# Dockerfile fuer ai-service (AiService.Web). Build-Context ist das Repo-ROOT (nicht
# Services/ai-service/), da AiService.Web.csproj zusaetzlich AppHost/Crm.ServiceDefaults
# referenziert (analog Services/catalog-service/src/CatalogService.Web/Dockerfile).
# Build-Aufruf (siehe docker-compose.prod.yml):
#   docker build -f Services/ai-service/src/AiService.Web/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Services/ai-service/src/AiService.Domain/AiService.Domain.csproj Services/ai-service/src/AiService.Domain/
COPY Services/ai-service/src/AiService.Application/AiService.Application.csproj Services/ai-service/src/AiService.Application/
COPY Services/ai-service/src/AiService.Infrastructure/AiService.Infrastructure.csproj Services/ai-service/src/AiService.Infrastructure/
COPY Services/ai-service/src/AiService.Web/AiService.Web.csproj Services/ai-service/src/AiService.Web/
COPY AppHost/Crm.ServiceDefaults/Crm.ServiceDefaults.csproj AppHost/Crm.ServiceDefaults/

RUN dotnet restore Services/ai-service/src/AiService.Web/AiService.Web.csproj

COPY Services/ai-service/src/ Services/ai-service/src/
COPY AppHost/Crm.ServiceDefaults/ AppHost/Crm.ServiceDefaults/

RUN dotnet publish Services/ai-service/src/AiService.Web/AiService.Web.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .
RUN chown -R app:app /app
USER app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AiService.Web.dll"]
