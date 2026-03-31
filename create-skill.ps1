param (
    [Parameter(Mandatory=$true)]
    [string]$Name,

    [Parameter(Mandatory=$true)]
    [string]$Description,

    [string]$Target = "Host" # or Web
)

$RootPath = Get-Location
$SkillPath = ""

if ($Target -eq "Web") {
    $SkillPath = Join-Path $RootPath "app.trading.algoritmico.web\universal-skills\$Name"
} else {
    $SkillPath = Join-Path $RootPath "app.trading.algoritmico.api\.agent\skills\$Name"
}

if (Test-Path $SkillPath) {
    Write-Host "Error: Skill '$Name' already exists at $SkillPath" -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Force -Path $SkillPath | Out-Null

$TemplateContent = @"
---
name: $Name
description: $Description
---

# 🦸 Skill: $($Name -replace '-', ' ' | ForEach-Object { $_.Substring(0,1).ToUpper() + $_.Substring(1) })

$Description

## 📋 Usage
Trigger this skill when...

## ⚙️ Process

### 1. Step One
...

### 2. Step Two
...

## ⚠️ Standards & Rules
- Do this...
- Don't do that...
"@

$SkillFile = Join-Path $SkillPath "SKILL.md"
Set-Content -Path $SkillFile -Value $TemplateContent

Write-Host "✅ Skill '$Name' created successfully at:" -ForegroundColor Green
Write-Host "   $SkillFile"
Write-Host "Now edit the SKILL.md file to define the logic."

