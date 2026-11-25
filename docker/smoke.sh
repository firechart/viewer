#!/bin/sh
# The smoke test of the image of FireChart Viewer (the Dockerfile of the root): it starts, serves its files and answers as the Viewer needs of its host. The
# requests are made inside the container (busybox wget of the image), so no port is published. GitHub runs it before it publishes the image.
#
#   sh docker/smoke.sh <image>
set -eu
# Git Bash on Windows would turn the paths of the commands run in the container (/dev/null) into Windows paths
export MSYS_NO_PATHCONV=1

image="${1:?usage: smoke.sh <image>}"
container="$(docker run -d "$image")"
finish() {
    code=$?
    [ "$code" -eq 0 ] || docker logs "$container" 2>&1 | tail -20 >&2
    docker rm -f "$container" >/dev/null
    exit "$code"
}
trap finish EXIT INT TERM

get() {
    docker exec "$container" wget -q -O "${2:-/dev/null}" "http://127.0.0.1:8080$1"
}
headers() {
    docker exec "$container" wget -S -q -O /dev/null "http://127.0.0.1:8080$1" 2>&1
}
fail() {
    echo "FAIL $*" >&2
    exit 1
}

# The container answers
i=0
until get / 2>/dev/null; do
    i=$((i + 1))
    [ $i -lt 30 ] || fail "the container does not answer"
    sleep 1
done

# The files of the application
for path in / /viewer.html /bundle.html /developers.html /assets/site.css /robots.txt /sitemap.xml /img/og-viewer.png /playground /embed /embed-bundle /viewer.json /host.json /_framework/blazor.webassembly.js /_content/Flox.Shared/css/Flox.Shared.Tailwind.css \
    /_content/FireChart.Viewer/css/FireChart.Viewer.Tailwind.css /sdk/viewer.js /library/firechart.project.json \
    /library/components/Patient.tile.xml; do
    get "$path" || fail "$path"
    echo "ok   $path"
done

# What the Viewer needs of its host
get /embed - | grep -q '<base href="/"' || fail "/embed is not the shell"
echo "ok   /embed is the shell"
get /examples/bundle-example.json - | grep -q '"resourceType": "Bundle"' || fail "/examples/bundle-example.json is not the example Bundle of the playground"
echo "ok   the example Bundle of the playground"
headers / | grep -qi 'cache-control: no-cache' || fail "the page is not revalidated"
headers /sdk/viewer.js | grep -qi 'access-control-allow-origin: \*' || fail "the SDK may not be imported by another origin"
echo "ok   revalidated, the SDK open to every origin"
# A module script must come as JavaScript, or the browser refuses it (the SQLite worker imports the .mjs of sqlite-wasm)
headers /_content/Flox.Sqlite/lib/sqlite-wasm/dist/index.mjs | grep -qi 'content-type: text/javascript' || fail "the .mjs of sqlite-wasm is not JavaScript"
echo "ok   .mjs as text/javascript"
get /library/firechart.project.json - | grep -q '"name": "firechart-vendor"' || fail "/library/firechart.project.json is not the manifest of the vendor library"
echo "ok   the vendor library"

# A missing file is a 404, never the page
if get /_framework/missing.wasm 2>/dev/null; then fail "a missing file of the framework is not a 404"; fi
if get /library/components/NoSuchType.tile.xml 2>/dev/null; then fail "a missing file of the library is not a 404"; fi
echo "ok   a missing file is a 404"

echo "smoke test passed: $image"
