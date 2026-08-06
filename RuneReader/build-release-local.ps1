# build-release-local.ps1
# RuneReader local VeloPack release build.
#
# Builds a self-contained win-x64 VeloPack installer and update feed.
# The feed is staged in bin\feed\stable so delta packages can be generated
# from previous releases. Use -Deploy to copy the feed and download page to
# the local web host path.
#
# Usage:
#   .\build-release-local.ps1
#   .\build-release-local.ps1 -Deploy
#   .\build-release-local.ps1 -NoIncrement
#   .\build-release-local.ps1 -Version "1.1.0" -NoIncrement

[CmdletBinding()]
param(
    [switch]$Deploy = $true,
    [switch]$SkipSign = $true,
    [switch]$NoIncrement,
    [string]$Version = "",
    [string]$FeedBaseUrl = "https://www.mkfam.com/runereader/",
    [string]$LocalCopyPath = "X:\dataStore\webhost\runereader",
    [string]$AppId = "RuneReader",
    [string]$AppFriendlyName = "Rune Reader",
    [string]$CertSubject = "Michael Sutton",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [int]$KeepMaxReleases = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Channel = "stable"
$ProjectFile = Join-Path $PSScriptRoot "RuneReader.csproj"
$VersionFile = Join-Path $PSScriptRoot "VERSION"
$PublishDir = Join-Path $PSScriptRoot "bin\publish-local\stable"
$FeedDir = Join-Path $PSScriptRoot "bin\feed\stable"
$TemplateFile = Join-Path $PSScriptRoot "index.template.html"
$FeedUrl = $FeedBaseUrl.TrimEnd('/') + '/' + $Channel + '/'

function Assert-VersionFormat([string]$Value) {
    if ($Value -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
        throw "Invalid version '$Value'. Expected Major.Minor.Patch, for example 1.0.0."
    }
}

function Read-VersionFile {
    if (-not (Test-Path $VersionFile)) {
        Set-Content -Path $VersionFile -Value "1.0.0" -Encoding ascii
    }

    $value = (Get-Content -Path $VersionFile -Raw).Trim()
    Assert-VersionFormat $value
    return $value
}

function Get-NextVersion([string]$Value) {
    Assert-VersionFormat $Value
    $parts = $Value.Split('.') | ForEach-Object { [int]$_ }
    $major = $parts[0]
    $minor = $parts[1]
    $patch = $parts[2]

    if ($patch -lt 9) {
        $patch++
    } else {
        $patch = 0
        $minor++
    }

    return "$major.$minor.$patch"
}

function Write-VersionFile([string]$Value) {
    Assert-VersionFormat $Value
    Set-Content -Path $VersionFile -Value $Value -Encoding ascii
}

function Find-SignTool {
    $sdkBin = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (Test-Path $sdkBin) {
        $found = Get-ChildItem $sdkBin -Directory | Sort-Object Name -Descending |
                 ForEach-Object { Join-Path $_.FullName "x64\signtool.exe" } |
                 Where-Object { Test-Path $_ } |
                 Select-Object -First 1
        if ($found) { return $found }
    }
    return $null
}

function Sign-File([string]$FilePath) {
    if ($SkipSign) {
        Write-Host "  [SKIP SIGN] $FilePath" -ForegroundColor Yellow
        return
    }

    $signTool = Find-SignTool
    if (-not $signTool) {
        Write-Warning "signtool not found. Skipping signing."
        return
    }

    Write-Host "  Signing: $FilePath" -ForegroundColor Cyan
    if ($PfxPath -and (Test-Path $PfxPath)) {
        $signArgs = @("sign", "/fd", "sha256", "/tr", "http://ts.ssl.com", "/td", "sha256",
                      "/f", $PfxPath, "/p", $PfxPassword, "/d", $AppFriendlyName, $FilePath)
    } else {
        $signArgs = @("sign", "/fd", "sha256", "/tr", "http://ts.ssl.com", "/td", "sha256",
                      "/n", $CertSubject, "/d", $AppFriendlyName, $FilePath)
    }

    & $signTool @signArgs
    if ($LASTEXITCODE -ne 0) { throw "signtool failed for: $FilePath" }
}

function Invoke-Publish([string]$ReleaseVersion) {
    Write-Host ""
    Write-Host "Publishing RuneReader $ReleaseVersion to $PublishDir" -ForegroundColor Green

    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force
    }

    $publishArgs = @(
        "publish", $ProjectFile,
        "--configuration", "Release",
        "--runtime", "win-x64",
        "--self-contained", "true",
        "--output", $PublishDir,
        "-p:PublishSingleFile=false",
        "-p:PublishTrimmed=false",
        "-p:PublishReadyToRun=false",
        "-p:GenerateManifests=false",
        "-p:SignManifests=false",
        "-p:DebugType=none",
        "-p:DebugSymbols=false",
        "-p:Version=$ReleaseVersion",
        "-p:AssemblyVersion=$ReleaseVersion.0",
        "-p:FileVersion=$ReleaseVersion.0",
        "-p:UpdateFeedUrl=$FeedUrl"
    )

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

    $exe = Join-Path $PublishDir "RuneReader.exe"
    if (Test-Path $exe) { Sign-File $exe }
}

