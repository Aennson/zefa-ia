using Xunit;
using ZefaIA.Overlay;

namespace ZefaIA.Overlay.Tests;

/// <summary>
/// The whole point of this type is that a secret never reaches disk in the clear, so the
/// tests assert on the stored form, not just on the round trip.
/// </summary>
public class SecretProtectorTests
{
    private const string Key = "sk-ant-api03-EXAMPLE-not-a-real-key-0123456789abcdef";

    [Fact]
    public void ProtectThenUnprotectReturnsTheOriginal()
    {
        Assert.Equal(Key, SecretProtector.Unprotect(SecretProtector.Protect(Key)));
    }

    [Fact]
    public void ProtectedValueDoesNotContainTheSecret()
    {
        var stored = SecretProtector.Protect(Key);

        Assert.DoesNotContain(Key, stored, StringComparison.Ordinal);
        // Not even a recognisable fragment: a prefix search through settings.json must miss.
        Assert.DoesNotContain("sk-ant", stored, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(SecretProtector.Prefix, stored, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AnEmptySecretStaysEmptyRatherThanBecomingCiphertext(string? secret)
    {
        // Encrypting "" would produce a non-empty stored value, which the UI would then
        // read back as "a key is configured".
        Assert.Equal(string.Empty, SecretProtector.Protect(secret));
    }

    [Fact]
    public void UnicodeSurvivesTheRoundTrip()
    {
        const string value = "chave-com-acentuação-e-emoji-🔑";

        Assert.Equal(value, SecretProtector.Unprotect(SecretProtector.Protect(value)));
    }

    [Fact]
    public void AValueWrittenBeforeEncryptionIsStillReadable()
    {
        // Settings files written by earlier builds, or hand-edited, hold a bare key.
        // Rejecting those would silently disable a working configuration.
        Assert.Equal(Key, SecretProtector.Unprotect(Key));
    }

    [Fact]
    public void CiphertextFromAnotherMachineReadsAsNoKeyInsteadOfThrowing()
    {
        // DPAPI is bound to the Windows account, so a settings.json copied from another
        // user or PC cannot be decrypted here. That must degrade to "not configured" —
        // throwing would take the app down during startup.
        var foreign = SecretProtector.Prefix + Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        Assert.Equal(string.Empty, SecretProtector.Unprotect(foreign));
    }

    [Fact]
    public void GarbageThatIsNotEvenBase64ReadsAsNoKey()
    {
        Assert.Equal(string.Empty, SecretProtector.Unprotect(SecretProtector.Prefix + "!!!not base64!!!"));
    }

    [Fact]
    public void TamperedCiphertextIsRejectedRatherThanDecodedToNoise()
    {
        var stored = SecretProtector.Protect(Key);
        var bytes = Convert.FromBase64String(stored[SecretProtector.Prefix.Length..]);
        bytes[^1] ^= 0xFF;

        var tampered = SecretProtector.Prefix + Convert.ToBase64String(bytes);

        Assert.Equal(string.Empty, SecretProtector.Unprotect(tampered));
    }

    [Fact]
    public void MaskShowsEnoughToIdentifyTheKeyAndNoMore()
    {
        var masked = SecretProtector.Mask(Key);

        Assert.Equal("sk-ant…cdef", masked);
        Assert.DoesNotContain(Key, masked, StringComparison.Ordinal);
    }

    [Fact]
    public void MaskDoesNotSplitAShortValueIntoSomethingReadable()
    {
        Assert.Equal("••••••", SecretProtector.Mask("abc123"));
        Assert.Equal(string.Empty, SecretProtector.Mask(""));
    }
}
