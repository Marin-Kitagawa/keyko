param([string]$Path, [string]$Anchor, [string]$Insert)
$s = [IO.File]::ReadAllText($Path)
$line = "- **Impeccable pass (v1.3.0)**: the impeccable.style skill (pbakaus/impeccable v4.5.0) is imported at `.agents/skills/impeccable/` (user asked to import the skill and redesign per it). Keyko = Operate mode. Icon-tile squares removed everywhere (impeccable anti-pattern); editor card-in-card sections de-nested. NEVER re-add icon-tile squares or bold weights."
if (-not $s.Contains($line)) {
    $s = $s.Replace($Anchor, $line + "`r`n" + $Anchor)
    [IO.File]::WriteAllText($Path, $s)
    Write-Output "inserted"
} else { Write-Output "already present" }
