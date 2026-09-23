param([Parameter(Mandatory)][string]$Executable, [switch]$AllowDesktopChanges)
$ErrorActionPreference = 'Stop'
if (-not $AllowDesktopChanges) { throw 'This opt-in integration test changes the active taskbar. Use a dedicated test desktop and pass -AllowDesktopChanges.' }
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class DockPlacementProbe {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [StructLayout(LayoutKind.Sequential)] public struct Monitor { public int Size; public Rect Bounds,Work; public uint Flags; }
 [StructLayout(LayoutKind.Sequential)] public struct Bar { public uint Size; public IntPtr Window; public uint Callback,Edge; public Rect Bounds; public IntPtr Parameter; }
 public delegate bool Callback(IntPtr h,IntPtr p);
 [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint m,ref Bar b);
 [DllImport("user32.dll")] static extern bool EnumWindows(Callback c,IntPtr p);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder t,int n);
 [DllImport("user32.dll",EntryPoint="GetWindowRect")] static extern bool ReadWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
 [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr h,ref Monitor m);
 [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,IntPtr l,uint f,uint timeout,out IntPtr result);
 public static uint State() { var b=new Bar {Size=(uint)Marshal.SizeOf<Bar>()}; return (uint)SHAppBarMessage(4,ref b).ToUInt64(); }
 public static void Restore(uint state) { var b=new Bar {Size=(uint)Marshal.SizeOf<Bar>(),Parameter=(IntPtr)state}; SHAppBarMessage(10,ref b); }
 public static Monitor Screen() { var old=SetThreadDpiAwarenessContext((IntPtr)(-4)); try { var m=new Monitor {Size=Marshal.SizeOf<Monitor>()}; GetMonitorInfo(MonitorFromWindow(IntPtr.Zero,1),ref m); return m; } finally {SetThreadDpiAwarenessContext(old);} }
 public static bool GetWindowRect(IntPtr h,out Rect r) { var old=SetThreadDpiAwarenessContext((IntPtr)(-4)); try { return ReadWindowRect(h,out r); } finally {SetThreadDpiAwarenessContext(old);} }
 public static IntPtr Find(int pid) { IntPtr result=IntPtr.Zero; EnumWindows((h,p)=>{ uint owner; GetWindowThreadProcessId(h,out owner); var text=new StringBuilder(128); GetWindowText(h,text,128); if(owner==pid && IsWindowVisible(h) && text.ToString()=="FloatDock") {result=h; return false;} return true;},IntPtr.Zero); return result; }
 public static void Close(IntPtr h) { IntPtr result; SendMessageTimeout(h,0x10,IntPtr.Zero,IntPtr.Zero,2,5000,out result); }
}
'@
$binaryPath = (Resolve-Path -LiteralPath $Executable).Path
$baselineState = [DockPlacementProbe]::State()
$baselineScreen = [DockPlacementProbe]::Screen()
$testProcess = $null
try {
    foreach ($mode in 'normal-close','forced-exit','immediate-restart','recovery-command') {
        $testProcess = Start-Process -FilePath $binaryPath -ArgumentList '--standalone' -WindowStyle Hidden -PassThru
        $null = $testProcess.WaitForInputIdle(15000)
        Start-Sleep -Milliseconds 1800
        if ($mode -eq 'immediate-restart') {
            Stop-Process -Id $testProcess.Id
            $null = $testProcess.WaitForExit(5000)
            $testProcess = Start-Process -FilePath $binaryPath -ArgumentList '--standalone' -WindowStyle Hidden -PassThru
            $null = $testProcess.WaitForInputIdle(15000)
            Start-Sleep -Milliseconds 1800
        }
        if ($testProcess.HasExited) { throw 'Dock exited before placement verification.' }
        $dockWindow = [DockPlacementProbe]::Find($testProcess.Id)
        if ($dockWindow -eq 0) { throw 'No visible Dock window.' }
        $dockBounds = New-Object DockPlacementProbe+Rect
        $null = [DockPlacementProbe]::GetWindowRect($dockWindow,[ref]$dockBounds)
        $screen = [DockPlacementProbe]::Screen()
        if (([DockPlacementProbe]::State() -band 1) -ne 1) { throw 'Windows taskbar did not enter auto-hide.' }
        if ([math]::Abs($dockBounds.Bottom - $screen.Bounds.Bottom) -gt 4) { throw "Dock remains above screen bottom: dock=$($dockBounds.Bottom), screen=$($screen.Bounds.Bottom)" }
        if ($screen.Work.Bottom -gt $screen.Bounds.Bottom - 50) { throw 'No usable bottom reservation for maximized windows.' }
        if ($mode -eq 'recovery-command') {
            $recoveryProcess = Start-Process -FilePath $binaryPath -ArgumentList '--restore-taskbar' -WindowStyle Hidden -PassThru
            if (-not $recoveryProcess.WaitForExit(10000)) { throw 'Recovery command did not finish.' }
        } elseif ($mode -ne 'forced-exit') { [DockPlacementProbe]::Close($dockWindow) } else { Stop-Process -Id $testProcess.Id }
        if (-not $testProcess.WaitForExit(10000)) { throw 'Dock did not close.' }
        for ($attempt=0; $attempt -lt 50; $attempt++) {
            $restoredScreen = [DockPlacementProbe]::Screen()
            if ([DockPlacementProbe]::State() -eq $baselineState -and $restoredScreen.Work.Bottom -eq $baselineScreen.Work.Bottom) { break }
            Start-Sleep -Milliseconds 100
        }
        if ([DockPlacementProbe]::State() -ne $baselineState) { throw 'Original taskbar setting was not restored.' }
        if ($restoredScreen.Work.Bottom -ne $baselineScreen.Work.Bottom) { throw 'Original work area was not restored.' }
        [pscustomobject]@{ Result='PASS'; Mode=$mode; DockBottom=$dockBounds.Bottom; ScreenBottom=$screen.Bounds.Bottom; ReservedPixels=$screen.Bounds.Bottom-$screen.Work.Bottom; TaskbarRestored=$true; WorkAreaRestored=$true }
    }
}
finally {
    if ($testProcess -and -not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id }
    # Test cleanup is independent of the feature under test.
    [DockPlacementProbe]::Restore($baselineState)
}
