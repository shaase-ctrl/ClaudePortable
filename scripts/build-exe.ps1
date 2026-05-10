<#
.SYNOPSIS
    Build a portable single-file self-contained ClaudePortable.exe.
    No installer, no admin needed - double-click and it runs.

    The exe bundles the .NET 10 Windows Desktop runtime plus WPF native
    dependencies. First launch extracts the native bits to
    %LocalAppData%\.net\<app>\<hash>\; subsequent launches reuse the cache
    (sub-second startup).

.PARAMETER Version
    Version stamped into the output filename.

.PARAMETER Configuration
    Debug or Release (default Release).

.EXAMPLE
    pwsh .\build-exe.ps1 -Version 0.2.0
#>

param(
  [string] $Version = "0.2.0",
  [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$appCsproj = Join-Path $root "src\ClaudePortable.App\ClaudePortable.App.csproj"
$outDir = Join-Path $root "dist\portable"
$finalExeName = "ClaudePortable-$Version-portable.exe"
$finalExe = Join-Path $root $finalExeName

Write-Host "[1/3] Cleaning dist\portable..." -ForegroundColor Cyan
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

Write-Host "[2/3] Publishing single-file self-contained..." -ForegroundColor Cyan
dotnet publish $appCsproj `
  --configuration $Configuration `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:IncludeAllContentForSelfExtract=true `
  -p:DebugType=embedded `
  -p:InvariantGlobalization=false `
  -p:AssemblyVersion=$Version.0 `
  -p:FileVersion=$Version.0 `
  -p:InformationalVersion=$Version `
  --output $outDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "[3/3] Emitting portable exe + SHA256..." -ForegroundColor Cyan
$srcExe = Join-Path $outDir "claudeportable.exe"
if (-not (Test-Path $srcExe)) { throw "claudeportable.exe not found in $outDir" }
Copy-Item $srcExe $finalExe -Force

$hash = (Get-FileHash -Path $finalExe -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $finalExeName" | Out-File -FilePath "$finalExe.sha256" -Encoding ascii

$sizeMb = [math]::Round((Get-Item $finalExe).Length / 1MB, 1)
Write-Host ""
Write-Host "OK: $finalExe ($sizeMb MB)" -ForegroundColor Green
Write-Host "     SHA256: $hash" -ForegroundColor DarkGray
Write-Host "     SHA256 file: $finalExe.sha256" -ForegroundColor DarkGray
