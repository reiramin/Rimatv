# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first (layer caching)
COPY iptv.Api/iptv.Api.csproj iptv.Api/
COPY iptv.Domain/iptv.Domain.csproj iptv.Domain/
COPY iptv.Services/iptv.Services.csproj iptv.Services/
COPY Utilities/Utilities.csproj Utilities/

RUN dotnet restore iptv.Api/iptv.Api.csproj

# Copy everything else and build
COPY . .
WORKDIR /src/iptv.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Render injects PORT env var; Kestrel must bind to it
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "iptv.Api.dll"]