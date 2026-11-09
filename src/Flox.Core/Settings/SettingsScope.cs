namespace Flox.Core.Settings;

public enum SettingsScope
{
    // The user manages it fully, nobody else sees it
    User,

    // Written by administrators in the administration, read by everyone, a user who is not signed in included
    System
}
