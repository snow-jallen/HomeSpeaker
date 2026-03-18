# syntax=docker/dockerfile:1
# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

# Install runtime dependencies in one layer with cleanup
# --no-install-recommends: Don't install recommended packages (saves 100-200MB)
# apt-get clean + rm: Remove apt cache (saves 50-100MB)
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        sox \
        libsox-fmt-mp3 \
        alsa-utils \
        vlc \
        ffmpeg \
        curl \
        iputils-ping \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["HomeSpeaker.Server2/HomeSpeaker.Server2.csproj", "HomeSpeaker.Server2/"]
COPY ["HomeSpeaker.Shared/HomeSpeaker.Shared.csproj", "HomeSpeaker.Shared/"]
COPY ["HomeSpeaker.WebAssembly/HomeSpeaker.WebAssembly.csproj", "HomeSpeaker.WebAssembly/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "./HomeSpeaker.Server2/HomeSpeaker.Server2.csproj"

COPY . .
WORKDIR "/src/HomeSpeaker.Server2"

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "./HomeSpeaker.Server2.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

FROM base AS final

RUN grep 1000 /etc/passwd && usermod -aG audio ubuntu
USER ubuntu

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HomeSpeaker.Server2.dll"]
