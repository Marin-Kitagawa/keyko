param([string]$Path, [int]$LineNumber, [string]$NewLine)
$lines = [System.Collections.Generic.List[string]](Get-Content $Path -Encoding UTF8)
$lines[$LineNumber - 1] = $NewLine
[System.IO.File]::WriteAllLines($Path, $lines, (New-Object System.Text.UTF8Encoding($false)))
Write-Output "line $LineNumber replaced"
