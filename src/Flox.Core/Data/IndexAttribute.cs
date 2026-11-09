namespace Flox.Core.Data;

// How a property is indexed in the document store. Plain [Index]: a column of the table with a btree index (filters, sorts, unique keys).
// Like: a substring search (case-insensitive LIKE) on the property when PagedRequest.FullTextSearchString is set; a row found here comes before a
// row found only by the full-text index: the exact value, then one that starts with the text, then one that contains it, the shorter first.
// FullText: the property joins the entity's full-text index, a match in a property of a higher weight ranks above one in a lower weight (several
// properties need different weights; Fisher builds one FTS5 index and ignores the weight). Btree = false: no column and no btree index, for a
// long text that is only searched. The search covers only what is marked here; an entity with no Like or FullText property is not searchable by text
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class IndexAttribute : Attribute
{
    public IndexAttribute() { }

    public IndexAttribute(bool unique)
    {
        Unique = unique;
    }

    public bool Unique { get; }

    public string? Name { get; set; }

    public bool Btree { get; set; } = true;

    public bool Like { get; set; }

    public FullTextWeight FullText { get; set; } = FullTextWeight.None;
}

// The weight of a property in the full-text index, the most significant first; None keeps the property out of it
public enum FullTextWeight
{
    None,
    A,
    B,
    C,
    D
}
