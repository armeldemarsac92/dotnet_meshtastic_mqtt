# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/MeshBoard.Contracts/MeshBoard.Contracts.csproj src/MeshBoard.Contracts/
COPY src/MeshBoard.Application/MeshBoard.Application.csproj src/MeshBoard.Application/
COPY src/MeshBoard.Infrastructure.Meshtastic/MeshBoard.Infrastructure.Meshtastic.csproj src/MeshBoard.Infrastructure.Meshtastic/
COPY src/MeshBoard.Infrastructure.Eventing/MeshBoard.Infrastructure.Eventing.csproj src/MeshBoard.Infrastructure.Eventing/
COPY src/MeshBoard.Collector.Normalizer/MeshBoard.Collector.Normalizer.csproj src/MeshBoard.Collector.Normalizer/

RUN dotnet restore src/MeshBoard.Collector.Normalizer/MeshBoard.Collector.Normalizer.csproj

COPY src ./src

RUN dotnet publish src/MeshBoard.Collector.Normalizer/MeshBoard.Collector.Normalizer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "MeshBoard.Collector.Normalizer.dll"]
