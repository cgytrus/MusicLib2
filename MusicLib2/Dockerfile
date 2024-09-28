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
COPY . .
WORKDIR "/src/MusicLib2"
RUN dotnet build "MusicLib2.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MusicLib2.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ML2_PATH=~/MusicLib2
ENTRYPOINT ["dotnet", "MusicLib2.dll"]
