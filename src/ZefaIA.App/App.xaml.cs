using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZefaIA.Overlay;
using ZefaIA.Persistence;

namespace ZefaIA.App;

public partial class App : Application
{
    private ILoggerFactory? _loggerFactory;
    private AppServices? _services;
    private MeetingOrchestrator? _orchestrator;
    private TrayIconController? _tray;
    private bool _shutdownStarted;

    internal AppServices? Services => _services;
    internal MeetingOrchestrator? Orchestrator => _orchestrator;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
            MessageBox.Show(
                $"Falha ao iniciar o Zefa IA:\n\n{ex.Message}",
                "Erro de inicializacao",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
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
            MessageBox.Show(
                $"Nao foi possivel iniciar a reuniao:\n\n{ex.Message}",
                "Erro",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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

        // Settings that only affect the next meeting (STT provider, profile) are
        // picked up when the graph is rebuilt; overlay values apply immediately.
        var updated = window.GetCurrentSettings();
        await updated.SaveAsync(AppBootstrapper.SettingsPath);
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
