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

    $installed = & winget list --id $Id --exact --disable-interactivity 2>$null | Out-String
    if ($LASTEXITCODE -eq 0 -and $installed -match [regex]::Escape($Id)) {
        Write-Host "$Id is already installed; skipping winget install."
        return
    }

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

function Get-RequiredSdkVersion {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $globalJsonPath = Join-Path $repoRoot "global.json"
    return (Get-Content $globalJsonPath -Raw | ConvertFrom-Json).sdk.version
}

function Test-DotNetSdkVersion {
    param([Parameter(Mandatory = $true)][string]$Version)

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        return $false
    }

    $installedSdks = @(dotnet --list-sdks)
    return $installedSdks -match [regex]::Escape($Version)
}

Assert-Command "winget"

# Required for restore, build, test, and the pinned SDK in global.json.
$requiredSdk = Get-RequiredSdkVersion
if (Test-DotNetSdkVersion $requiredSdk) {
    Write-Host ".NET SDK $requiredSdk is already installed; skipping winget install."
}
else {
    Install-WingetPackage "Microsoft.DotNet.SDK.10"
}

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
    $installedSdks = @(dotnet --list-sdks)
    if (-not ($installedSdks -match [regex]::Escape($requiredSdk))) {
        throw ".NET SDK $requiredSdk from global.json was not found. Winget may not publish that SDK patch for this architecture yet. Installed SDKs: $($installedSdks -join '; '). Install the exact SDK from https://dotnet.microsoft.com/download/dotnet/10.0 and run this script again."
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
