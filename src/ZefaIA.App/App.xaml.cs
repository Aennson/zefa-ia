using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZefaIA.Overlay;
using ZefaIA.Persistence;

namespace ZefaIA.App;

// Fully qualified: UseWindowsForms adds a global using for System.Windows.Forms,
// which makes the bare name `Application` ambiguous with System.Windows.Application.
public partial class App : System.Windows.Application
{
    private ILoggerFactory? _loggerFactory;
    private AppServices? _services;
    private MeetingOrchestrator? _orchestrator;
    private TrayIconController? _tray;
    private readonly CrashReporter _crashReporter = new();
    private bool _shutdownStarted;

    internal AppServices? Services => _services;
    internal MeetingOrchestrator? Orchestrator => _orchestrator;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        InstallGlobalExceptionHandlers();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = _loggerFactory.CreateLogger<App>();

        try
        {
            _services = await AppBootstrapper.BuildAsync(configuration, _loggerFactory);
            _orchestrator = new MeetingOrchestrator(
                _services, _loggerFactory.CreateLogger<MeetingOrchestrator>());

            _tray = new TrayIconController(_orchestrator, _loggerFactory.CreateLogger<TrayIconController>());
            _tray.NewMeetingRequested += OnNewMeetingRequested;
            _tray.StopMeetingRequested += OnStopMeetingRequested;
            _tray.SettingsRequested += OnSettingsRequested;
            _tray.HistoryRequested += OnHistoryRequested;
            _tray.ExitRequested += OnExitRequested;
            _tray.Show();

            logger.LogInformation("Zefa IA ready (LLM {State})",
                _services.IsLlmEnabled ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Startup failed");
            System.Windows.MessageBox.Show(
                $"Falha ao iniciar o Zefa IA:\n\n{ex.Message}",
                "Erro de inicializacao",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Catches everything the three .NET exception channels can surface. A crash
    /// during a meeting must not take the app down without the transcription being
    /// flushed, so UI-thread exceptions are reported and swallowed rather than
    /// allowed to terminate the process.
    /// </summary>
    private void InstallGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            HandleCrash(args.Exception, "UI");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                HandleCrash(ex, "AppDomain");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            HandleCrash(args.Exception, "Task");
            args.SetObserved();
        };
    }

    private void HandleCrash(Exception exception, string source)
    {
        var path = _crashReporter.Report(exception, source);
        _loggerFactory?.CreateLogger<App>().LogError(exception, "Unhandled exception from {Source}", source);

        // Only interrupt the user for failures that leave the app unusable; a
        // background task blowing up mid-meeting is logged and left to the
        // component's own recovery.
        if (source != "UI") return;

        System.Windows.MessageBox.Show(
            path == null
                ? $"Ocorreu um erro inesperado:\n\n{exception.Message}"
                : $"Ocorreu um erro inesperado:\n\n{exception.Message}\n\nDetalhes salvos em:\n{path}",
            "Zefa IA - Erro",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }

    private async void OnNewMeetingRequested()
    {
        if (_services == null || _orchestrator == null) return;
        if (_orchestrator.State == MeetingState.Running) return;

        var dialog = new NewMeetingWindow();
        if (dialog.ShowDialog() != true || dialog.Result == null) return;

        var result = dialog.Result;
        var session = new MeetingSession
        {
            Title = result.Title,
            Agenda = result.Agenda,
            Objective = result.Objective,
            Participants = result.Participants
        };

        try
        {
            await _orchestrator.StartMeetingAsync(session);
            _services.Overlay.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Nao foi possivel iniciar a reuniao:\n\n{ex.Message}",
                "Erro",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private async void OnStopMeetingRequested()
    {
        if (_orchestrator == null) return;

        await _orchestrator.StopMeetingAsync();
        _services?.Overlay.Hide();
    }

    private async void OnSettingsRequested()
    {
        if (_services == null) return;

        var window = new SettingsWindow();
        window.LoadSettings(_services.Settings);

        if (window.ShowDialog() != true) return;

        var updated = window.GetCurrentSettings();
        await updated.SaveAsync(AppBootstrapper.SettingsPath);

        ApplySavedSettings(_services, updated);
    }

    /// <summary>
    /// Pushes saved settings onto the running app, so an API key pasted into Settings
    /// works on the next meeting instead of requiring a restart. Settings that only shape
    /// a meeting (STT provider, profile) are read when the graph is rebuilt.
    /// </summary>
    internal static void ApplySavedSettings(AppServices services, AppSettings updated)
    {
        services.Settings = updated;

        // STTServiceManager holds this instance, so mutating it is what makes a new key
        // reach the next provider it builds.
        AppBootstrapper.ApplyUserSettings(services.SttSettings, updated);

        // The Claude client caches the key in a request header at construction, so a
        // changed key means a new client. Also covers going from no key to a key, which
        // is what turns suggestions on without a restart.
        var previous = services.LlmClient;
        services.LlmClient = AppBootstrapper.TryCreateLlmClient(
            updated, services.LoggerFactory, services.LoggerFactory.CreateLogger("Settings"));

        if (previous != null)
            _ = previous.DisposeAsync().AsTask();
    }

    private async void OnHistoryRequested()
    {
        if (_services == null) return;

        var window = new MeetingHistoryWindow(_services.Repository);
        await window.LoadSessionsAsync();
        window.ShowDialog();
    }

    private async void OnExitRequested()
    {
        await ShutdownGracefullyAsync();
        Shutdown();
    }

    /// <summary>
    /// Stops the meeting before tearing down shared services, so the final
    /// transcription batch is flushed while the repository is still alive.
    /// Guarded because both the tray's Exit and WPF's OnExit reach it.
    /// </summary>
    private async Task ShutdownGracefullyAsync()
    {
        if (_shutdownStarted) return;
        _shutdownStarted = true;

        var logger = _loggerFactory?.CreateLogger<App>();

        try
        {
            if (_orchestrator != null)
                await _orchestrator.DisposeAsync();

            _tray?.Dispose();

            if (_services != null)
                await _services.DisposeAsync();

            logger?.LogInformation("Shutdown complete");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during shutdown");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ShutdownGracefullyAsync().GetAwaiter().GetResult();
        _loggerFactory?.Dispose();
        base.OnExit(e);
    }
}
