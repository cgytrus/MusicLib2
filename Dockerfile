FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MusicLib2/MusicLib2.csproj", "MusicLib2/"]
RUN dotnet restore "MusicLib2/MusicLib2.csproj"
COPY MusicLib2/. ./MusicLib2
WORKDIR "/src/MusicLib2"
RUN dotnet build "MusicLib2.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MusicLib2.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM alpine:latest AS ffmpeg
RUN cat /etc/apk/repositories
RUN apk add wget unzip
RUN mkdir /opt ; \
    wget "https://github.com/ffbinaries/ffbinaries-prebuilt/releases/download/v6.1/ffmpeg-6.1-linux-64.zip" ; \
    unzip "ffmpeg-6.1-linux-64.zip" -d /opt ; \
    chmod +x /opt/ffmpeg

FROM alpine:latest AS ffprobe
RUN cat /etc/apk/repositories
RUN apk add wget unzip
RUN mkdir /opt ; \
    wget "https://github.com/ffbinaries/ffbinaries-prebuilt/releases/download/v6.1/ffprobe-6.1-linux-64.zip" ; \
    unzip "ffprobe-6.1-linux-64.zip" -d /opt ; \
    chmod +x /opt/ffprobe

FROM alpine:latest AS yt-dlp
RUN cat /etc/apk/repositories
RUN apk add wget
RUN mkdir /opt ; \
    cd /opt ; \
    wget "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux" ; \
    chmod +x /opt/yt-dlp_linux

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=ffmpeg /opt .
COPY --from=ffprobe /opt .
COPY --from=yt-dlp /opt .
ENTRYPOINT ["dotnet", "MusicLib2.dll"]
