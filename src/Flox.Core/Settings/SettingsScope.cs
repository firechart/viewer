namespace Flox.Core.Settings;

/// <summary>Who owns a settings class and who may change it.</summary>
public enum SettingsScope
{
    /// <summary>The user manages it fully and nobody else sees it.</summary>
    User,

    /// <summary>Written by administrators in the administration and read by everyone, a user who is not signed in included.</summary>
    System
}
