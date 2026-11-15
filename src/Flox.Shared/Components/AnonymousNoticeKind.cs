namespace Flox.Shared.Components;

/// <summary>
/// What a caller who is not signed in gets of the data on a page.
/// </summary>
public enum AnonymousNoticeKind
{
    /// <summary>
    /// Sign in is required to use the data.
    /// </summary>
    Refuse,

    /// <summary>
    /// Changes are kept in memory until the page is reloaded.
    /// </summary>
    Memory,

    /// <summary>
    /// The shared rows can be read; own rows need a sign-in.
    /// </summary>
    ReadShared
}
