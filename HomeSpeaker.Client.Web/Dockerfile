#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim-arm64v8 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim-arm64v8 AS build
WORKDIR /src
COPY ["HomeSpeaker.Client.Web/HomeSpeaker.Client.Web.csproj", "HomeSpeaker.Client.Web/"]
COPY ["HomeSpeaker.Client/HomeSpeaker.Client.csproj", "HomeSpeaker.Client/"]
COPY ["HomeSpeaker.Shared/HomeSpeaker.Shared.csproj", "HomeSpeaker.Shared/"]
RUN dotnet restore "HomeSpeaker.Client.Web/HomeSpeaker.Client.Web.csproj"
COPY . .
WORKDIR "/src/HomeSpeaker.Client.Web"
RUN dotnet build "HomeSpeaker.Client.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HomeSpeaker.Client.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HomeSpeaker.Client.Web.dll"]