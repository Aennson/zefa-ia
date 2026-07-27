using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ZefaIA.Overlay;

public partial class SettingsWindow : Window
{
    public event EventHandler<AppSettings>? SettingsSaved;

    /// <summary>
    /// Swapped in tests so the key check does not depend on the network. Created lazily
    /// so opening Settings costs nothing until the user presses "Testar chaves".
    /// </summary>
    internal Func<ApiKeyValidator> ValidatorFactory { get; set; } = static () => new ApiKeyValidator();

    public SettingsWindow()
    {
        InitializeComponent();
    }

    /// <summary>The window draws its own chrome, so the header stands in for a title bar.</summary>
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    public void LoadSettings(AppSettings settings)
    {
        CmbProvider.SelectedIndex = settings.SttProvider == "ElevenLabs" ? 1 : 0;
        CmbModelSize.SelectedIndex = settings.WhisperModelSize switch
        {
            "tiny" => 0,
            "base" => 1,
            "small" => 2,
            "medium" => 3,
            _ => 1
        };
        CmbLanguage.SelectedIndex = settings.Language switch
        {
            "auto" => 0,
            "pt" => 1,
            "en" => 2,
            "es" => 3,
            "fr" => 4,
            _ => 0
        };
        ChkGpu.IsChecked = settings.UseGPU;

        // Only keys stored here are shown. A key coming from an environment variable is
        // reported in the label instead: loading it into the box would silently copy it
        // into settings.json the next time the user pressed Save.
        PwdAnthropic.Password = settings.AnthropicApiKey;
        PwdElevenLabs.Password = settings.ElevenLabsApiKey;
        TxtAnthropic.Text = PwdAnthropic.Password;
        TxtElevenLabs.Text = PwdElevenLabs.Password;

        DescribeKeySource(LblAnthropicSource, settings.AnthropicApiKeySource, "ANTHROPIC_API_KEY");
        DescribeKeySource(LblElevenLabsSource, settings.ElevenLabsApiKeySource, "ELEVENLABS_API_KEY");

        TxtName.Text = settings.UserName;
        TxtRole.Text = settings.UserRole;
        TxtExpertise.Text = settings.UserExpertise;
        CmbTone.SelectedIndex = settings.PreferredTone switch
        {
            "Formal" => 0,
            "Casual" => 1,
            "Tecnico" => 2,
            _ => 0
        };
        TxtContext.Text = settings.AdditionalContext;

        SldOpacity.Value = settings.OverlayOpacity;
        SldFontSize.Value = settings.OverlayFontSize;
        CmbPosition.SelectedIndex = settings.OverlayPosition switch
        {
            OverlayPosition.BottomRight => 0,
            OverlayPosition.BottomLeft => 1,
            OverlayPosition.TopRight => 2,
            OverlayPosition.TopLeft => 3,
            OverlayPosition.Center => 4,
            _ => 0
        };
        TxtAutoHide.Text = settings.AutoHideSeconds.ToString();
        ChkExcludeCapture.IsChecked = settings.ExcludeFromCapture;
    }

    private static void DescribeKeySource(
        System.Windows.Controls.TextBlock label, ApiKeySource source, string environmentVariable)
    {
        label.Text = source switch
        {
            ApiKeySource.Settings => "salva aqui",
            ApiKeySource.Environment => $"vinda de {environmentVariable}",
            _ => "nao configurada"
        };
    }

    /// <summary>The key lives in the PasswordBox; the TextBox is only the reveal view of it.</summary>
    private string AnthropicKeyInput =>
        (ChkRevealKeys.IsChecked == true ? TxtAnthropic.Text : PwdAnthropic.Password).Trim();

    private string ElevenLabsKeyInput =>
        (ChkRevealKeys.IsChecked == true ? TxtElevenLabs.Text : PwdElevenLabs.Password).Trim();

