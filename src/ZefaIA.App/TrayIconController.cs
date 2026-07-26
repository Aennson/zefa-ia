using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ZefaIA.App;

/// <summary>
/// The app has no main window — the tray icon is the only always-present UI.
/// Uses the WinForms NotifyIcon (WPF has no equivalent) and keeps its menu items
/// in sync with the orchestrator's state.
/// </summary>
public sealed partial class TrayIconController : IDisposable
{
    private readonly MeetingOrchestrator _orchestrator;
    private readonly ILogger<TrayIconController> _logger;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _newMeetingItem;
    private readonly ToolStripMenuItem _stopMeetingItem;
    private bool _disposed;

    public event Action? NewMeetingRequested;
    public event Action? StopMeetingRequested;
    public event Action? SettingsRequested;
    public event Action? HistoryRequested;
    public event Action? ExitRequested;

    public TrayIconController(
        MeetingOrchestrator orchestrator,
        ILogger<TrayIconController>? logger = null)
    {
        _orchestrator = orchestrator;
        _logger = logger ?? NullLogger<TrayIconController>.Instance;

        _newMeetingItem = new ToolStripMenuItem("Nova Reuniao", null, (_, _) => NewMeetingRequested?.Invoke());
        _stopMeetingItem = new ToolStripMenuItem("Parar Reuniao", null, (_, _) => StopMeetingRequested?.Invoke())
        {
            Enabled = false
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_newMeetingItem);
        menu.Items.Add(_stopMeetingItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Configuracoes", null, (_, _) => SettingsRequested?.Invoke()));
        menu.Items.Add(new ToolStripMenuItem("Historico", null, (_, _) => HistoryRequested?.Invoke()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Sair", null, (_, _) => ExitRequested?.Invoke()));

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateStateIcon(MeetingState.Idle),
            ContextMenuStrip = menu,
            Text = BuildTooltip(MeetingState.Idle),
            Visible = false
        };

        _notifyIcon.DoubleClick += (_, _) => OnDoubleClick();

        _orchestrator.OnStateChanged += OnOrchestratorStateChanged;
    }

    public void Show() => _notifyIcon.Visible = true;

    public void Hide() => _notifyIcon.Visible = false;

    /// <summary>Double-click starts a meeting when idle, stops it when running.</summary>
    private void OnDoubleClick()
    {
        if (_orchestrator.State == MeetingState.Running)
            StopMeetingRequested?.Invoke();
        else
            NewMeetingRequested?.Invoke();
    }

    private void OnOrchestratorStateChanged(MeetingState state)
    {
        // The orchestrator raises this from a background thread; NotifyIcon must
        // be touched from the UI thread that created it.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ApplyState(state));
            return;
        }

        ApplyState(state);
    }

    private void ApplyState(MeetingState state)
    {
        var previous = _notifyIcon.Icon;
        _notifyIcon.Icon = CreateStateIcon(state);
        previous?.Dispose();

        _notifyIcon.Text = BuildTooltip(state);
        _newMeetingItem.Enabled = state is MeetingState.Idle or MeetingState.Error;
        _stopMeetingItem.Enabled = state is MeetingState.Running or MeetingState.Error;

        _logger.LogDebug("Tray state: {State}", state);
    }

    internal static string BuildTooltip(MeetingState state) => state switch
    {
        MeetingState.Idle => "Zefa IA - ocioso",
        MeetingState.Starting => "Zefa IA - iniciando...",
        MeetingState.Running => "Zefa IA - gravando",
        MeetingState.Stopping => "Zefa IA - encerrando...",
        MeetingState.Error => "Zefa IA - erro",
        _ => "Zefa IA"
    };

    internal static Color GetStateColor(MeetingState state) => state switch
    {
        MeetingState.Running => Color.FromArgb(220, 38, 38),      // red: recording
        MeetingState.Starting => Color.FromArgb(234, 179, 8),     // amber: transitioning
        MeetingState.Stopping => Color.FromArgb(234, 179, 8),
        MeetingState.Error => Color.FromArgb(148, 163, 184),      // slate: degraded
        _ => Color.FromArgb(124, 58, 237)                         // violet: idle
    };

    /// <summary>
    /// Draws the icon in code rather than shipping five .ico files — the tray only
    /// needs a 16x16 state dot.
    /// </summary>
    private static Icon CreateStateIcon(MeetingState state)
    {
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(GetStateColor(state));
            g.FillEllipse(brush, 2, 2, 12, 12);
        }

        // Icon.FromHandle does not take ownership of the HICON, so the handle must
        // be destroyed by hand. Cloning first gives an Icon backed by its own
        // memory that stays valid after the handle goes away.
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _orchestrator.OnStateChanged -= OnOrchestratorStateChanged;

        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}
