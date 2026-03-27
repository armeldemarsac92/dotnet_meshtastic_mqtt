# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/MeshBoard.Contracts/MeshBoard.Contracts.csproj src/MeshBoard.Contracts/
COPY src/MeshBoard.Application/MeshBoard.Application.csproj src/MeshBoard.Application/
COPY src/MeshBoard.Infrastructure.Neo4j/MeshBoard.Infrastructure.Neo4j.csproj src/MeshBoard.Infrastructure.Neo4j/
COPY src/MeshBoard.Collector.TopologyAnalyst/MeshBoard.Collector.TopologyAnalyst.csproj src/MeshBoard.Collector.TopologyAnalyst/

RUN dotnet restore src/MeshBoard.Collector.TopologyAnalyst/MeshBoard.Collector.TopologyAnalyst.csproj

COPY src ./src

RUN dotnet publish src/MeshBoard.Collector.TopologyAnalyst/MeshBoard.Collector.TopologyAnalyst.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "MeshBoard.Collector.TopologyAnalyst.dll"]
