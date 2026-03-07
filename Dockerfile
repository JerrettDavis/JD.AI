# syntax=docker/dockerfile:1.7
# Root Dockerfile — builds JD.AI.Daemon (default workload).
# For other targets see deploy/docker/Dockerfile.gateway and deploy/docker/Dockerfile.tui.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/JD.AI.Daemon/JD.AI.Daemon.csproj
RUN dotnet publish src/JD.AI.Daemon/JD.AI.Daemon.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 15790
ENV ASPNETCORE_URLS=http://+:15790
USER $APP_UID
ENTRYPOINT ["dotnet", "JD.AI.Daemon.dll"]
