param([string]$ReleaseId, [string]$NotesPath)
Add-Type -AssemblyName System.Web.Extensions
$notes = [IO.File]::ReadAllText($NotesPath, [Text.Encoding]::UTF8)
$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$json = '{"body": ' + $serializer.Serialize($notes) + '}'
$tok = $env:KEYKO_TOKEN
Invoke-RestMethod -Method Patch -Uri "https://api.github.com/repos/Marin-Kitagawa/keyko/releases/$ReleaseId" -Headers @{ Authorization = "Bearer $tok" } -ContentType "application/json" -Body ([System.Text.Encoding]::UTF8.GetBytes($json)) | Out-Null
Write-Output "patched release $ReleaseId"
