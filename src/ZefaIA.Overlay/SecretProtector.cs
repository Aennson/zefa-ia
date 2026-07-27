using System.Security.Cryptography;
using System.Text;

namespace ZefaIA.Overlay;

/// <summary>
/// Encrypts the API keys before they reach settings.json.
///
/// The file sits in %APPDATA%, readable by anything running as the user, and gets
/// copied around by backup tools and support requests. DPAPI with
/// <see cref="DataProtectionScope.CurrentUser"/> means a stolen settings.json is
/// useless on another machine or under another account: the key material is derived
/// from the Windows login and never leaves it.
///
/// This is not protection against malware already running as the user — nothing
/// stored locally can be. It is protection against the key travelling somewhere it
/// was never meant to go.
/// </summary>
public static class SecretProtector
{
    /// <summary>Marks a value as DPAPI-encrypted, so an older plaintext value is still readable.</summary>
    internal const string Prefix = "dpapi:";

    /// <summary>
    /// Returns a value safe to write to disk, or an empty string for an empty secret
    /// (an encrypted empty string would still look like a stored key to the UI).
    /// </summary>
    public static string Protect(string? secret)
    {
        if (string.IsNullOrEmpty(secret))
            return string.Empty;

        try
        {
            var cipher = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(secret), optionalEntropy: null, DataProtectionScope.CurrentUser);

            return Prefix + Convert.ToBase64String(cipher);
        }
        catch (CryptographicException)
        {
            // DPAPI is unavailable (some service accounts, non-Windows hosts). Refusing to
            // store the key beats silently writing it in the clear.
            return string.Empty;
        }
    }

    /// <summary>
    /// Reverses <see cref="Protect"/>. Returns an empty string when the value cannot be
    /// decrypted — a settings.json carried over from another machine or user account
    /// should behave like "no key configured", not crash the app on startup.
    /// </summary>
    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
            return string.Empty;

        // Written before keys were encrypted, or hand-edited by the user.
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored;

        try
        {
            var cipher = Convert.FromBase64String(stored[Prefix.Length..]);
            var plain = ProtectedData.Unprotect(cipher, optionalEntropy: null, DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Renders a key for display without exposing it: <c>sk-ant-…YLIbVQAA</c>. Used in
    /// logs and in the settings UI so the user can tell which key is stored without the
    /// whole secret sitting on screen.
    /// </summary>
    public static string Mask(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return string.Empty;

        // Too short to split without revealing most of it.
        if (secret.Length <= 12)
            return new string('•', secret.Length);

        return secret[..6] + "…" + secret[^4..];
    }
}
