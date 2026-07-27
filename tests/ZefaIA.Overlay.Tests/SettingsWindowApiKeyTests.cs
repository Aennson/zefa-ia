using System.Windows;
using System.Windows.Controls;
using Xunit;
using ZefaIA.Overlay;

namespace ZefaIA.Overlay.Tests;

/// <summary>
/// The API key section of the Settings window: what it shows, what it saves, and the two
/// mistakes that would matter most — leaking an environment key into settings.json, and
/// losing an edit made while the key was revealed.
/// </summary>
public class SettingsWindowApiKeyTests : IDisposable
{
    private const string AnthropicVar = "ANTHROPIC_API_KEY";
    private const string ElevenLabsVar = "ELEVENLABS_API_KEY";

    private readonly string? _originalAnthropic = Environment.GetEnvironmentVariable(AnthropicVar);
    private readonly string? _originalElevenLabs = Environment.GetEnvironmentVariable(ElevenLabsVar);

    public SettingsWindowApiKeyTests()
    {
        Environment.SetEnvironmentVariable(AnthropicVar, null);
        Environment.SetEnvironmentVariable(ElevenLabsVar, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(AnthropicVar, _originalAnthropic);
        Environment.SetEnvironmentVariable(ElevenLabsVar, _originalElevenLabs);
    }

    [WpfFact]
    public void AStoredKeyIsShownAndSavedBackUnchanged()
    {
        WithWindow(w =>
        {
            w.LoadSettings(new AppSettings { AnthropicApiKey = "sk-ant-stored", ElevenLabsApiKey = "sk_eleven_stored" });

            Assert.Equal("sk-ant-stored", Password(w, "PwdAnthropic"));
            Assert.Equal("sk_eleven_stored", Password(w, "PwdElevenLabs"));

            var saved = w.GetCurrentSettings();
            Assert.Equal("sk-ant-stored", saved.AnthropicApiKey);
            Assert.Equal("sk_eleven_stored", saved.ElevenLabsApiKey);
        });
    }

    [WpfFact]
    public void AKeyTypedIntoTheBoxIsWhatGetsSaved()
    {
        WithWindow(w =>
        {
            w.LoadSettings(new AppSettings());
            Box(w, "PwdAnthropic").Password = "sk-ant-typed";

            Assert.Equal("sk-ant-typed", w.GetCurrentSettings().AnthropicApiKey);
        });
    }

    [WpfFact]
    public void AnEnvironmentKeyIsReportedButNeverCopiedIntoTheSavedSettings()
    {
        // Loading it into the box would mean the next Save silently writes a key the user
        // never typed into settings.json, and the environment variable would stop being
        // the source of truth without anyone deciding that.
        Environment.SetEnvironmentVariable(AnthropicVar, "sk-ant-from-environment");

        WithWindow(w =>
        {
            w.LoadSettings(new AppSettings());

            Assert.Equal("", Password(w, "PwdAnthropic"));
            Assert.Contains(AnthropicVar, Label(w, "LblAnthropicSource").Text, StringComparison.Ordinal);

            var saved = w.GetCurrentSettings();
            Assert.Equal("", saved.AnthropicApiKey);
            Assert.Equal("", saved.AnthropicApiKeyProtected);
            // The app still finds the key — it just comes from the environment, as before.
            Assert.Equal("sk-ant-from-environment", saved.ResolveAnthropicApiKey());
        });
    }

    [WpfFact]
    public void TheLabelSaysWhenNoKeyIsConfiguredAtAll()
    {
        WithWindow(w =>
        {
            w.LoadSettings(new AppSettings());

            Assert.Equal("nao configurada", Label(w, "LblAnthropicSource").Text);
            Assert.Equal("nao configurada", Label(w, "LblElevenLabsSource").Text);
        });
    }

    [WpfFact]
    public void TheLabelSaysWhenTheKeyIsStoredHere()
    {
        WithWindow(w =>
        {
            w.LoadSettings(new AppSettings { ElevenLabsApiKey = "sk_eleven_stored" });

            Assert.Equal("salva aqui", Label(w, "LblElevenLabsSource").Text);
        });
    }

    // --- reveal toggle ------------------------------------------------------------

    [WpfFact]
    public void RevealingShowsTheKeyThatWasMasked()
    {
        WithWindow(w =>
        {
            w.LoadSettings(new AppSettings { AnthropicApiKey = "sk-ant-stored" });

            Reveal(w, true);

            Assert.Equal(Visibility.Visible, Text(w, "TxtAnthropic").Visibility);
            Assert.Equal(Visibility.Collapsed, Box(w, "PwdAnthropic").Visibility);
            Assert.Equal("sk-ant-stored", Text(w, "TxtAnthropic").Text);
        });
    }

    [WpfFact]
    public void AnEditMadeWhileRevealedIsTheOneThatGetsSaved()
    {
        WithWindow(w =>
        {
            w.LoadSettings(new AppSettings { AnthropicApiKey = "sk-ant-old" });
            Reveal(w, true);

            Text(w, "TxtAnthropic").Text = "sk-ant-new";

            Assert.Equal("sk-ant-new", w.GetCurrentSettings().AnthropicApiKey);
        });
    }

    [WpfFact]
    public void AnEditSurvivesToggingTheRevealBackOff()
    {
        // Two one-way copies would drop whichever edit happened in the hidden view.
        WithWindow(w =>
        {
            w.LoadSettings(new AppSettings());

            Reveal(w, true);
            Text(w, "TxtAnthropic").Text = "sk-ant-typed-while-visible";
            Reveal(w, false);

            Assert.Equal("sk-ant-typed-while-visible", Password(w, "PwdAnthropic"));
            Assert.Equal("sk-ant-typed-while-visible", w.GetCurrentSettings().AnthropicApiKey);
        });
    }

    [WpfFact]
    public void PastedWhitespaceIsTrimmedOffTheKey()
    {
        // Copying a key out of a web page or a chat drags spaces and newlines with it, and
        // the API rejects those with a 401 that looks like a wrong key.
        WithWindow(w =>
        {
            w.LoadSettings(new AppSettings());
            Box(w, "PwdAnthropic").Password = "  sk-ant-padded\r\n";

            Assert.Equal("sk-ant-padded", w.GetCurrentSettings().AnthropicApiKey);
        });
    }

    [WpfFact]
    public void ClearingTheBoxRemovesTheStoredKey()
    {
        WithWindow(w =>
        {
            w.LoadSettings(new AppSettings { AnthropicApiKey = "sk-ant-stored" });
            Box(w, "PwdAnthropic").Password = "";

            var saved = w.GetCurrentSettings();
            Assert.Equal("", saved.AnthropicApiKeyProtected);
            Assert.Equal(ApiKeySource.None, saved.AnthropicApiKeySource);
        });
    }

    [WpfFact]
    public void SavingKeysDoesNotDisturbTheOtherSettings()
    {
        WithWindow(w =>
        {
            w.LoadSettings(new AppSettings
            {
                AnthropicApiKey = "sk-ant-stored",
                UserName = "Aennson",
                SttProvider = "ElevenLabs",
                OverlayFontSize = 18
            });

            var saved = w.GetCurrentSettings();

            Assert.Equal("Aennson", saved.UserName);
            Assert.Equal("ElevenLabs", saved.SttProvider);
            Assert.Equal(18, saved.OverlayFontSize);
        });
    }

    // --- the test button ----------------------------------------------------------

    [WpfFact]
    public void TestingKeysReportsBothServicesWithoutTouchingTheNetworkTwice()
    {
        WithWindow(w =>
        {
            var handler = new CountingHandler();
            w.ValidatorFactory = () => new ApiKeyValidator(new HttpClient(handler));

            w.LoadSettings(new AppSettings { AnthropicApiKey = "sk-ant-x", ElevenLabsApiKey = "sk_eleven_x" });

            RaiseClick(w, "BtnTestKeys");
            DrainDispatcher(w);

            var result = Label(w, "LblKeyTestResult");
            Assert.Equal(Visibility.Visible, result.Visibility);
            Assert.Contains("Anthropic:", result.Text, StringComparison.Ordinal);
            Assert.Contains("ElevenLabs:", result.Text, StringComparison.Ordinal);
            Assert.Equal(2, handler.Calls);
        });
    }

    // --- helpers ------------------------------------------------------------------

    private static PasswordBox Box(SettingsWindow w, string name) => (PasswordBox)w.FindName(name)!;
    private static TextBox Text(SettingsWindow w, string name) => (TextBox)w.FindName(name)!;
    private static TextBlock Label(SettingsWindow w, string name) => (TextBlock)w.FindName(name)!;
    private static string Password(SettingsWindow w, string name) => Box(w, name).Password;

    private static void Reveal(SettingsWindow w, bool on) =>
        ((CheckBox)w.FindName("ChkRevealKeys")!).IsChecked = on;

    private static void RaiseClick(SettingsWindow w, string name) =>
        ((Button)w.FindName(name)!).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

    /// <summary>The click handler is async void, so let its continuations run before asserting.</summary>
    private static void DrainDispatcher(Window w)
    {
        for (var i = 0; i < 20; i++)
            w.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private static void WithWindow(Action<SettingsWindow> assert)
    {
        var w = new SettingsWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000
        };

        w.Show();
        NewMeetingWindowLayoutTests.LayoutSettled(w);

        try { assert(w); }
        finally { w.Close(); }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
