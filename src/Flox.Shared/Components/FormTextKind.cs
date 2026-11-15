namespace Flox.Shared.Components;

/// <summary>
/// What a <see cref="FormTextField"/> holds, which sets the keyboard of a phone and whether the browser corrects the text.
/// </summary>
public enum FormTextKind
{
    /// <summary>
    /// Free text.
    /// </summary>
    Text,

    /// <summary>
    /// An address: the keyboard of URLs, no capitals, no corrections, no autofill.
    /// </summary>
    Url,

    /// <summary>
    /// A code or an identifier (a client id, a scope): no capitals, no corrections, no autofill.
    /// </summary>
    Identifier
}
