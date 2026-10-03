# Captures the KeyForge process windows by title via EnumWindows (DPI-aware, no focus).
param(
    [Parameter(Mandatory=$true)][string]$Title,
    [Parameter(Mandatory=$true)][string]$OutPath
)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public class WCap2 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  public struct R { public int L, T, Rt, B; }
}
"@
[WCap2]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$proc = Get-Process Keyko -ErrorAction Stop
$targetPid = $proc.Id
$script:found = [IntPtr]::Zero
$cb = [WCap2+EnumProc]{
  param($h, $l)
  $winPid = 0
  [WCap2]::GetWindowThreadProcessId($h, [ref]$winPid) | Out-Null
  if ($winPid -eq $targetPid -and [WCap2]::IsWindowVisible($h)) {
    $sb = New-Object System.Text.StringBuilder 256
    [WCap2]::GetWindowTextW($h, $sb, 256) | Out-Null
    if ($sb.ToString() -eq $Title) { $script:found = $h; return $false }
  }
  return $true
}
[WCap2]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($script:found -eq [IntPtr]::Zero) { Write-Output "not-found: $Title"; exit 1 }
$h = $script:found
$r = New-Object WCap2+R
[WCap2]::GetWindowRect($h, [ref]$r) | Out-Null
$wd = $r.Rt - $r.L; $ht = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap($wd, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [WCap2]::PrintWindow($h, $hdc, 2)
$g.ReleaseHdc($hdc); $g.Dispose()
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "saved=$ok $wd x $ht -> $OutPath"
