# syntax=docker/dockerfile:1.7

FROM node:20-bookworm-slim AS assets
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY src ./src
RUN npm run tailwind:build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/MeshBoard.Contracts/MeshBoard.Contracts.csproj src/MeshBoard.Contracts/
COPY src/MeshBoard.Application/MeshBoard.Application.csproj src/MeshBoard.Application/
COPY src/MeshBoard.Infrastructure.Persistence/MeshBoard.Infrastructure.Persistence.csproj src/MeshBoard.Infrastructure.Persistence/
COPY src/MeshBoard.Infrastructure.Meshtastic/MeshBoard.Infrastructure.Meshtastic.csproj src/MeshBoard.Infrastructure.Meshtastic/
COPY src/MeshBoard.Web/MeshBoard.Web.csproj src/MeshBoard.Web/

RUN dotnet restore src/MeshBoard.Web/MeshBoard.Web.csproj

COPY src ./src
COPY --from=assets /src/src/MeshBoard.Web/wwwroot/app.css /src/src/MeshBoard.Web/wwwroot/app.css

RUN dotnet publish src/MeshBoard.Web/MeshBoard.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MeshBoard.Web.dll"]
