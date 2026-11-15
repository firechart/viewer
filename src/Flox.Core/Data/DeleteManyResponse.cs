namespace Flox.Core.Data;

/// <summary>
/// The answer of the DeleteMany action of the OData controllers.
/// </summary>
/// <param name="Value">The number of deleted documents.</param>
public sealed record DeleteManyResponse
(
    long Value
);
