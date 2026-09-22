using System.Windows;

namespace FloatDock.Windows;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, @"Local\FloatDock.Desktop", out _ownsMutex);
        if (!_ownsMutex) { Shutdown(); return; }
        var settings = SettingsStore.Load(out var warning);
        MainWindow = new DockWindow(settings);
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
