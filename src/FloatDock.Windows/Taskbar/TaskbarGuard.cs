using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace FloatDock.Windows.Taskbar;

// A separate process survives a crashed/terminated UI and restores the taskbar preference.
internal sealed class TaskbarGuard : IDisposable
{
    private static readonly string RecoveryFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloatDock", "taskbar-recovery.json");
    private sealed record Recovery(string Id, int Process, long Started, bool AutoHide, long Window);
    private readonly Recovery _record;
    private readonly EventWaitHandle _release, _restored;
    private readonly Process _watcher;
    private bool _disposed;

    internal TaskbarGuard(nint window)
    {
        using var parent = Process.GetCurrentProcess();
        var id = "Local\\FloatDock.Guard." + Guid.NewGuid().ToString("N");
        using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, id + ".ready");
        _release = new(false, EventResetMode.ManualReset, id + ".release");
        _restored = new(false, EventResetMode.ManualReset, id + ".restored");
        using (var ownership = new RecoveryLock())
        {
            if (Read() is { } old && (IsAlive(old) || !RestoreLocked(old)))
                throw new InvalidOperationException("Previous taskbar state is still awaiting recovery.");
            if (TaskbarNative.FindWindow("Shell_TrayWnd", null) == 0) throw new InvalidOperationException("Windows taskbar is unavailable.");
            _record = new(id, parent.Id, parent.StartTime.ToUniversalTime().Ticks, (TaskbarNative.State() & 1) != 0, (long)window);
            Directory.CreateDirectory(Path.GetDirectoryName(RecoveryFile)!);
            File.WriteAllText(RecoveryFile + ".tmp", JsonSerializer.Serialize(_record));
            File.Move(RecoveryFile + ".tmp", RecoveryFile, true);
        }
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        start.ArgumentList.Add("--taskbar-guard"); start.ArgumentList.Add(id);
        try
        {
            _watcher = Process.Start(start) ?? throw new InvalidOperationException("Cannot start taskbar recovery process.");
            if (!ready.WaitOne(TimeSpan.FromSeconds(5))) throw new InvalidOperationException("Taskbar recovery process did not become ready.");
            using var ownership = new RecoveryLock();
            if (Read()?.Id != id) throw new InvalidOperationException("Taskbar recovery ownership was lost.");
            TaskbarNative.AutoHide(true);
            if ((TaskbarNative.State() & 1) == 0) throw new InvalidOperationException("Windows did not accept taskbar auto-hide.");
        }
        catch { Restore(_record); _release.Set(); _release.Dispose(); _restored.Dispose(); throw; }
    }

    internal static bool HandleArguments(string[] args)
    {
        if (args is ["--taskbar-guard", var id])
        {
            Recovery? record = null;
            try
            {
                record = Read();
                if (record?.Id != id) return true;
                using var ready = EventWaitHandle.OpenExisting(id + ".ready");
                using var release = EventWaitHandle.OpenExisting(id + ".release");
                using var restored = EventWaitHandle.OpenExisting(id + ".restored");
                using var parent = Process.GetProcessById(record.Process);
                if (parent.StartTime.ToUniversalTime().Ticks != record.Started) return true;
                ready.Set();
                while (!release.WaitOne(150) && !parent.HasExited) { }
                // Explorer can briefly be absent during a shell restart. Keep the original record until verified.
                if (RestoreWithRetry(record)) restored.Set();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
            { if (record != null) RestoreWithRetry(record); }
            return true;
        }
        if (args is ["--restore-taskbar"])
        {
            // Do not let a running Dock reapply its reservation after an emergency restore.
            var record = Read();
            if (record != null && IsAlive(record))
            {
                try { using var release = EventWaitHandle.OpenExisting(record.Id + ".release"); release.Set(); } catch (WaitHandleCannotBeOpenedException) { }
                NativeMethods.SendMessageTimeout((nint)record.Window, 0x10, 0, 0, 2, 2000, out _);
                Restore(record);
            }
            else if (record != null) Restore(record);
            // Without a recovery record, there is no original preference to restore.
            return true;
        }
        return false;
    }
    internal static void RecoverStale()
    { var record = Read(); if (record != null && !IsAlive(record)) Restore(record); }
    private static Recovery? Read()
    { try { return File.Exists(RecoveryFile) ? JsonSerializer.Deserialize<Recovery>(File.ReadAllText(RecoveryFile)) : null; } catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; } }
    private static bool IsAlive(Recovery record)
    { try { using var process = Process.GetProcessById(record.Process); return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == record.Started; } catch (ArgumentException) { return false; } }
    private static bool Restore(Recovery record)
    {
        try { using var ownership = new RecoveryLock(); return RestoreLocked(record); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException) { return false; }
    }
    private static bool RestoreWithRetry(Recovery record)
    {
        var elapsed = Stopwatch.StartNew();
        do { if (Restore(record)) return true; Thread.Sleep(250); } while (elapsed.Elapsed < TimeSpan.FromSeconds(30));
        return false;
    }
    private static bool RestoreLocked(Recovery record)
    {
        // Ownership check, shell write and record removal are one cross-process transaction.
        if (Read()?.Id != record.Id) return true;
        var shell = TaskbarNative.FindWindow("Shell_TrayWnd", null);
        if (shell == 0 || !NativeMethods.IsWindow(shell)) return false;
        TaskbarNative.Remove((nint)record.Window);
        TaskbarNative.AutoHide(record.AutoHide);
        if (TaskbarNative.FindWindow("Shell_TrayWnd", null) != shell || !NativeMethods.IsWindow(shell) || ((TaskbarNative.State() & 1) != 0) != record.AutoHide) return false;
        try { File.Delete(RecoveryFile); return true; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
    private sealed class RecoveryLock : IDisposable
    {
        private readonly Mutex _mutex = new(false, @"Local\FloatDock.TaskbarRecovery");
        internal RecoveryLock()
        {
            try { if (!_mutex.WaitOne(TimeSpan.FromSeconds(5))) { _mutex.Dispose(); throw new InvalidOperationException("Taskbar recovery is busy."); } }
            catch (AbandonedMutexException) { /* Ownership transfers to this thread after a crashed writer. */ }
        }
        public void Dispose() { _mutex.ReleaseMutex(); _mutex.Dispose(); }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _release.Set();
        if (!_restored.WaitOne(TimeSpan.FromSeconds(3))) Restore(_record);
        _release.Dispose(); _restored.Dispose(); _watcher.Dispose();
    }
}
