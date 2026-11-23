FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Restore from the project files alone, so this layer stays cached while only code changes.
COPY global.json NuGet.config Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY src/ElectricityTimeSeries.Api/ElectricityTimeSeries.Api.csproj src/ElectricityTimeSeries.Api/
COPY src/ElectricityTimeSeries.Core/ElectricityTimeSeries.Core.csproj src/ElectricityTimeSeries.Core/
COPY src/ElectricityTimeSeries.Infrastructure/ElectricityTimeSeries.Infrastructure.csproj src/ElectricityTimeSeries.Infrastructure/
RUN dotnet restore src/ElectricityTimeSeries.Api/ElectricityTimeSeries.Api.csproj

COPY src/ src/
RUN dotnet publish src/ElectricityTimeSeries.Api/ElectricityTimeSeries.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

# A small program that applies the EF Core migrations, used by docker-compose.sqlserver.yml.
FROM build AS migrations-build
COPY dotnet-tools.json ./
RUN dotnet tool restore && \
    dotnet ef migrations bundle --configuration Release --output /app/efbundle \
        --project src/ElectricityTimeSeries.Infrastructure --startup-project src/ElectricityTimeSeries.Infrastructure

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS migrations
WORKDIR /app
COPY --from=migrations-build /app/efbundle .
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# The API. Last, so it's what a plain docker build produces.
FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Run as the image's non-root user.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ElectricityTimeSeries.Api.dll"]
