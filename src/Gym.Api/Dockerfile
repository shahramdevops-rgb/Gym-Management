# Production image for the API. The build context is the repository root, because the solution
# shares Directory.Build.props, Directory.Packages.props and global.json across every project:
#   docker build -f src/Gym.Api/Dockerfile -t gym-api .
# docker-compose.prod.yml does exactly this.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files alone. Docker caches each layer, so a code change
# reuses this layer and does not download every package again.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Gym.Domain/Gym.Domain.csproj src/Gym.Domain/
COPY src/Gym.Application/Gym.Application.csproj src/Gym.Application/
COPY src/Gym.Infrastructure/Gym.Infrastructure.csproj src/Gym.Infrastructure/
COPY src/Gym.Api/Gym.Api.csproj src/Gym.Api/
RUN dotnet restore src/Gym.Api/Gym.Api.csproj

COPY src/ src/
RUN dotnet publish src/Gym.Api/Gym.Api.csproj -c Release --no-restore -o /app

# The Debian-based aspnet image carries both ICU (Persian dates and digits) and tzdata
# (Gym:TimeZone = Asia/Tehran). An Alpine variant would need both installed by hand.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Not root: a compromised API process should not own the container.
USER $APP_UID

# aspnet 10 images listen on 8080 (ASPNETCORE_HTTP_PORTS). Only Caddy reaches it, over the
# Compose network; no port is published to the host.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gym.Api.dll"]