    /// <summary>
    /// A PasswordBox cannot show its content, so revealing means swapping in a TextBox.
    /// Both directions copy the value across, otherwise edits made in one view would be
    /// lost when the user toggled back.
    /// </summary>
    private void RevealKeys_Changed(object sender, RoutedEventArgs e)
    {
        var revealed = ChkRevealKeys.IsChecked == true;

        if (revealed)
        {
            TxtAnthropic.Text = PwdAnthropic.Password;
            TxtElevenLabs.Text = PwdElevenLabs.Password;
        }
        else
        {
            PwdAnthropic.Password = TxtAnthropic.Text;
            PwdElevenLabs.Password = TxtElevenLabs.Text;
        }

        TxtAnthropic.Visibility = revealed ? Visibility.Visible : Visibility.Collapsed;
        TxtElevenLabs.Visibility = revealed ? Visibility.Visible : Visibility.Collapsed;
        PwdAnthropic.Visibility = revealed ? Visibility.Collapsed : Visibility.Visible;
        PwdElevenLabs.Visibility = revealed ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void BtnTestKeys_Click(object sender, RoutedEventArgs e)
    {
        BtnTestKeys.IsEnabled = false;
        ShowKeyTestResult("Testando...", "TextSecondaryBrush");

        try
        {
            using var validator = ValidatorFactory();

            // An empty box is not an error here: it means "use the environment variable",
            // so test whatever the app would actually end up using.
            var anthropic = await validator.CheckAnthropicAsync(
                Fallback(AnthropicKeyInput, "ANTHROPIC_API_KEY"));
            var elevenLabs = await validator.CheckElevenLabsAsync(
                Fallback(ElevenLabsKeyInput, "ELEVENLABS_API_KEY"));

            var anyRejected = anthropic.Status == ApiKeyStatus.Rejected
                || elevenLabs.Status == ApiKeyStatus.Rejected;

            ShowKeyTestResult(
                $"Anthropic: {anthropic.Message}\nElevenLabs: {elevenLabs.Message}",
                anyRejected ? "DangerBrush" : "SuccessBrush");
        }
        finally
        {
            BtnTestKeys.IsEnabled = true;
        }
    }

    private static string? Fallback(string typed, string environmentVariable) =>
        string.IsNullOrWhiteSpace(typed) ? Environment.GetEnvironmentVariable(environmentVariable) : typed;

    /// <summary>
    /// Takes the colour from the theme rather than a static brush. A brush built in a
    /// static field belongs to whichever thread ran the initializer, and WPF refuses to
    /// use it from any other — which is exactly what happens across STA test cases.
    /// </summary>
    private void ShowKeyTestResult(string message, string brushKey)
    {
        LblKeyTestResult.Text = message;
        LblKeyTestResult.Visibility = Visibility.Visible;

        if (TryFindResource(brushKey) is Brush brush)
            LblKeyTestResult.Foreground = brush;
    }

    public AppSettings GetCurrentSettings()
    {
        return new AppSettings
        {
            AnthropicApiKey = AnthropicKeyInput,
            ElevenLabsApiKey = ElevenLabsKeyInput,
            SttProvider = CmbProvider.SelectedIndex == 1 ? "ElevenLabs" : "WhisperLocal",
            WhisperModelSize = CmbModelSize.SelectedIndex switch
            {
                0 => "tiny",
                1 => "base",
                2 => "small",
                3 => "medium",
                _ => "base"
            },
            Language = CmbLanguage.SelectedIndex switch
            {
                0 => "auto",
                1 => "pt",
                2 => "en",
                3 => "es",
                4 => "fr",
                _ => "auto"
            },
            UseGPU = ChkGpu.IsChecked == true,
            UserName = TxtName.Text,
            UserRole = TxtRole.Text,
            UserExpertise = TxtExpertise.Text,
            PreferredTone = CmbTone.SelectedIndex switch
            {
                0 => "Formal",
                1 => "Casual",
                2 => "Tecnico",
                _ => "Formal"
            },
            AdditionalContext = TxtContext.Text,
            OverlayOpacity = SldOpacity.Value,
            OverlayFontSize = SldFontSize.Value,
            OverlayPosition = CmbPosition.SelectedIndex switch
            {
                0 => OverlayPosition.BottomRight,
                1 => OverlayPosition.BottomLeft,
                2 => OverlayPosition.TopRight,
                3 => OverlayPosition.TopLeft,
                4 => OverlayPosition.Center,
                _ => OverlayPosition.BottomRight
            },
            AutoHideSeconds = int.TryParse(TxtAutoHide.Text, out var s) ? s : 30,
            ExcludeFromCapture = ChkExcludeCapture.IsChecked == true
        };
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var settings = GetCurrentSettings();
        SettingsSaved?.Invoke(this, settings);
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
