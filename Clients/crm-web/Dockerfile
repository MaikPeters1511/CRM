# syntax=docker/dockerfile:1
#
# Dockerfile fuer crm-web (konsolidiertes Angular-22-Frontend, ADR 0002).
# Build-Context: Clients/crm-web/ (keine Cross-Ordner-Referenzen, eigenstaendiges npm-Projekt).
#
# Stage 1: Production-Build der Angular-SPA (default configuration ist bereits "production",
# siehe angular.json "defaultConfiguration": "production" - Output zeigt per
# environment.production.ts auf den relativen Pfad "/api").
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# Stage 2: schlankes nginx-Image, liefert die statischen Dateien aus und leitet /api/** an das
# Gateway weiter. Der Upstream-Hostname ist zur Build-Zeit nicht bekannt (Docker-Compose-
# Service-Name) - daher nginx.conf.template + envsubst im Entrypoint (Standardmuster, siehe
# Kommentar in nginx.conf.template).
FROM nginx:1.27-alpine AS final

COPY --from=build /app/dist/crm-web/browser /usr/share/nginx/html
COPY nginx.conf.template /etc/nginx/templates/default.conf.template

# API_GATEWAY_UPSTREAM wird per docker-compose.prod.yml gesetzt (Standard: "gateway:8080").
# Das offizielle nginx-Image fuehrt beim Start automatisch envsubst ueber alle Dateien unter
# /etc/nginx/templates/*.template aus und schreibt das Ergebnis nach /etc/nginx/conf.d/
# (kein eigenes Entrypoint-Skript notwendig, siehe docker-entrypoint.sh im nginx-Basisimage).
ENV API_GATEWAY_UPSTREAM=gateway:8080

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q -O- http://localhost/ > /dev/null || exit 1
