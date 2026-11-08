#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

manifest_name="$(python3 -c "import json;print(json.load(open('manifest.json'))['name'])")"
manifest_version="$(python3 -c "import json;print(json.load(open('manifest.json'))['version_number'])")"
csproj_version="$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' AlwaysBeSearchin.csproj | head -n 1)"

if [ "$manifest_version" != "$csproj_version" ]; then
  echo "Version mismatch: manifest.json has $manifest_version, AlwaysBeSearchin.csproj has $csproj_version" >&2
  exit 1
fi

dotnet build AlwaysBeSearchin.csproj -c Release -p:PulsarLocalMods=/nonexistent/skip

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

cp manifest.json README.md CHANGELOG.md icon.png "$stage/"
mkdir -p "$stage/plugins/AlwaysBeSearchin"
cp bin/Release/AlwaysBeSearchin.dll "$stage/plugins/AlwaysBeSearchin/AlwaysBeSearchin.dll"

mkdir -p dist
zip_path="$root/dist/SolrLabs-$manifest_name-$manifest_version.zip"
rm -f "$zip_path"
(cd "$stage" && zip -X -r "$zip_path" manifest.json README.md CHANGELOG.md icon.png plugins)

echo "$zip_path"
