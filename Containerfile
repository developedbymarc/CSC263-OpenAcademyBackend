# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only what restore needs, so the layer is cached until dependencies change.
# Add Directory.Build.props, Directory.Packages.props, global.json, nuget.config here if you have them.
COPY ["OpenAcademy.csproj", "./"]
RUN dotnet restore "OpenAcademy.csproj" -r linux-x64

COPY . .
RUN dotnet publish "OpenAcademy.csproj" \
    -c $BUILD_CONFIGURATION -r linux-x64 --no-self-contained --no-restore \
    -p:UseAppHost=false -o /app/publish

# ---- ffmpeg ----
FROM docker.io/mwader/static-ffmpeg:9.0.2-1 AS ffmpeg

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=mwader/static-ffmpeg:9.0.2-1 /ffmpeg /usr/local/bin/ffmpeg
COPY --from=mwader/static-ffmpeg:9.0.2-1 /ffprobe /usr/local/bin/ffprobe

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "OpenAcademy.dll"]
