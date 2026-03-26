# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/MeshBoard.Contracts/MeshBoard.Contracts.csproj src/MeshBoard.Contracts/
COPY src/MeshBoard.Application/MeshBoard.Application.csproj src/MeshBoard.Application/
COPY src/MeshBoard.Infrastructure.Persistence/MeshBoard.Infrastructure.Persistence.csproj src/MeshBoard.Infrastructure.Persistence/
COPY src/MeshBoard.Infrastructure.Meshtastic/MeshBoard.Infrastructure.Meshtastic.csproj src/MeshBoard.Infrastructure.Meshtastic/
COPY src/MeshBoard.RealtimeBridge/MeshBoard.RealtimeBridge.csproj src/MeshBoard.RealtimeBridge/

RUN dotnet restore src/MeshBoard.RealtimeBridge/MeshBoard.RealtimeBridge.csproj

COPY src ./src

RUN dotnet publish src/MeshBoard.RealtimeBridge/MeshBoard.RealtimeBridge.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "MeshBoard.RealtimeBridge.dll"]