function Invoke-VpkPack([string]$ReleaseVersion) {
    Write-Host ""
    Write-Host "Packaging with vpk channel=$Channel" -ForegroundColor Green

    New-Item -ItemType Directory -Path $FeedDir -Force | Out-Null

    $vpkArgs = @(
        "pack",
        "--packId", $AppId,
        "--packVersion", $ReleaseVersion,
        "--packDir", $PublishDir,
        "--outputDir", $FeedDir,
        "--mainExe", "RuneReader.exe",
        "--packTitle", $AppFriendlyName,
        "--channel", $Channel,
        "--delta", "BestSpeed",
        "--exclude", ".*\.(so(\.\d+)*|dylib)$"
    )

    & vpk "-y" @vpkArgs
    if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

    $setup = Get-ChildItem $FeedDir -Filter "*Setup*.exe" |
             Where-Object { $_.Name -notlike "*-$ReleaseVersion-*" -and $_.Name -ne "RuneReaderSetup.exe" } |
             Select-Object -First 1
    if ($setup) {
        Sign-File $setup.FullName
        $newName = "$AppId-$ReleaseVersion-$Channel-Setup.exe"
        $newPath = Join-Path $FeedDir $newName
        if (Test-Path $newPath) { Remove-Item $newPath -Force }
        Rename-Item $setup.FullName $newPath
        Write-Host "  Installer: $newName" -ForegroundColor Green
    }

    $latestSetup = Get-ChildItem $FeedDir -Filter "$AppId-$ReleaseVersion-$Channel-Setup.exe" | Select-Object -First 1
    if ($latestSetup) {
        Copy-Item $latestSetup.FullName (Join-Path $FeedDir "RuneReaderSetup.exe") -Force
    }
}

function Invoke-PruneFeed {
    Write-Host ""
    Write-Host "Pruning feed to last $KeepMaxReleases releases" -ForegroundColor Green

    if ($KeepMaxReleases -lt 1) { return }

    $versions = Get-ChildItem $FeedDir -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^RuneReader-(\d+\.\d+\.\d+)-stable' } |
        ForEach-Object { $Matches[1] } |
        Sort-Object {[version]$_} -Descending |
        Select-Object -Unique

    $keep = @($versions | Select-Object -First $KeepMaxReleases)
    Get-ChildItem $FeedDir -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -match '^RuneReader-(\d+\.\d+\.\d+)-stable' -and
            ($keep -notcontains $Matches[1])
        } |
        ForEach-Object {
            Write-Host "  Removing old release file: $($_.Name)" -ForegroundColor DarkGray
            Remove-Item $_.FullName -Force
        }
}

function Write-DownloadPage([string]$ReleaseVersion) {
    if (-not (Test-Path $TemplateFile)) {
        Write-Warning "index.template.html not found. Skipping download page generation."
        return
    }

    $html = Get-Content $TemplateFile -Raw
    $html = $html.Replace("{{VERSION}}", $ReleaseVersion)
    $html = $html.Replace("{{FEED_URL}}", $FeedUrl)
    $html = $html.Replace("{{SETUP_FILE}}", "stable/RuneReaderSetup.exe")
    $html = $html.Replace("{{CHANNEL}}", $Channel)

    $outFile = Join-Path $PSScriptRoot "bin\feed\index.html"
    New-Item -ItemType Directory -Path (Split-Path $outFile -Parent) -Force | Out-Null
    Set-Content -Path $outFile -Value $html -Encoding ascii
}

function Invoke-Deploy {
    Write-Host ""
    Write-Host "Deploying to $LocalCopyPath" -ForegroundColor Green

    $targetFeed = Join-Path $LocalCopyPath $Channel
    New-Item -ItemType Directory -Path $targetFeed -Force | Out-Null

    Copy-Item (Join-Path $FeedDir "*") $targetFeed -Recurse -Force

    $index = Join-Path $PSScriptRoot "bin\feed\index.html"
    if (Test-Path $index) {
        Copy-Item $index (Join-Path $LocalCopyPath "index.html") -Force
    }

    $icon = Join-Path $PSScriptRoot "Icons\RuneReader_512x512.png"
    if (Test-Path $icon) {
        Copy-Item $icon (Join-Path $LocalCopyPath "RuneReader_512x512.png") -Force
    }
}

$releaseVersion = if ([string]::IsNullOrWhiteSpace($Version)) { Read-VersionFile } else { $Version.Trim() }
Assert-VersionFormat $releaseVersion
$nextVersion = Get-NextVersion $releaseVersion

Write-Host "RuneReader release version: $releaseVersion" -ForegroundColor Cyan
Write-Host "Update feed URL: $FeedUrl" -ForegroundColor Cyan
if ($NoIncrement -or -not [string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "VERSION file will not be incremented." -ForegroundColor Yellow
} else {
    Write-Host "VERSION file will be updated to $nextVersion after success." -ForegroundColor Cyan
}

Invoke-Publish $releaseVersion
Invoke-VpkPack $releaseVersion
Invoke-PruneFeed
Write-DownloadPage $releaseVersion
if ($Deploy) { Invoke-Deploy }

if (-not $NoIncrement -and [string]::IsNullOrWhiteSpace($Version)) {
    Write-VersionFile $nextVersion
    Write-Host "VERSION updated to $nextVersion" -ForegroundColor Green
}

Write-Host ""
Write-Host "Release build complete." -ForegroundColor Green
