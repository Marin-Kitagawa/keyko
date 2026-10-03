Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public class Dump {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  public struct R { public int L, T, Rt, B; }
}
"@
$pids = @((Get-Process Keyko -ErrorAction SilentlyContinue).Id)
Write-Output "pids: $pids"
$cb = [Dump+EnumProc]{
  param($h, $l)
  $winPid = 0
  [Dump]::GetWindowThreadProcessId($h, [ref]$winPid) | Out-Null
  if ($pids -contains $winPid -and [Dump]::IsWindowVisible($h)) {
    $sb = New-Object System.Text.StringBuilder 64
    [Dump]::GetWindowTextW($h, $sb, 64) | Out-Null
    $r = New-Object Dump+R
    [Dump]::GetWindowRect($h, [ref]$r) | Out-Null
    Write-Output ("win: '" + $sb.ToString() + "' " + ($r.Rt - $r.L) + "x" + ($r.B - $r.T))
  }
  return $true
}
[Dump]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
