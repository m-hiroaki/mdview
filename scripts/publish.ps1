[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet("osx-arm64", "osx-x64", "win-x64", "win-arm64")]
    [string]$Rid,

    [Parameter(Position = 1)]
    [string]$OutputRoot = "artifacts",

    [switch]$Aot
)

$ErrorActionPreference = "Stop"

$publishDirectory = Join-Path $OutputRoot $Rid
$archive = Join-Path $OutputRoot "mdview-$Rid.zip"
$project = "src/mdview.Presentation/mdview.Presentation.csproj"

if (Test-Path $publishDirectory) {
    Remove-Item -Recurse -Force $publishDirectory
}
if (Test-Path $archive) {
    Remove-Item -Force $archive
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$restoreArguments = @(
    "restore",
    $project,
    "--runtime", $Rid
)
if ($Aot) {
    $restoreArguments += "-p:PublishAot=true"
    $restoreArguments += "-p:InvariantGlobalization=true"
}
& dotnet @restoreArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$publishArguments = @(
    "publish",
    $project,
    "--configuration", "Release",
    "--runtime", $Rid,
    "--self-contained", "true",
    "--no-restore",
    "--output", $publishDirectory
)
if ($Aot) {
    $publishArguments += "-p:PublishAot=true"
    $publishArguments += "-p:InvariantGlobalization=true"
}
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $archive -Force
Write-Output "Created $archive"
