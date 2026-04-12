FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project files
COPY Darah.ECM.sln .
COPY src/Darah.ECM.Domain/Darah.ECM.Domain.csproj src/Darah.ECM.Domain/
COPY src/Darah.ECM.Application/Darah.ECM.Application.csproj src/Darah.ECM.Application/
COPY src/Darah.ECM.Infrastructure/Darah.ECM.Infrastructure.csproj src/Darah.ECM.Infrastructure/
COPY src/Darah.ECM.API/Darah.ECM.API.csproj src/Darah.ECM.API/
COPY src/Darah.ECM.xECM/Darah.ECM.xECM.csproj src/Darah.ECM.xECM/

# Restore
RUN dotnet restore src/Darah.ECM.API/Darah.ECM.API.csproj

# Copy source code
COPY src/ src/

# Build
RUN dotnet publish src/Darah.ECM.API/Darah.ECM.API.csproj \
    -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Create storage directory
RUN mkdir -p /app/ecm-storage /app/logs

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "Darah.ECM.API.dll"]
# Cache bust: 1775990951
