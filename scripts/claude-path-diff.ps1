<#
.SYNOPSIS
    Take before/after snapshots of Claude-related directories around a
    scenario (cold start, new chat, etc.) and emit a diff of paths that
    were created or modified. Lightweight alternative to ProcMon for the
    narrow question "which persistent state changes on disk?". No admin
    required.

.PARAMETER Scenario
    Tag for the output files. e.g. "coldstart", "idle60s", "newchat".

.PARAMETER Duration
    Seconds to wait between the two snapshots while the scenario runs.

.PARAMETER OutputDir
    Where the before/after CSVs and the diff report are written.

.PARAMETER AutoLaunch
    Kill any running Claude Desktop, take the "before" snapshot, launch
    the Store app via its AUMID, wait $Duration seconds, take the "after"
    snapshot. Without this flag the script assumes the scenario runs
    manually between the two snapshots and waits $Duration seconds.

.EXAMPLE
    pwsh .\claude-path-diff.ps1 -Scenario coldstart -Duration 60 -AutoLaunch

.EXAMPLE
    pwsh .\claude-path-diff.ps1 -Scenario newchat -Duration 45
#>

param(
  [Parameter(Mandatory)] [string] $Scenario,
  [int] $Duration = 60,
  [string] $OutputDir = 'C:\Temp\ClaudePathDiff',
  [switch] $AutoLaunch
)

$ErrorActionPreference = "Stop"

$appdata  = [Environment]::GetFolderPath("ApplicationData")
$localapp = [Environment]::GetFolderPath("LocalApplicationData")
$user     = [Environment]::GetFolderPath("UserProfile")

$watchTargets = @(
  (Join-Path $appdata  "Claude"),
  (Join-Path $localapp "Claude"),
  (Join-Path $user     ".claude"),
  (Join-Path $user     ".cowork")
)
# Add any %LOCALAPPDATA%\Packages\Claude_* dirs for Store-installed variant.
$packagesRoot = Join-Path $localapp "Packages"
if (Test-Path $packagesRoot) {
  Get-ChildItem -Path $packagesRoot -Directory -Filter "Claude*" -ErrorAction SilentlyContinue |
    ForEach-Object { $watchTargets += $_.FullName }
}
$watchTargets = $watchTargets | Where-Object { $_ -and (Test-Path $_) }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$beforeCsv = Join-Path $OutputDir "$Scenario.before.csv"
$afterCsv  = Join-Path $OutputDir "$Scenario.after.csv"
$diffFile  = Join-Path $OutputDir "$Scenario.diff.txt"

function Take-Snapshot {
  param([string] $Tag, [string] $OutCsv)

  Write-Host "  Snapshot ($Tag) ..." -ForegroundColor DarkCyan -NoNewline
  $rows = New-Object System.Collections.ArrayList

  foreach ($root in $watchTargets) {
    try {
      Get-ChildItem -Path $root -Recurse -Force -File -ErrorAction SilentlyContinue |
        ForEach-Object {
          [void]$rows.Add([pscustomobject]@{
            Path      = $_.FullName
            Size      = $_.Length
            LastWrite = $_.LastWriteTimeUtc.ToString("o")
          })
        }
    }
    catch {
      # reparse points etc - ignore
    }
  }

  $rows | Export-Csv -NoTypeInformation -Encoding utf8 -Path $OutCsv
  Write-Host " $($rows.Count) files" -ForegroundColor Green
}

Write-Host "Watch targets:" -ForegroundColor Cyan
$watchTargets | ForEach-Object { Write-Host "  $_" }
Write-Host ""

if ($AutoLaunch) {
  Write-Host "Killing any running Claude Desktop ..." -ForegroundColor Yellow
  Get-Process -Name "Claude","ClaudeCli" -ErrorAction SilentlyContinue | ForEach-Object {
    try { $_ | Stop-Process -Force -ErrorAction SilentlyContinue } catch {}
  }
  Start-Sleep -Seconds 2
}

Write-Host "Taking BEFORE snapshot ..." -ForegroundColor Cyan
Take-Snapshot -Tag "before" -OutCsv $beforeCsv

if ($AutoLaunch) {
  Write-Host "Launching Claude Desktop via explorer.exe shell: URI ..." -ForegroundColor Yellow
  # The Store app installs with AUMID Claude_pzs8sxrjxfjjc!App (confirmed via Get-AppxPackage).
  try {
    Start-Process "explorer.exe" -ArgumentList 'shell:AppsFolder\Claude_pzs8sxrjxfjjc!App'
  }
  catch {
    Write-Host "Could not auto-launch Claude Desktop. Start it manually and press Enter." -ForegroundColor Red
    $null = Read-Host
  }
}
else {
  Write-Host "Now run the scenario. I will wait $Duration seconds." -ForegroundColor Yellow
}

Write-Host "Waiting $Duration seconds for scenario to finish ..." -ForegroundColor Cyan
Start-Sleep -Seconds $Duration

Write-Host "Taking AFTER snapshot ..." -ForegroundColor Cyan
Take-Snapshot -Tag "after" -OutCsv $afterCsv

# Diff
Write-Host ""
Write-Host "Computing diff ..." -ForegroundColor Cyan

$before = @{}
Import-Csv $beforeCsv | ForEach-Object { $before[$_.Path] = $_ }
$after = @{}
Import-Csv $afterCsv | ForEach-Object { $after[$_.Path] = $_ }

$added     = @()
$modified  = @()
$removed   = @()

foreach ($path in $after.Keys) {
  if (-not $before.ContainsKey($path)) {
    $added += $path
  }
  elseif ($before[$path].Size -ne $after[$path].Size -or
          $before[$path].LastWrite -ne $after[$path].LastWrite) {
    $modified += $path
  }
}
foreach ($path in $before.Keys) {
  if (-not $after.ContainsKey($path)) {
    $removed += $path
  }
}

# Write diff report
$report = @()
$report += "# Claude path diff: scenario=$Scenario duration=${Duration}s autoLaunch=$AutoLaunch"
$report += "# Before: $($before.Count) files. After: $($after.Count) files."
$report += "# Added: $($added.Count). Modified: $($modified.Count). Removed: $($removed.Count)."
$report += ""
$report += "## ADDED ($($added.Count))"
$report += ($added | Sort-Object)
$report += ""
$report += "## MODIFIED ($($modified.Count))"
$report += ($modified | Sort-Object)
$report += ""
$report += "## REMOVED ($($removed.Count))"
$report += ($removed | Sort-Object)

$report | Out-File -Encoding utf8 -FilePath $diffFile

Write-Host ""
Write-Host "Summary:" -ForegroundColor Green
Write-Host "  added:    $($added.Count)"
Write-Host "  modified: $($modified.Count)"
Write-Host "  removed:  $($removed.Count)"
Write-Host "  report:   $diffFile"
