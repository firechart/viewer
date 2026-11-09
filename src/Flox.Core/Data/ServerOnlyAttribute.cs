namespace Flox.Core.Data;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ServerOnlyAttribute : Attribute;
