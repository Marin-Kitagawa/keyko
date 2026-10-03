# Captures the Keyko TOAST window (small borderless, no title) via EnumWindows.
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public class TCap {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  public struct R { public int L, T, Rt, B; }
}
"@
[TCap]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$proc = Get-Process Keyko -ErrorAction Stop
$targetPid = $proc.Id
$script:cands = @()
$cb = [TCap+EnumProc]{
  param($h, $l)
  $winPid = 0
  [TCap]::GetWindowThreadProcessId($h, [ref]$winPid) | Out-Null
  if ($winPid -eq $targetPid -and [TCap]::IsWindowVisible($h)) {
    $sb = New-Object System.Text.StringBuilder 64
    [TCap]::GetWindowTextW($h, $sb, 64) | Out-Null
    $r = New-Object TCap+R
    [TCap]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.Rt - $r.L
    if ($w -gt 300 -and $w -lt 900) { $script:cands += $h }
  }
  return $true
}
[TCap]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($script:cands.Count -eq 0) { Write-Output "no toast window"; exit 1 }
$h = $script:cands[$script:cands.Count - 1]
$r = New-Object TCap+R
[TCap]::GetWindowRect($h, [ref]$r) | Out-Null
$wd = $r.Rt - $r.L; $ht = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap($wd, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [TCap]::PrintWindow($h, $hdc, 2)
$g.ReleaseHdc($hdc); $g.Dispose()
$bmp.Save("C:\Users\Ahri\Documents\Default Project\Keyko\screenshots\toast.png", [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "saved=$ok $wd x $ht"
