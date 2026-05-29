#Requires -Version 5.1
<#
.SYNOPSIS
    Automated E2E verification for ClaudePortable backup/restore roundtrip.
.DESCRIPTION
    Validates that a restored backup ZIP contains expected files, correct
    exclusions, and consistent manifest data. Designed to run on VM-B
    after a restore from VM-A in the OneDrive roundtrip test playbook.

    Usage:
        .\e2e-verify.ps1 -BackupZip "C:\OneDrive\ClaudePortable\claude-backup_20260529.zip"
        .\e2e-verify.ps1 -BackupZip ".\backup.zip" -RestoreDir "C:\Temp\restore-check"
.PARAMETER BackupZip
    Path to the backup ZIP file to verify.
.PARAMETER RestoreDir
    Directory where the ZIP will be extracted for inspection. Defaults to
    a temp folder under $env:TEMP\ClaudePortable\e2e-verify-<guid>.
.PARAMETER ExpectedMcpServers
    Optional JSON array of expected MCP server keys (from pre-backup capture).
    Used to assert that mcpServers in claude_desktop_config.json match.
.EXAMPLE
    .\e2e-verify.ps1 -BackupZip ".\claude-backup_20260529.zip"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupZip,

    [string]$RestoreDir,

    [string[]]$ExpectedMcpServers
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Helpers ---
$passed = 0
$failed = 0
$warnings = 0

function Write-Result {
    param(
        [string]$Name,
        [bool]$Ok,
        [string]$Detail = '',
        [switch]$Warning
    )
    $icon = if ($Ok) { '[PASS]' } elseif ($Warning) { '[WARN]' } else { '[FAIL]' }
    $color = if ($Ok) { 'Green' } elseif ($Warning) { 'Yellow' } else { 'Red' }
    Write-Host "$icon $Name" -ForegroundColor $color
    if ($Detail) { Write-Host "       $Detail" -ForegroundColor DarkGray }
    if ($Ok) { $script:passed++ }
    elseif ($Warning) { $script:warnings++ }
    else { $script:failed++ }
}

function Test-PathExists {
    param([string]$Path, [string]$Label)
    if (Test-Path $Path -PathType Leaf) { return $true }
    Write-Host "       Expected file not found: $Path" -ForegroundColor DarkRed
    return $false
}

function Test-DirExists {
    param([string]$Path, [string]$Label)
    if (Test-Path $Path -PathType Container) { return $true }
    Write-Host "       Expected directory not found: $Path" -ForegroundColor DarkRed
    return $false
}

# --- Pre-flight ---
Write-Host ''
Write-Host '========================================' -ForegroundColor Cyan
Write-Host '  ClaudePortable E2E Verification' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan
Write-Host ''

if (-not (Test-Path $BackupZip)) {
    Write-Error "Backup ZIP not found: $BackupZip"
    exit 1
}

$zipSha = (Get-FileHash $BackupZip -Algorithm SHA256).Hash.ToLower()
Write-Host "[INFO] ZIP SHA-256: $zipSha" -ForegroundColor DarkCyan

if (-not $RestoreDir) {
    $RestoreDir = Join-Path $env:TEMP "ClaudePortable\e2e-verify-$((New-Guid).ToString('N'))"
}
New-Item -ItemType Directory -Force -Path $RestoreDir | Out-Null

# --- 1. Extract ZIP ---
Write-Host '[1/6] Extracting backup ZIP...' -ForegroundColor Yellow
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($BackupZip, $RestoreDir)
    Write-Result 'ZIP extraction' $true
} catch {
    Write-Result 'ZIP extraction' $false "Could not extract: $_"
    exit 2
}

# --- 2. Validate manifest.json ---
Write-Host '[2/6] Validating manifest...' -ForegroundColor Yellow
$manifestPath = Join-Path $RestoreDir 'manifest.json'
if (-not (Test-PathExists $manifestPath 'manifest.json')) {
    Write-Result 'Manifest validation' $false 'manifest.json not found in ZIP root'
} else {
    try {
        $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
        Write-Result 'Manifest JSON parse' $true

        # Schema version
        if ($manifest.schemaVersion -ge 1) {
            Write-Result 'Schema version' $true "v$($manifest.schemaVersion)"
        } else {
            Write-Result 'Schema version' $false "Unexpected schemaVersion: $($manifest.schemaVersion)"
        }

        # Required fields
        $requiredFields = @('createdAt', 'hostname', 'windowsUser', 'retentionTier', 'sourcePaths', 'archiveTargets', 'sizeBytes', 'fileCount', 'sha256')
        foreach ($field in $requiredFields) {
            if ($null -ne $manifest.$field) {
                Write-Result "Manifest field: $field" $true
            } else {
                Write-Result "Manifest field: $field" $false 'Field is null or missing'
            }
        }

        # SHA-256 consistency check
        if ($manifest.sha256 -and $manifest.sha256.ToLower() -eq $zipSha) {
            Write-Result 'SHA-256 integrity' $true 'ZIP hash matches manifest'
        } elseif ($manifest.sha256) {
            Write-Result 'SHA-256 integrity' $false "Expected $($manifest.sha256), got $zipSha"
        } else {
            Write-Result 'SHA-256 integrity' $false 'Manifest has no sha256 field' -Warning
        }

        # Source paths sanity
        if ($manifest.sourcePaths.Count -gt 0) {
            Write-Result 'Source paths populated' $true "$($manifest.sourcePaths.Count) entries"
        } else {
            Write-Result 'Source paths populated' $false 'No source paths in manifest'
        }

        # Archive targets sanity
        if ($manifest.archiveTargets.Count -gt 0) {
            Write-Result 'Archive targets populated' $true "$($manifest.archiveTargets.Count) entries"
        } else {
            Write-Result 'Archive targets populated' $false 'No archive targets in manifest'
        }

    } catch {
        Write-Result 'Manifest validation' $false "Parse error: $_"
    }
}

