# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
RUN apt update && apt install --yes sox libsox-fmt-mp3 alsa-utils vlc ffmpeg 
RUN apt install --yes shairport-sync pulseaudio-utils avahi-daemon
# Disable system shairport-sync service to prevent conflicts
RUN systemctl disable shairport-sync 2>/dev/null || true
# Configure ALSA for better audio support
RUN echo "pcm.!default { type pulse }" > /etc/asound.conf && \
    echo "ctl.!default { type pulse }" >> /etc/asound.conf
# Create audio group and add user
RUN groupadd -g 1000 mygroup 2>/dev/null || true
RUN useradd -u 1000 -g mygroup -s /bin/bash -m homespeakeruser 2>/dev/null || true
RUN usermod -aG audio homespeakeruser
# Allow homespeakeruser to run avahi-daemon with sudo
RUN echo "homespeakeruser ALL=(ALL) NOPASSWD: /usr/sbin/avahi-daemon, /bin/mkdir, /bin/chown" >> /etc/sudoers
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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
RUN groupadd -g 1000 mygroup 2>/dev/null || true
RUN useradd -u 1000 -g mygroup -s /bin/bash -m homespeakeruser 2>/dev/null || true
#RUN useradd -ms /bin/bash homespeakeruser
RUN usermod -aG audio homespeakeruser
# Allow homespeakeruser to run avahi-daemon with sudo
RUN echo "homespeakeruser ALL=(ALL) NOPASSWD: /usr/sbin/avahi-daemon, /bin/mkdir, /bin/chown" >> /etc/sudoers
COPY container-diagnostics.sh /usr/local/bin/container-diagnostics.sh
RUN chmod +x /usr/local/bin/container-diagnostics.sh
COPY HomeSpeaker.Server2/start-homespeaker.sh /usr/local/bin/start-homespeaker.sh
RUN chmod +x /usr/local/bin/start-homespeaker.sh
USER homespeakeruser
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["/usr/local/bin/start-homespeaker.sh"]
