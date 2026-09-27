#!/usr/bin/env sh
set -eu

rid="${1:-}"
output_root="${2:-artifacts}"
aot="${3:-}"

case "$rid" in
  osx-arm64|osx-x64|win-x64|win-arm64) ;;
  *)
    printf '%s\n' "Usage: scripts/publish.sh <osx-arm64|osx-x64|win-x64|win-arm64> [output-directory] [--aot]" >&2
    exit 2
    ;;
esac

if [ -n "$aot" ] && [ "$aot" != "--aot" ]; then
  printf '%s\n' "The third argument must be --aot when provided." >&2
  exit 2
fi

publish_properties=""
if [ "$aot" = "--aot" ]; then
  publish_properties="-p:PublishAot=true -p:InvariantGlobalization=true"
fi

publish_directory="$output_root/$rid"
archive="$output_root/mdview-$rid.zip"

rm -rf "$publish_directory" "$archive"
mkdir -p "$output_root"

dotnet restore src/mdview.Presentation/mdview.Presentation.csproj \
  --runtime "$rid" \
  $publish_properties

dotnet publish src/mdview.Presentation/mdview.Presentation.csproj \
  --configuration Release \
  --runtime "$rid" \
  --self-contained true \
  --no-restore \
  $publish_properties \
  --output "$publish_directory"

(cd "$publish_directory" && zip -qr "../$(basename "$archive")" .)
printf 'Created %s\n' "$archive"
