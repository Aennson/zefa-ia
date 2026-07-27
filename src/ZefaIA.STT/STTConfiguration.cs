namespace ZefaIA.STT;

public class STTSettings
{
    public string ActiveProvider { get; set; } = "WhisperLocal";
    public WhisperLocalSettings WhisperLocal { get; set; } = new();
    public ElevenLabsSettings ElevenLabs { get; set; } = new();
}

public class WhisperLocalSettings
{
    public string ModelSize { get; set; } = "base";
    public string Language { get; set; } = "auto";
    public bool UseGPU { get; set; }
    public string ModelPath { get; set; } = "./models";
    public int BufferMs { get; set; } = 2500;
}

public class ElevenLabsSettings
{
    /// <summary>
    /// The key itself, when the user configured one in Settings. Takes precedence over
    /// <see cref="ApiKeyEnvVar"/>; empty means "fall back to the environment variable".
    /// Never persisted here — it is read from the (encrypted) app settings at startup.
    /// </summary>
    public string ApiKey { get; set; } = "";

    public string ApiKeyEnvVar { get; set; } = "ELEVENLABS_API_KEY";
    public string Language { get; set; } = "auto";
    public bool VadEnabled { get; set; } = true;
}
