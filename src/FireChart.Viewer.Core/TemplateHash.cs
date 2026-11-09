using System.Security.Cryptography;
using System.Text;

namespace FireChart.Viewer.Core;

/// <summary>
/// A short, stable hash of the text of a template: the version of a template and of a library of templates.
/// </summary>
/// <example>
/// <code>
/// var version = TemplateHash.Of(File.ReadAllText("Patient@tile.liquid"));   // "3F9A0C1B2D4E5F60"
/// </code>
/// </example>
public static class TemplateHash
{
    /// <summary>
    /// The hash of a text: the first 16 hexadecimal digits of the SHA-256 of its UTF-8 bytes after <see cref="Normalize"/>.
    /// </summary>
    /// <param name="text">The text of a template.</param>
    /// <returns>16 upper-case hexadecimal digits. Line endings do not count: the same text from a Windows and a Linux checkout has the same hash.</returns>
    public static string Of(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(Normalize(text));
        return Convert.ToHexString(SHA256.HashData(bytes))[..16];
    }

    /// <summary>
    /// The text with Windows line endings (<c>\r\n</c>) turned into <c>\n</c>, the form that <see cref="Of"/> hashes.
    /// </summary>
    /// <param name="text">The text of a template.</param>
    /// <returns>The normalized text.</returns>
    public static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
