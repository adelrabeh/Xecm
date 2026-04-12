FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Restore
COPY Darah.ECM.sln .
COPY src/Darah.ECM.Domain/Darah.ECM.Domain.csproj src/Darah.ECM.Domain/
COPY src/Darah.ECM.Application/Darah.ECM.Application.csproj src/Darah.ECM.Application/
COPY src/Darah.ECM.Infrastructure/Darah.ECM.Infrastructure.csproj src/Darah.ECM.Infrastructure/
COPY src/Darah.ECM.API/Darah.ECM.API.csproj src/Darah.ECM.API/
COPY src/Darah.ECM.xECM/Darah.ECM.xECM.csproj src/Darah.ECM.xECM/
RUN dotnet restore src/Darah.ECM.API/Darah.ECM.API.csproj

# Build
COPY src/ src/
RUN dotnet publish src/Darah.ECM.API/Darah.ECM.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
RUN mkdir -p /app/ecm-storage /app/logs
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Use PORT env var from Railway (defaults to 8080)
CMD PORT=${PORT:-8080} && ASPNETCORE_URLS="http://+:$PORT" dotnet Darah.ECM.API.dll

# Cache bust: 1744474000
