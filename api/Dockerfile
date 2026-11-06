# --- Build ----------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los .csproj: la restauración queda en caché mientras no cambien los paquetes.
COPY src/PlantPulse.Domain/PlantPulse.Domain.csproj                 src/PlantPulse.Domain/
COPY src/PlantPulse.Application/PlantPulse.Application.csproj       src/PlantPulse.Application/
COPY src/PlantPulse.Infrastructure/PlantPulse.Infrastructure.csproj src/PlantPulse.Infrastructure/
COPY src/PlantPulse.Api/PlantPulse.Api.csproj                       src/PlantPulse.Api/
RUN dotnet restore src/PlantPulse.Api/PlantPulse.Api.csproj

COPY src/ src/
RUN dotnet publish src/PlantPulse.Api/PlantPulse.Api.csproj -c Release -o /app --no-restore

# --- Runtime --------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "PlantPulse.Api.dll"]
