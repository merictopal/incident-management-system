# Build context is the repository root (see docker-compose.yml),
# so the shared ServiceDefaults project can be included.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first (cached as long as project files do not change).
COPY Directory.Build.props ./
COPY api-gateway/ApiGateway.csproj api-gateway/
RUN dotnet restore api-gateway/ApiGateway.csproj

COPY api-gateway/ api-gateway/
RUN dotnet publish api-gateway/ApiGateway.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
# Run as the non-root user that ships with the official .NET images.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
