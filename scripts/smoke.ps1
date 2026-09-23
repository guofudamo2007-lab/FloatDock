param([string]$Executable = "$PSScriptRoot\..\artifacts\win-x64\FloatDock.exe", [switch]$AllowDesktopChanges)
$ErrorActionPreference = 'Stop'
if (-not $AllowDesktopChanges) { throw 'This test opens a window. Use a dedicated test desktop and pass -AllowDesktopChanges.' }
if (-not ('FloatDockSmokeProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class FloatDockSmokeProbe {
    public delegate bool Callback(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr handle, StringBuilder text, int capacity);
    public static IntPtr Find(int processId) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((handle, parameter) => {
            uint owner; GetWindowThreadProcessId(handle, out owner);
            if (owner != processId || !IsWindowVisible(handle)) return true;
            var title = new StringBuilder(128); GetWindowText(handle, title, title.Capacity);
            if (title.ToString().StartsWith("FloatDock")) { found = handle; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@
}
$resolvedExecutable = (Resolve-Path -LiteralPath $Executable).Path
$smokeProcess = Start-Process -FilePath $resolvedExecutable -PassThru -WindowStyle Hidden
try {
    $ready = $smokeProcess.WaitForInputIdle(10000)
    Start-Sleep -Seconds 3
    $smokeProcess.Refresh()
    if ($smokeProcess.HasExited) { throw "FloatDock exited early: $($smokeProcess.ExitCode)" }
    # Process.MainWindowHandle ignores owned windows, including WPF ShowInTaskbar=false windows.
    $dockHandle = [FloatDockSmokeProbe]::Find($smokeProcess.Id)
    if (-not $ready -or $dockHandle -eq 0) { throw 'FloatDock did not create its visible dock window.' }
    [pscustomobject]@{ Result = 'PASS'; Check = 'Process startup and dock-window creation only'; ProcessId = $smokeProcess.Id; DockWindowHandle = $dockHandle; WorkingSetMB = [math]::Round($smokeProcess.WorkingSet64 / 1MB, 1) }
}
finally {
    if (-not $smokeProcess.HasExited) { Stop-Process -Id $smokeProcess.Id }
}
