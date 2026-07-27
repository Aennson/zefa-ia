using System.Windows;
using System.Windows.Input;

namespace ZefaIA.Overlay;

public partial class SettingsWindow : Window
{
    public event EventHandler<AppSettings>? SettingsSaved;

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

    public AppSettings GetCurrentSettings()
    {
        return new AppSettings
        {
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
