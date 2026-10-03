Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public class SC {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
}
"@
[SC]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$w = 620; $h = 280
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Width - $w, $bounds.Height - $h, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$g.Dispose()
$bmp.Save("C:\Users\Ahri\Documents\Default Project\Keyko\screenshots\toast.png", [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "corner captured"
