# syntax=docker/dockerfile:1.7

FROM node:20-bookworm-slim AS assets
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY package.json package-lock.json ./
COPY src/MeshBoard.Contracts/MeshBoard.Contracts.csproj src/MeshBoard.Contracts/
COPY src/MeshBoard.Api.SDK/MeshBoard.Api.SDK.csproj src/MeshBoard.Api.SDK/
COPY src/MeshBoard.Client/MeshBoard.Client.csproj src/MeshBoard.Client/

COPY --from=assets /src/node_modules /src/node_modules
COPY --from=assets /usr/local/bin/node /usr/local/bin/node
COPY --from=assets /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -sf ../lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm && \
    ln -sf ../lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx

RUN dotnet restore src/MeshBoard.Client/MeshBoard.Client.csproj

COPY src ./src

RUN dotnet publish src/MeshBoard.Client/MeshBoard.Client.csproj -c Release -o /app/publish --no-restore

RUN set -eu; \
    framework_dir=/app/publish/wwwroot/_framework; \
    cd "$framework_dir"; \
    for file in blazor.webassembly.*.js; do cp "$file" blazor.webassembly.js; break; done; \
    for file in dotnet.*.js; do \
        case "$file" in \
            dotnet.native.*|dotnet.runtime.*) ;; \
            *) cp "$file" dotnet.js; break ;; \
        esac; \
    done; \
    for file in dotnet.runtime.*.js; do cp "$file" dotnet.runtime.js; break; done; \
    for file in dotnet.native.*.js; do cp "$file" dotnet.native.js; break; done; \
    for file in dotnet.native.*.wasm; do cp "$file" dotnet.native.wasm; break; done; \
    index_file=/app/publish/wwwroot/index.html; \
    awk 'BEGIN{skip=0} /<script type="importmap">/{skip=1; next} skip && /<\/script>/{skip=0; next} !skip {print}' "$index_file" > "$index_file.tmp"; \
    mv "$index_file.tmp" "$index_file"

FROM caddy:2.10.2-alpine AS final
WORKDIR /srv

COPY src/MeshBoard.Client/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /app/publish/wwwroot /srv

EXPOSE 80
