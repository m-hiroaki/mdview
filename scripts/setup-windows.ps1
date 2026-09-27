[CmdletBinding()]
param(
    [switch]$SkipRestore
)

$ErrorActionPreference = "Stop"

function Assert-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name was not found. Install or enable App Installer, then run this script again."
    }
}

function Install-WingetPackage {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [string]$Override
    )

    $arguments = @(
        "install",
        "--id", $Id,
        "--exact",
        "--source", "winget",
        "--accept-package-agreements",
        "--accept-source-agreements"
    )

    if (-not [string]::IsNullOrWhiteSpace($Override)) {
        $arguments += @("--override", $Override)
    }

    Write-Host "Installing $Id..."
    & winget @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "winget failed to install $Id with exit code $LASTEXITCODE."
    }
}

Assert-Command "winget"

# Required for restore, build, test, and the pinned SDK in global.json.
Install-WingetPackage "Microsoft.DotNet.SDK.10"

# Required for source checkout and normal repository maintenance.
Install-WingetPackage "Git.Git"

# Required by Native AOT on Windows, including the win-arm64 linker.
$buildToolsOverride = @(
    "--wait",
    "--passive",
    "--add Microsoft.VisualStudio.Workload.VCTools",
    "--includeRecommended",
    "--add Microsoft.VisualStudio.Component.VC.Tools.ARM64",
    "--add Microsoft.VisualStudio.Component.VC.Tools.ARM64EC",
    "--add Microsoft.VisualStudio.Component.Windows10SDK.19041"
) -join " "
Install-WingetPackage "Microsoft.VisualStudio.2022.BuildTools" $buildToolsOverride

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $requiredSdk = (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version
    $installedSdks = @(dotnet --list-sdks)
    if (-not ($installedSdks -match [regex]::Escape($requiredSdk))) {
        throw ".NET SDK $requiredSdk from global.json was not found after installation. Installed SDKs: $($installedSdks -join '; ')"
    }

    if (-not $SkipRestore) {
        Write-Host "Restoring NuGet packages..."
        & dotnet restore mdview.sln
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed with exit code $LASTEXITCODE."
        }
    }
}
finally {
    Pop-Location
}

Write-Host "Windows development tools are ready."
Write-Host "For a normal build: dotnet build mdview.sln --configuration Release"
Write-Host "For Native AOT: .\scripts\publish.ps1 win-arm64 artifacts\aot -Aot"
