using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private bool _ownsSingleInstance;

    public AppServices Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Startup performs file and detector-pack I/O before the first window exists.
        // Keep WPF alive across those awaits, then restore normal main-window shutdown.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _singleInstance = new Mutex(initiallyOwned: true, "Local\\ExpeditionsMacro.App", out bool created);
        _ownsSingleInstance = created;
        if (!created)
        {
            MessageBox.Show("Expeditions Macro is already running.", "Expeditions Macro", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(2);
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        try
        {
            Services = await AppServices.CreateAsync(Dispatcher);
            if (e.Args.Length == 2 && string.Equals(e.Args[0], "--snapshot-ui", StringComparison.OrdinalIgnoreCase))
            {
                await UiSnapshotRenderer.RenderAsync(Services, e.Args[1]);
                Shutdown(0);
                return;
            }
            ThemeService.Apply(Services.Settings.Theme);
            MainWindow window = new(Services);
            MainWindow = window;
            window.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception error)
        {
            try
            {
                AppPaths paths = new();
                new FileLogger(paths.Logs).Error("Application startup failed.", error);
            }
            catch { }
            MessageBox.Show($"Expeditions Macro could not start.\n\n{error.Message}\n\nDetails were written to the logs folder.", "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Services is not null) Services.Dispose();
        if (_ownsSingleInstance) _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try { Services?.Log.Error("Unhandled UI error.", e.Exception); } catch { }
        MessageBox.Show(e.Exception.Message, "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
