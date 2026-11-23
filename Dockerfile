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

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Run as the image's non-root user.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ElectricityTimeSeries.Api.dll"]
