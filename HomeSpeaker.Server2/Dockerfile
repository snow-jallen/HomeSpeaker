# syntax=docker/dockerfile:1
# App image — builds on the pre-built base (ghcr.io/snow-jallen/homespeaker-base)
# which contains all system packages (sox, ffmpeg, vlc, etc.).
# Rebuild the base image via the build-base-image.yml workflow when packages change.

FROM ghcr.io/snow-jallen/homespeaker-base:latest AS base

# --max-time/--connect-timeout ensure the probe curl can never hang and leak
# orphaned processes (dotnet is PID 1 and does not reap children). /alive is a
# DB-free liveness check, so a contended SQLite DB can't fail the probe.
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -fsS --max-time 5 --connect-timeout 2 http://localhost:80/alive || exit 1

WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["HomeSpeaker.Server2/HomeSpeaker.Server2.csproj", "HomeSpeaker.Server2/"]
COPY ["HomeSpeaker.Shared/HomeSpeaker.Shared.csproj", "HomeSpeaker.Shared/"]

# RUN --mount=type=cache,target=/root/.nuget/packages \
#    dotnet restore "./HomeSpeaker.Server2/HomeSpeaker.Server2.csproj"

COPY . .
WORKDIR "/src/HomeSpeaker.Server2"

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "./HomeSpeaker.Server2.csproj" && \
    dotnet publish "./HomeSpeaker.Server2.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:RunAnalyzersDuringPublish=false \
    --no-restore

FROM base AS final

USER ubuntu

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HomeSpeaker.Server2.dll"]
