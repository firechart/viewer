# The image of FireChart Viewer: the published files of FireChart.Viewer.App behind nginx (docker/nginx.conf), not root, port 8080.
#   docker build -t firechart-viewer:local .
# The build stage runs on the architecture of the builder: the publish is WebAssembly, the same on every architecture.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0 AS build
# The version is the file VERSION of the sources
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/FireChart.Viewer.App/FireChart.Viewer.App.csproj
# The stylesheets before the publish, one project after another with the one Tailwind CLI: on a clean tree the CSS a project writes during the
# publish is missing from its static assets. The CLI (about 100 MB from GitHub) is kept in a cache of the builder
RUN --mount=type=cache,target=/root/.nuget/packages --mount=type=cache,id=tailwind-cli,target=/src/.tailwind,sharing=locked \
    dotnet msbuild src/Flox.Shared/Flox.Shared.csproj -t:TailwindDownload -p:Configuration=Release \
 && for project in src/Flox.Shared/Flox.Shared.csproj src/FireChart.Viewer/FireChart.Viewer.csproj src/FireChart.Viewer.App/FireChart.Viewer.App.csproj; do \
        dotnet msbuild "$project" -t:TailwindBuild -p:Configuration=Release || exit 1; \
    done
# The configuration files are mounted by an installation: no compressed copies of them
RUN --mount=type=cache,target=/root/.nuget/packages --mount=type=cache,id=tailwind-cli,target=/src/.tailwind \
    dotnet publish src/FireChart.Viewer.App/FireChart.Viewer.App.csproj -c Release -o /publish --no-restore \
 && rm -f /publish/wwwroot/viewer.json.* /publish/wwwroot/host.json.*

FROM nginxinc/nginx-unprivileged:1.29-alpine
COPY --from=build /publish/wwwroot /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
