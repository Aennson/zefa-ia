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
    public string ApiKeyEnvVar { get; set; } = "ELEVENLABS_API_KEY";
    public string Language { get; set; } = "auto";
    public bool VadEnabled { get; set; } = true;
}
