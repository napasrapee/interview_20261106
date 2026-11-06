FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
# Copy project files first so package restore stays cached until dependencies change.
COPY global.json nuget.config ./
COPY src/ProjectMonitoring.Domain/*.csproj src/ProjectMonitoring.Domain/
COPY src/ProjectMonitoring.Application/*.csproj src/ProjectMonitoring.Application/
COPY src/ProjectMonitoring.Infrastructure/*.csproj src/ProjectMonitoring.Infrastructure/
COPY src/ProjectMonitoring.Api/*.csproj src/ProjectMonitoring.Api/
RUN dotnet restore src/ProjectMonitoring.Api/ProjectMonitoring.Api.csproj
COPY src/ src/
RUN dotnet publish src/ProjectMonitoring.Api/ProjectMonitoring.Api.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl is used by the container healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProjectMonitoring.Api.dll"]