# --- 3. Check expected backup content ---
Write-Host '[3/6] Checking backup content...' -ForegroundColor Yellow
$expectedDirs = @(
    @{ Path = 'claude-desktop/appdata'; Label = 'Claude Desktop appdata' },
    @{ Path = 'claude-code/dotclaude'; Label = 'Claude Code .claude' }
)

foreach ($item in $expectedDirs) {
    $zipPath = Join-Path $RestoreDir "$($item.Path)"
    if (Test-DirExists $zipPath $item.Label) {
        $fileCount = (Get-ChildItem $zipPath -Recurse -File).Count
        Write-Result "Content: $($item.Label)" $true "$fileCount files"
    } else {
        Write-Result "Content: $($item.Label)" $false 'Directory not found in backup'
    }
}

# --- 4. Check credential exclusions ---
Write-Host '[4/6] Checking credential exclusions...' -ForegroundColor Yellow
$excludedPatterns = @(
    @{ Pattern = '**/tokens.dat'; Label = 'OAuth tokens (tokens.dat)' },
    @{ Pattern = '**/Login Data*'; Label = 'Browser login data' },
    @{ Pattern = '**/Cookies*'; Label = 'Browser cookies' },
    @{ Pattern = '**/config.json'; Label = 'Claude config with tokenCache' }
)

foreach ($item in $excludedPatterns) {
    $matches = Get-ChildItem -Path $RestoreDir -Recurse -File -Filter $item.Pattern -ErrorAction SilentlyContinue
    if ($matches.Count -eq 0) {
        Write-Result "Exclusion: $($item.Label)" $true 'Not found (correctly excluded)'
    } else {
        Write-Result "Exclusion: $($item.Label)" $false "Found $($matches.Count) file(s): $($matches.FullName -join ', ')"
    }
}

# --- 5. MCP server verification ---
Write-Host '[5/6] Checking MCP servers...' -ForegroundColor Yellow
$configPath = Join-Path $RestoreDir 'claude-desktop/appdata/claude_desktop_config.json'
if (Test-PathExists $configPath 'claude_desktop_config.json') {
    try {
        $config = Get-Content $configPath -Raw | ConvertFrom-Json
        if ($null -ne $config.mcpServers) {
            $mcpKeys = @($config.mcpServers.PSObject.Properties.Name)
            Write-Result 'MCP servers in config' $true "$($mcpKeys.Count) server(s): $($mcpKeys -join ', ')"

            if ($ExpectedMcpServers.Count -gt 0) {
                $missing = $ExpectedMcpServers | Where-Object { $_ -notin $mcpKeys }
                if ($missing.Count -eq 0) {
                    Write-Result 'MCP servers match expected' $true 'All expected servers present'
                } else {
                    Write-Result 'MCP servers match expected' $false "Missing: $($missing -join ', ')"
                }
            }
        } else {
            Write-Result 'MCP servers in config' $false 'mcpServers key not found' -Warning
        }
    } catch {
        Write-Result 'MCP servers parse' $false "Parse error: $_" -Warning
    }
} else {
    Write-Result 'claude_desktop_config.json' $false 'Not found in backup' -Warning
}

# --- 6. Post-restore checklist ---
Write-Host '[6/6] Checking post-restore checklist...' -ForegroundColor Yellow
$checklistPattern = 'post-restore-checklist-*.md'
$checklistFiles = Get-ChildItem -Path $env:LOCALAPPDATA\ClaudePortable -Filter $checklistPattern -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending

if ($checklistFiles.Count -gt 0) {
    $latestChecklist = $checklistFiles[0]
    Write-Result 'Post-restore checklist exists' $true $latestChecklist.Name

    $content = Get-Content $latestChecklist.FullName -Raw
    $requiredSections = @('Required Steps', 'Safety Backups', 'Troubleshooting')
    foreach ($section in $requiredSections) {
        if ($content -match [regex]::Escape($section)) {
            Write-Result "Checklist section: $section" $true
        } else {
            Write-Result "Checklist section: $section" $false 'Section not found'
        }
    }
} else {
    Write-Result 'Post-restore checklist exists' $false 'No checklist file found in %LOCALAPPDATA%\ClaudePortable\' -Warning
}

# --- Summary ---
Write-Host ''
Write-Host '========================================' -ForegroundColor Cyan
Write-Host "  Results: $passed passed, $failed failed, $warnings warnings" -ForegroundColor $(if ($failed -eq 0) { 'Green' } else { 'Red' })
Write-Host '========================================' -ForegroundColor Cyan
Write-Host ''

# Cleanup
try { Remove-Item $RestoreDir -Recurse -Force -ErrorAction SilentlyContinue } catch { }

exit $(if ($failed -gt 0) { 1 } else { 0 })
