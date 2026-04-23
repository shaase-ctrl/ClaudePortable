#Requires -Version 7
<#
.SYNOPSIS
    Capture every file write under %APPDATA%\Claude, %LOCALAPPDATA%\Claude,
    %LOCALAPPDATA%\Packages\Claude*, and %USERPROFILE%\.claude for a fixed
    duration. Lightweight alternative to ProcMon when we only care about
    file-system paths (no admin required).

.PARAMETER DurationSeconds
    How long to watch before auto-stopping.

.PARAMETER OutputCsv
    Path to write the captured events to.

.EXAMPLE
    pwsh .\watch-claude-paths.ps1 -DurationSeconds 60 -OutputCsv C:\Temp\coldstart.csv
#>

param(
  [int] $DurationSeconds = 60,
  [Parameter(Mandatory)] [string] $OutputCsv
)

$ErrorActionPreference = "Stop"

$appdata  = [Environment]::GetFolderPath("ApplicationData")
$localapp = [Environment]::GetFolderPath("LocalApplicationData")
$user     = [Environment]::GetFolderPath("UserProfile")

$watchTargets = @(
  (Join-Path $appdata  "Claude"),
  (Join-Path $localapp "Claude"),
  (Join-Path $user     ".claude"),
  (Join-Path $user     ".cowork"),
  (Get-ChildItem -Path (Join-Path $localapp "Packages") -Directory -Filter "Claude*" -ErrorAction SilentlyContinue | ForEach-Object FullName)
) | Where-Object { $_ -and (Test-Path $_) }

Write-Host "Watching $($watchTargets.Count) target(s) for $DurationSeconds s:" -ForegroundColor Cyan
$watchTargets | ForEach-Object { Write-Host "  $_" }

$events = New-Object System.Collections.ArrayList
$watchers = @()

foreach ($target in $watchTargets) {
  $w = New-Object System.IO.FileSystemWatcher
  $w.Path = $target
  $w.IncludeSubdirectories = $true
  $w.NotifyFilter = [System.IO.NotifyFilters]'FileName, LastWrite, Size, DirectoryName'
  $w.EnableRaisingEvents = $true

  $null = Register-ObjectEvent -InputObject $w -EventName Created -MessageData $target -Action {
    $null = $event.MessageData
    $line = [pscustomobject]@{
      Timestamp = (Get-Date).ToString("o")
      Event = 'Created'
      Path = $EventArgs.FullPath
    }
    $line | Export-Csv -Append -NoTypeInformation -Path $using:OutputCsv
  }
  $null = Register-ObjectEvent -InputObject $w -EventName Changed -MessageData $target -Action {
    $null = $event.MessageData
    $line = [pscustomobject]@{
      Timestamp = (Get-Date).ToString("o")
      Event = 'Changed'
      Path = $EventArgs.FullPath
    }
    $line | Export-Csv -Append -NoTypeInformation -Path $using:OutputCsv
  }
  $null = Register-ObjectEvent -InputObject $w -EventName Renamed -MessageData $target -Action {
    $null = $event.MessageData
    $line = [pscustomobject]@{
      Timestamp = (Get-Date).ToString("o")
      Event = 'Renamed'
      Path = $EventArgs.FullPath
    }
    $line | Export-Csv -Append -NoTypeInformation -Path $using:OutputCsv
  }
  $watchers += $w
}

if (Test-Path $OutputCsv) { Remove-Item $OutputCsv -Force }
'Timestamp,Event,Path' | Out-File -FilePath $OutputCsv -Encoding utf8

Start-Sleep -Seconds $DurationSeconds

foreach ($w in $watchers) { $w.EnableRaisingEvents = $false; $w.Dispose() }
Get-EventSubscriber | Unregister-Event -Force

Write-Host ""
$rows = Import-Csv $OutputCsv
Write-Host "Captured $($rows.Count) events. Unique paths:" -ForegroundColor Green
$rows | Select-Object -ExpandProperty Path | Sort-Object -Unique | Measure-Object | Select-Object -ExpandProperty Count
