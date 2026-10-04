param(
    [string]$OutputRoot = "coverage"
)

$ErrorActionPreference = "Stop"

dotnet restore mdview.sln
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
dotnet tool restore --tool-manifest .config/dotnet-tools.json
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (Test-Path $OutputRoot) {
    Remove-Item -Recurse -Force $OutputRoot
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$projects = @(
    "tests/mdview.Domain.Tests/mdview.Domain.Tests.csproj",
    "tests/mdview.Application.Tests/mdview.Application.Tests.csproj",
    "tests/mdview.Infrastructure.Tests/mdview.Infrastructure.Tests.csproj"
)

foreach ($project in $projects) {
    $projectName = Split-Path (Split-Path $project -Parent) -Leaf
    $resultsDirectory = Join-Path $OutputRoot $projectName
    dotnet test $project `
        --configuration Release `
        --no-restore `
        --coverage `
        --coverage-output-format cobertura `
        --coverage-output coverage.cobertura.xml `
        --results-directory $resultsDirectory
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

dotnet tool run reportgenerator `
    "-reports:$OutputRoot/**/coverage.cobertura.xml" `
    "-targetdir:$OutputRoot/report" `
    "-reporttypes:Html;TextSummary"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Output "Coverage reports: $OutputRoot"
Write-Output "HTML report: $OutputRoot/report/index.html"
