# Build context is the repository root (see docker-compose.yml),
# so the shared ServiceDefaults project can be included.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first (cached as long as project files do not change).
COPY Directory.Build.props ./
COPY shared/ServiceDefaults/ServiceDefaults.csproj shared/ServiceDefaults/
COPY services/user-service/UserService.csproj services/user-service/
RUN dotnet restore services/user-service/UserService.csproj

COPY shared/ shared/
COPY services/user-service/ services/user-service/
RUN dotnet publish services/user-service/UserService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
# Run as the non-root user that ships with the official .NET images.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "UserService.dll"]
