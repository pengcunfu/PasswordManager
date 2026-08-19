# syntax=docker/dockerfile:1

FROM node:22-alpine AS web
WORKDIR /web
COPY src/PasswordManager.Web/package.json src/PasswordManager.Web/package-lock.json ./
RUN npm ci
COPY src/PasswordManager.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/PasswordManager.Api/ ./PasswordManager.Api/
RUN dotnet publish PasswordManager.Api/PasswordManager.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/api/health || exit 1
ENTRYPOINT ["dotnet", "PasswordManager.Api.dll"]
