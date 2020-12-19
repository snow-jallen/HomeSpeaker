#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim-arm64v8 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
RUN apt update && apt install --yes sox libsox-fmt-mp3 alsa-utils

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim-arm64v8 AS build
WORKDIR /src
COPY ["HomeSpeaker.Shared/HomeSpeaker.Shared.csproj", "HomeSpeaker.Shared/"]
RUN dotnet restore "HomeSpeaker.Server/HomeSpeaker.Server.csproj"
COPY . .
WORKDIR "/src/HomeSpeaker.Server"
RUN dotnet build "HomeSpeaker.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HomeSpeaker.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HomeSpeaker.Server.dll"]