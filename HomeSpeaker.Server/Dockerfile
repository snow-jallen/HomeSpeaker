#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim-arm32v7 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
RUN apt update && apt install --yes sox libsox-fmt-mp3 alsa-utils vlc

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["HomeSpeaker.Server/HomeSpeaker.Server.csproj", "HomeSpeaker.Server/"]
COPY ["HomeSpeaker.Shared/HomeSpeaker.Shared.csproj", "HomeSpeaker.Shared/"]
RUN dotnet restore "HomeSpeaker.Server/HomeSpeaker.Server.csproj"
COPY . .
WORKDIR "/src/HomeSpeaker.Server"
RUN dotnet publish "HomeSpeaker.Server.csproj" -c Release -r linux-arm -o /app/publish

FROM base AS final
RUN useradd -ms /bin/bash homespeakeruser
RUN usermod -aG audio homespeakeruser
USER homespeakeruser
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HomeSpeaker.Server.dll"]