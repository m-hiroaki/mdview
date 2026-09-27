#!/usr/bin/env sh
set -eu

rid="${1:-}"
output_root="${2:-artifacts}"

case "$rid" in
  osx-arm64|osx-x64|win-x64|win-arm64) ;;
  *)
    printf '%s\n' "Usage: scripts/publish.sh <osx-arm64|osx-x64|win-x64|win-arm64> [output-directory]" >&2
    exit 2
    ;;
esac

publish_directory="$output_root/$rid"
archive="$output_root/mdview-$rid.zip"

rm -rf "$publish_directory" "$archive"
mkdir -p "$output_root"

dotnet publish src/mdview.Presentation/mdview.Presentation.csproj \
  --configuration Release \
  --runtime "$rid" \
  --self-contained true \
  --no-restore \
  --output "$publish_directory"

(cd "$publish_directory" && zip -qr "../$(basename "$archive")" .)
printf 'Created %s\n' "$archive"
