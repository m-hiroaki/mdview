#!/usr/bin/env sh
set -eu

output_root="${1:-coverage}"
dotnet restore mdview.sln
dotnet tool restore --tool-manifest .config/dotnet-tools.json

rm -rf "$output_root"
mkdir -p "$output_root"

for project in \
  tests/mdview.Domain.Tests/mdview.Domain.Tests.csproj \
  tests/mdview.Application.Tests/mdview.Application.Tests.csproj \
  tests/mdview.Infrastructure.Tests/mdview.Infrastructure.Tests.csproj
do
  project_name=$(basename "$(dirname "$project")")
  dotnet test "$project" \
    --configuration Release \
    --no-restore \
    --coverage \
    --coverage-output-format cobertura \
    --coverage-output coverage.cobertura.xml \
    --results-directory "$output_root/$project_name"
done

dotnet tool run reportgenerator \
  "-reports:$output_root/**/coverage.cobertura.xml" \
  "-targetdir:$output_root/report" \
  '-reporttypes:Html;TextSummary'

printf 'Coverage reports: %s\n' "$output_root"
printf 'HTML report: %s/report/index.html\n' "$output_root"
