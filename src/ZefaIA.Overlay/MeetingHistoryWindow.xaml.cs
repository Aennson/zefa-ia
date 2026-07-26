using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ZefaIA.Persistence;

namespace ZefaIA.Overlay;

public partial class MeetingHistoryWindow : Window
{
    private readonly IMeetingRepository _repository;
    private readonly ObservableCollection<SessionListItem> _items = new();
    private List<MeetingSession> _allSessions = new();

    public MeetingHistoryWindow(IMeetingRepository repository)
    {
        _repository = repository;
        InitializeComponent();
        LstSessions.ItemsSource = _items;
    }

    public async Task LoadSessionsAsync()
    {
        _allSessions = await _repository.GetAllSessionsAsync();
        UpdateList(_allSessions);
    }

    internal void UpdateList(List<MeetingSession> sessions)
    {
        _items.Clear();
        foreach (var s in sessions)
            _items.Add(SessionListItem.From(s));
    }

    private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = TxtSearch.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            UpdateList(_allSessions);
            return;
        }

        var results = await _repository.SearchSessionsAsync(query);
        UpdateList(results);
    }

    private async void LstSessions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstSessions.SelectedItem is not SessionListItem item) return;

        TxtDetailHeader.Text = item.Title;
        TxtDetailMeta.Text = $"{item.DateDisplay}  |  Duracao: {item.DurationDisplay}  |  Participantes: {item.Participants}";

        PnlDetail.Children.Clear();

        var transcriptions = await _repository.GetTranscriptionsAsync(item.SessionId);
        var suggestions = await _repository.GetSuggestionsAsync(item.SessionId);

        var suggestionsByTime = suggestions
            .OrderBy(s => s.Timestamp)
            .ToList();

        int sugIdx = 0;
        foreach (var t in transcriptions)
        {
            while (sugIdx < suggestionsByTime.Count && suggestionsByTime[sugIdx].Timestamp <= t.Timestamp)
            {
                AddSuggestionBlock(suggestionsByTime[sugIdx]);
                sugIdx++;
            }

            AddTranscriptionBlock(t);
        }

        while (sugIdx < suggestionsByTime.Count)
        {
            AddSuggestionBlock(suggestionsByTime[sugIdx]);
            sugIdx++;
        }
    }

    private void AddTranscriptionBlock(TranscriptionEntry entry)
    {
        var block = new TextBlock
        {
            Margin = new Thickness(0, 2, 0, 2),
            TextWrapping = TextWrapping.Wrap
        };

        block.Inlines.Add(new Run($"[{entry.Timestamp:HH:mm:ss}] ")
        {
            Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 170))
        });
        block.Inlines.Add(new Run($"{entry.SpeakerName}: ")
        {
            Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
            FontWeight = FontWeights.SemiBold
        });
        block.Inlines.Add(new Run(entry.Text)
        {
            Foreground = new SolidColorBrush(Color.FromRgb(224, 224, 255))
        });

        PnlDetail.Children.Add(block);
    }

    private void AddSuggestionBlock(SuggestionEntry entry)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 124, 58, 237)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(16, 4, 0, 4)
        };

        var block = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };

        block.Inlines.Add(new Run($"[{entry.Timestamp:HH:mm:ss}] Sugestao ({entry.TriggerReason}): ")
        {
            Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
            FontWeight = FontWeights.SemiBold
        });
        block.Inlines.Add(new Run(entry.Text)
        {
            Foreground = new SolidColorBrush(Color.FromRgb(224, 224, 255))
        });

        border.Child = block;
        PnlDetail.Children.Add(border);
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LstSessions.SelectedItem is not SessionListItem item) return;

        var result = MessageBox.Show(
            $"Deletar reuniao \"{item.Title}\"?\nEsta acao nao pode ser desfeita.",
            "Confirmar exclusao",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        await _repository.DeleteSessionAsync(item.SessionId);
        _allSessions.RemoveAll(s => s.Id == item.SessionId);
        _items.Remove(item);

        PnlDetail.Children.Clear();
        TxtDetailHeader.Text = "Selecione uma reuniao";
        TxtDetailMeta.Text = "";
    }
}

public class SessionListItem
{
    public long SessionId { get; init; }
    public string Title { get; init; } = "";
    public string DateDisplay { get; init; } = "";
    public string DurationDisplay { get; init; } = "";
    public string Participants { get; init; } = "";

    public static SessionListItem From(MeetingSession session) => new()
    {
        SessionId = session.Id,
        Title = string.IsNullOrWhiteSpace(session.Title) ? $"Reuniao #{session.Id}" : session.Title,
        DateDisplay = session.StartedAt.ToString("dd/MM/yyyy HH:mm"),
        DurationDisplay = session.Duration == TimeSpan.Zero
            ? "Em andamento"
            : $"{(int)session.Duration.TotalMinutes}min",
        Participants = string.IsNullOrWhiteSpace(session.Participants) ? "-" : session.Participants
    };
}
