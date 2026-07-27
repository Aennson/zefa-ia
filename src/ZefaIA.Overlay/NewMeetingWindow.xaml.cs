using System.Windows;
using System.Windows.Input;

namespace ZefaIA.Overlay;

public partial class NewMeetingWindow : Window
{
    private readonly IReadOnlyList<MeetingTemplate> _templates = MeetingTemplate.All;

    public NewMeetingResult? Result { get; private set; }

    public NewMeetingWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The window draws its own chrome (WindowStyle="None"), so there is no title bar
    /// for Windows to drag. The header stands in for one.
    /// </summary>
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void BtnTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (!int.TryParse(btn.Tag?.ToString(), out var idx)) return;
        if (idx < 0 || idx >= _templates.Count) return;

        ApplyTemplate(_templates[idx]);
    }

    internal void ApplyTemplate(MeetingTemplate template)
    {
        TxtAgenda.Text = template.Agenda;
        TxtObjective.Text = template.Objective;
        TxtParticipants.Text = template.Participants;
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        Result = BuildResult(isQuickStart: false);
        DialogResult = true;
        Close();
    }

    private void BtnQuickStart_Click(object sender, RoutedEventArgs e)
    {
        Result = BuildResult(isQuickStart: true);
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    internal NewMeetingResult BuildResult(bool isQuickStart)
    {
        var title = string.IsNullOrWhiteSpace(TxtTitle.Text)
            ? MeetingTemplate.GenerateDefaultTitle()
            : TxtTitle.Text.Trim();

        return new NewMeetingResult(
            Title: title,
            Agenda: isQuickStart ? "" : TxtAgenda.Text.Trim(),
            Objective: isQuickStart ? "" : TxtObjective.Text.Trim(),
            Participants: isQuickStart ? "" : TxtParticipants.Text.Trim(),
            IsQuickStart: isQuickStart
        );
    }
}

public record NewMeetingResult(
    string Title,
    string Agenda,
    string Objective,
    string Participants,
    bool IsQuickStart
);
