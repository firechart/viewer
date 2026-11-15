namespace Flox.Core.Models;

/// <summary>What a list needs to show an entity as a link: its kind, name and address.</summary>
public interface IEntityDisplayInfo
{
    /// <summary>The kind of the entity.</summary>
    string Category { get; set; }

    /// <summary>The text shown for the entity.</summary>
    string DisplayName { get; set; }

    /// <summary>The address of the entity in the application.</summary>
    string Url { get; set; }
}
