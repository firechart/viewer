FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.0.0-local
WORKDIR /src
COPY . .
RUN dotnet msbuild src/Flox.Shared/Flox.Shared.csproj -restore -t:TailwindDownload -p:Configuration=Release -p:MinVerSkip=true
RUN dotnet msbuild src/Flox.Shared/Flox.Shared.csproj -restore -t:TailwindBuild -p:Configuration=Release -p:MinVerSkip=true
RUN dotnet msbuild src/FireChart.Viewer/FireChart.Viewer.csproj -restore -t:TailwindBuild -p:Configuration=Release -p:MinVerSkip=true
RUN dotnet publish src/FireChart.Viewer.App/FireChart.Viewer.App.csproj -c Release -o /publish -p:MinVerSkip=true -p:Version=$VERSION
RUN rm -f /publish/wwwroot/viewer.json.* /publish/wwwroot/host.json.*

FROM nginxinc/nginx-unprivileged:1.27-alpine
COPY --from=build /publish/wwwroot /usr/share/nginx/html
COPY <<EOF /etc/nginx/conf.d/default.conf
server {
    listen 8080;
    root /usr/share/nginx/html;
    gzip_static on;
    # Revalidated (ETag, a cheap 304): dotnet.js, index.html and the configs have no fingerprint, and without a header of ours a proxy
    # (Cloudflare: max-age=14400) lets the browser start the previous release for hours; the fingerprinted files are cached by the Blazor loader anyway
    add_header Cache-Control "no-cache" always;
    location / {
        try_files \$uri \$uri/ /index.html;
    }
    # The files of the libraries: a missing one is a 404, never the page (the Viewer asks for templates that may not exist)
    location /_content/ {
        try_files \$uri =404;
    }
    # The JavaScript SDK is imported as an ES module by pages of other origins: CORS for any, and revalidated so a page never mixes versions
    location /sdk/ {
        add_header Access-Control-Allow-Origin "*" always;
        add_header Cache-Control "no-cache" always;
    }
}
EOF
