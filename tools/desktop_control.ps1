param([string]$Action,[string]$Path,[int]$X,[int]$Y,[string]$Title)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class Desk {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, IntPtr dwExtraInfo);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
function Get-GameWindow {
    $proc=Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and $_.ProcessName -eq 'dotnet' -and ($_.MainWindowTitle -match '泰拉瑞|tModLoader|Terraria') } | Select-Object -First 1
    if($proc){ return $proc.MainWindowHandle }
    $proc=Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -match '泰拉瑞|tModLoader|Terraria' } | Select-Object -First 1
    if($proc){ return $proc.MainWindowHandle }
    return [IntPtr]::Zero
}
switch($Action){
    'list' {
        Get-Process | Where-Object { $_.MainWindowTitle } | Select-Object Id,ProcessName,MainWindowTitle | Format-Table -AutoSize
    }
    'focus' {
        $h=Get-GameWindow
        if($h -eq [IntPtr]::Zero){ throw 'game window not found' }
        [Desk]::ShowWindow($h,9) | Out-Null
        [Desk]::SetForegroundWindow($h) | Out-Null
        Start-Sleep -Milliseconds 300
        $r=New-Object Desk+RECT
        [Desk]::GetWindowRect($h,[ref]$r) | Out-Null
        "focused hwnd=$h rect=$($r.Left),$($r.Top),$($r.Right),$($r.Bottom)"
    }
    'shoot' {
        $bounds=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bmp=New-Object System.Drawing.Bitmap $bounds.Width,$bounds.Height
        $g=[System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($bounds.Location,[System.Drawing.Point]::Empty,$bounds.Size)
        $bmp.Save($Path,[System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose();$bmp.Dispose()
        "saved $Path ($($bounds.Width)x$($bounds.Height))"
    }
    'click' {
        [Desk]::SetCursorPos($X,$Y) | Out-Null
        Start-Sleep -Milliseconds 120
        [Desk]::mouse_event(0x02,0,0,0,[IntPtr]::Zero)
        Start-Sleep -Milliseconds 60
        [Desk]::mouse_event(0x04,0,0,0,[IntPtr]::Zero)
        Start-Sleep -Milliseconds 200
        "clicked $X,$Y"
    }
    'dblclick' {
        [Desk]::SetCursorPos($X,$Y) | Out-Null
        Start-Sleep -Milliseconds 120
        [Desk]::mouse_event(0x02,0,0,0,[IntPtr]::Zero)
        [Desk]::mouse_event(0x04,0,0,0,[IntPtr]::Zero)
        Start-Sleep -Milliseconds 90
        [Desk]::mouse_event(0x02,0,0,0,[IntPtr]::Zero)
        [Desk]::mouse_event(0x04,0,0,0,[IntPtr]::Zero)
        Start-Sleep -Milliseconds 200
        "double-clicked $X,$Y"
    }
    default { throw "unknown action $Action" }
}
