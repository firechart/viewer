namespace Flox.Core.Data;

/// <summary>
/// Marks a property that an update cannot change; <see cref="Entity{TKey}.CopyImmutableFieldsFrom"/> restores it from the stored copy.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ImmutableAttribute : Attribute;
