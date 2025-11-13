# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt update && apt install --yes sox libsox-fmt-mp3 alsa-utils vlc ffmpeg
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["HomeSpeaker.Server2/HomeSpeaker.Server2.csproj", "HomeSpeaker.Server2/"]
COPY ["HomeSpeaker.Shared/HomeSpeaker.Shared.csproj", "HomeSpeaker.Shared/"]
COPY ["HomeSpeaker.WebAssembly/HomeSpeaker.WebAssembly.csproj", "HomeSpeaker.WebAssembly/"]
RUN dotnet restore "./HomeSpeaker.Server2/HomeSpeaker.Server2.csproj"
COPY . .
WORKDIR "/src/HomeSpeaker.Server2"
RUN dotnet build "./HomeSpeaker.Server2.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HomeSpeaker.Server2.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
RUN apt-get install -y curl iputils-ping
RUN grep 1000 /etc/passwd

# RUN groupadd -g 1000 mygroup
# RUN useradd -u 1000 -g mygroup -s /bin/bash -m homespeakeruser
# RUN usermod -aG audio homespeakeruser
RUN usermod -aG audio ubuntu
USER ubuntu

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HomeSpeaker.Server2.dll"]
