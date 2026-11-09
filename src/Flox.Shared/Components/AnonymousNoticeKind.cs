namespace Flox.Shared.Components;

// What a caller who is not signed in gets of the data on a page
public enum AnonymousNoticeKind
{
    // Sign in to use it
    Refuse,

    // Changes are kept until the page is reloaded
    Memory,

    // The shared rows are read, own rows need the sign-in
    ReadShared
}
