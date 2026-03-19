# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/MeshBoard.Contracts/MeshBoard.Contracts.csproj src/MeshBoard.Contracts/
COPY src/MeshBoard.Application/MeshBoard.Application.csproj src/MeshBoard.Application/
COPY src/MeshBoard.Infrastructure.Persistence/MeshBoard.Infrastructure.Persistence.csproj src/MeshBoard.Infrastructure.Persistence/
COPY src/MeshBoard.Api/MeshBoard.Api.csproj src/MeshBoard.Api/

RUN dotnet restore src/MeshBoard.Api/MeshBoard.Api.csproj

COPY src ./src

RUN dotnet publish src/MeshBoard.Api/MeshBoard.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MeshBoard.Api.dll"]
