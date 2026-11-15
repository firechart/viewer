namespace Flox.Core.Data;

/// <summary>Marks a property that the server keeps and never sends to clients.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ServerOnlyAttribute : Attribute;
