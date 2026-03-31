param (
    [string]$TargetDir = (Get-Location)
)

$ErrorActionPreference = "Stop"

Write-Host "=========================================="
Write-Host "    PRIZM SKILLS SYNC (Global + Local)"
Write-Host "=========================================="
Write-Host "Target: $TargetDir"
Write-Host ""

# --- HELPER FUNCTIONS ---
function Get-FrontMatter {
    param ($Content)
    $lines = $Content -split "`n"
    if ($lines[0].Trim() -eq '---') {
        $fm = @{}
        for ($i = 1; $i -lt $lines.Length; $i++) {
            if ($lines[$i].Trim() -eq '---') { break }
            if ($lines[$i] -match "^([^:]+):\s*(.*)$") {
                $fm[$matches[1].Trim()] = $matches[2].Trim()
            }
        }
        return $fm
    }
    return $null
}

function Sync-SkillsToAgents {
    param (
        [string]$SkillsDir,
        [string]$OutputFile,
        [string]$Header,
        [string]$InheritedText = ""
    )
    
    if (-not (Test-Path $SkillsDir)) {
        Write-Warning "Skills directory not found: $SkillsDir"
        return
    }
    
    $SkillFiles = Get-ChildItem -Path $SkillsDir -Recurse -Filter "SKILL.md" | Sort-Object FullName
    
    if ($SkillFiles.Count -eq 0) {
        Write-Warning "No skill files found in $SkillsDir"
        return
    }
    
    $SkillIndex = @()
    
    foreach ($File in $SkillFiles) {
        $Content = Get-Content -Path $File.FullName -Raw
        $FM = Get-FrontMatter -Content $Content
        
        $Name = $File.Directory.Name
        $Desc = "No description provided."
        $Trigger = "Manual activation."
        
        if ($FM) {
            if ($FM.name) { $Name = $FM.name }
            if ($FM.description) { $Desc = $FM.description }
        }
        
        $RelPath = $File.FullName.Substring($TargetDir.Length + 1).Replace("\", "/")
        $SkillIndex += "| **$Name** | $Trigger | ``$RelPath`` |"
    }
    
    $IndexText = $SkillIndex -join "`n"
    
    $AgentsContent = @"
$Header

## LOCAL SKILLS

| Skill | Trigger | Path |
|-------|---------|------|
$IndexText

$InheritedText
"@
    
    Set-Content -Path $OutputFile -Value $AgentsContent -Encoding UTF8
    Write-Host "  [OK] Updated $OutputFile ($($SkillFiles.Count) skills)"
}

# --- 1. SYNC GLOBAL SKILLS ---
Write-Host ""
Write-Host "=== Syncing Global Skills ==="

$GlobalSkillsDir = Join-Path $TargetDir "universal-skills"
$GlobalAgents = Join-Path $TargetDir "AGENTS.md"

# Already manually maintained, skip auto-generation for global
Write-Host "  [SKIP] Global AGENTS.md is manually maintained"

# --- 2. SYNC SHARED AGENT SKILLS ---
Write-Host ""
Write-Host "=== Syncing Shared Agent Skills ==="

$SharedSkillsDir = Join-Path $TargetDir ".agents\skills"
if (Test-Path $SharedSkillsDir) {
    $SharedCount = (Get-ChildItem $SharedSkillsDir -Directory).Count
    Write-Host "  Found $SharedCount shared skill folders"
}
else {
    Write-Host "  [WARN] Shared agent skills directory not found: .agents\skills"
}

# --- 3. SYNC HOST SKILLS ---
Write-Host ""
Write-Host "=== Syncing Host Skills (.NET) ==="

$HostSkillsDir = Join-Path $TargetDir "app.trading.algoritmico.api\.agents\skills"
$HostAgents = Join-Path $TargetDir "app.trading.algoritmico.api\AGENTS.md"

if (Test-Path $HostSkillsDir) {
    $HostCount = (Get-ChildItem $HostSkillsDir -Directory).Count
    Write-Host "  Found $HostCount skill folders"
}
else {
    Write-Host "  [WARN] Host skills directory not found: app.trading.algoritmico.api\.agents\skills"
}

# --- 4. SYNC WEB SKILLS ---
Write-Host ""
Write-Host "=== Syncing Web Skills (Angular) ==="

$WebSkillsDir = Join-Path $TargetDir "app.trading.algoritmico.web\skills"
$WebAgents = Join-Path $TargetDir "app.trading.algoritmico.web\AGENTS.md"

if (Test-Path $WebSkillsDir) {
    $WebCount = (Get-ChildItem $WebSkillsDir -Directory).Count
    Write-Host "  Found $WebCount skill folders"
}
else {
    Write-Host "  [WARN] Web skills directory not found: app.trading.algoritmico.web\skills"
}

# --- 5. SYNC TO CLAUDE CODE ---
Write-Host ""
Write-Host "=== Syncing to Claude Code (.claude/skills/) ==="

$ClaudeSkillsDir = Join-Path $TargetDir ".claude\skills"
if (-not (Test-Path $ClaudeSkillsDir)) {
    New-Item -ItemType Directory -Path $ClaudeSkillsDir -Force | Out-Null
}

$AllSkillDirs = @(
    $GlobalSkillsDir,
    $SharedSkillsDir,
    $HostSkillsDir,
    $WebSkillsDir
)

# Track which skills we sync so we can clean up stale ones and skip duplicates
$SyncedSkillNames = @{}

foreach ($Dir in $AllSkillDirs) {
    if (Test-Path $Dir) {
        $Skills = @(Get-ChildItem -Path $Dir -Recurse -Filter "SKILL.md" -ErrorAction SilentlyContinue)
        foreach ($Skill in $Skills) {
            $SkillName = $Skill.Directory.Name

            # Skip if already synced from a higher-priority source
            if ($SyncedSkillNames.ContainsKey($SkillName)) {
                Write-Host "  [--] Claude Skill -> $SkillName (already synced from $($SyncedSkillNames[$SkillName]))"
                continue
            }

            $RelSource = $Skill.FullName.Substring($TargetDir.Length + 1).Replace("\", "/")
            $SyncedSkillNames[$SkillName] = $RelSource

            $DestDir = Join-Path $ClaudeSkillsDir $SkillName
            if (-not (Test-Path $DestDir)) {
                New-Item -ItemType Directory -Path $DestDir -Force | Out-Null
            }

            $SourceContent = Get-Content -Path $Skill.FullName -Raw
            $DestPath = Join-Path $DestDir "SKILL.md"

            # Only write if content changed (avoid unnecessary git diffs)
            $ShouldWrite = $true
            if (Test-Path $DestPath) {
                $ExistingContent = Get-Content -Path $DestPath -Raw
                if ($ExistingContent -eq $SourceContent) {
                    $ShouldWrite = $false
                }
            }

            if ($ShouldWrite) {
                Set-Content -Path $DestPath -Value $SourceContent -Encoding UTF8
                Write-Host "  [OK] Claude Skill -> $SkillName (from $RelSource)"
            }
            else {
                Write-Host "  [--] Claude Skill -> $SkillName (unchanged)"
            }
        }
    }
}

# Clean up stale skills that no longer exist in source
$ExistingClaudeSkills = Get-ChildItem -Path $ClaudeSkillsDir -Directory -ErrorAction SilentlyContinue
foreach ($Existing in $ExistingClaudeSkills) {
    if (-not $SyncedSkillNames.ContainsKey($Existing.Name)) {
        Remove-Item -Path $Existing.FullName -Recurse -Force
        Write-Host "  [DEL] Removed stale skill -> $($Existing.Name)"
    }
}

Write-Host "  Total: $($SyncedSkillNames.Count) unique skills synced to .claude/skills/"

# --- 6. SYNC TO CURSOR ---
Write-Host ""
Write-Host "=== Syncing to Cursor ==="

$CursorDir = Join-Path $TargetDir ".cursor\rules"
if (-not (Test-Path $CursorDir)) {
    New-Item -ItemType Directory -Path $CursorDir -Force | Out-Null
}

foreach ($Dir in $AllSkillDirs) {
    if (Test-Path $Dir) {
        $Skills = @(Get-ChildItem -Path $Dir -Recurse -Filter "SKILL.md" -ErrorAction SilentlyContinue)
        foreach ($Skill in $Skills) {
            $RuleName = $Skill.Directory.Name + ".mdc"
            $DestPath = Join-Path $CursorDir $RuleName
            $Content = Get-Content -Path $Skill.FullName -Raw
            Set-Content -Path $DestPath -Value $Content -Encoding UTF8
            Write-Host "  [OK] Cursor Rule -> $RuleName"
        }
    }
}

Write-Host ""
Write-Host "=========================================="
Write-Host "    SYNC COMPLETE!"
Write-Host "=========================================="
Write-Host ""
Write-Host "Structure:"
Write-Host "  - Global:  universal-skills/"
Write-Host "  - Shared:  .agents/skills/"
Write-Host "  - Host:    app.trading.algoritmico.api/.agents/skills/"
Write-Host "  - Web:     app.trading.algoritmico.web/skills/"
Write-Host "  - Claude:  .claude/skills/ (auto-synced)"
Write-Host "  - Cursor:  .cursor/rules/ (auto-synced)"
Write-Host ""

