# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/MeshBoard.Contracts/MeshBoard.Contracts.csproj src/MeshBoard.Contracts/
COPY src/MeshBoard.Application/MeshBoard.Application.csproj src/MeshBoard.Application/
COPY src/MeshBoard.Infrastructure.Meshtastic/MeshBoard.Infrastructure.Meshtastic.csproj src/MeshBoard.Infrastructure.Meshtastic/
COPY src/MeshBoard.Infrastructure.Eventing/MeshBoard.Infrastructure.Eventing.csproj src/MeshBoard.Infrastructure.Eventing/
COPY src/MeshBoard.Collector.Ingress/MeshBoard.Collector.Ingress.csproj src/MeshBoard.Collector.Ingress/

RUN dotnet restore src/MeshBoard.Collector.Ingress/MeshBoard.Collector.Ingress.csproj

COPY src ./src

RUN dotnet publish src/MeshBoard.Collector.Ingress/MeshBoard.Collector.Ingress.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "MeshBoard.Collector.Ingress.dll"]
