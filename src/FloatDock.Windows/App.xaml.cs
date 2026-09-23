using System.Windows;
using FloatDock.Windows.Taskbar;

namespace FloatDock.Windows;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (TaskbarGuard.HandleArguments(e.Args)) { Shutdown(); return; }
        _mutex = new Mutex(true, @"Local\FloatDock.Desktop", out _ownsMutex);
        if (!_ownsMutex) { Shutdown(); return; }
        var settings = SettingsStore.Load(out var warning);
        MainWindow = e.Args.Contains("--standalone") ? new DockWindow(settings) : new FusionWindow(settings);
        MainWindow.Show();
        if (warning != null) MessageBox.Show(warning, "FloatDock", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
