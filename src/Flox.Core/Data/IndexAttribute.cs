namespace Flox.Core.Data;

/// <summary>
/// Marks how a property is indexed in the document store: a column with a btree index (the default), a substring search (<see cref="Like"/>)
/// and a full-text weight (<see cref="FullText"/>). The text search covers only what is marked here; an entity with no Like or FullText
/// property is not searchable by text.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class IndexAttribute : Attribute
{
    /// <summary>Creates a plain index.</summary>
    public IndexAttribute() { }

    /// <summary>Creates an index that is unique or not.</summary>
    public IndexAttribute(bool unique)
    {
        Unique = unique;
    }

    /// <summary>Tells whether the index enforces unique values.</summary>
    public bool Unique { get; }

    /// <summary>The name of the index.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Tells whether the property gets a column with a btree index (filters, sorts, unique keys); false for a long text that is only searched.
    /// </summary>
    public bool Btree { get; set; } = true;

    /// <summary>
    /// Tells whether the property is searched as a substring (case-insensitive) when the full-text search string of a request is set; such a
    /// row comes before one found only by the full-text index: the exact value, then one that starts with the text, then one that contains it,
    /// the shorter first.
    /// </summary>
    public bool Like { get; set; }

    /// <summary>
    /// The weight of the property in the full-text index of the entity; a match in a property of a higher weight ranks above one in a lower
    /// weight.
    /// </summary>
    public FullTextWeight FullText { get; set; } = FullTextWeight.None;
}

/// <summary>The weight of a property in the full-text index, the most significant first.</summary>
public enum FullTextWeight
{
    /// <summary>The property is not in the full-text index.</summary>
    None,
    /// <summary>The highest weight.</summary>
    A,
    /// <summary>The second weight.</summary>
    B,
    /// <summary>The third weight.</summary>
    C,
    /// <summary>The lowest weight.</summary>
    D
}
