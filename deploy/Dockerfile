# lectarr — all-in-one build (frontend + backend + runtime)
# Build from the repository root:
#   docker build -f deploy/Dockerfile -t lectarr .

# ---------- Frontend ----------
FROM node:20 AS frontend
WORKDIR /src
COPY package.json yarn.lock tsconfig.json ./
COPY frontend ./frontend
RUN yarn install --frozen-lockfile --network-timeout 120000
RUN yarn run build --env production
# → /src/_output/UI

# ---------- Backend ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend
WORKDIR /src
COPY .editorconfig ./
COPY src ./src
COPY Logo ./Logo
RUN dotnet msbuild -restore src/Readarr.sln \
    -p:SelfContained=True \
    -p:Configuration=Release \
    -p:Platform=Posix \
    -p:RuntimeIdentifiers=linux-x64 \
    -t:PublishAllRids
# → /src/_output/net8.0/linux-x64/publish

# ---------- Runtime ----------
# Base glibc (Ubuntu) : requise pour le docker mod universal-calibre
# (conversion de format) qui ne fonctionne pas sur musl/alpine.
FROM ghcr.io/linuxserver/baseimage-ubuntu:noble

ENV XDG_CONFIG_HOME="/config/xdg" \
    COMPlus_EnableDiagnostics=0 \
    TMPDIR=/run/readarr-temp

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    libicu74 \
    libsqlite3-0 \
    xmlstarlet && \
    rm -rf /var/lib/apt/lists/*

ARG METADATA_URL=http://rreading-glasses:8788
ENV METADATA_URL=${METADATA_URL}

LABEL maintainer="greenlynxe"

RUN mkdir -p /app/readarr

COPY --from=backend /src/_output/net8.0/linux-x64/publish /app/readarr/bin
COPY --from=frontend /src/_output/UI /app/readarr/bin/UI

RUN echo -e "UpdateMethod=docker\nBranch=main\nPackageVersion=lectarr\nPackageAuthor=greenlynxe" > /app/readarr/package_info

# s6 service files (same layout as bookshelf)
COPY docker/root/ /

EXPOSE 8787

VOLUME /config
