# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, using only the project files, so the restore layer is cached between code changes.
COPY global.json ./
COPY src/ElectricityTimeSeries.Api/ElectricityTimeSeries.Api.csproj src/ElectricityTimeSeries.Api/
COPY src/ElectricityTimeSeries.Core/ElectricityTimeSeries.Core.csproj src/ElectricityTimeSeries.Core/
COPY src/ElectricityTimeSeries.Infrastructure/ElectricityTimeSeries.Infrastructure.csproj src/ElectricityTimeSeries.Infrastructure/
RUN dotnet restore src/ElectricityTimeSeries.Api/ElectricityTimeSeries.Api.csproj

COPY src/ src/
RUN dotnet publish src/ElectricityTimeSeries.Api/ElectricityTimeSeries.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Run as the image's non-root user.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ElectricityTimeSeries.Api.dll"]
