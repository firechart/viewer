namespace Flox.Core.Data;

public enum SearchControlType
{
    /// <summary>
    /// Inferred from the property type (and from <see cref="SearchableAttribute.Options"/>).
    /// </summary>
    Auto,
    Text,
    Select,
    MultiSelect,
    Bool,
    DateRange,
    NumberRange
}

/// <summary>
/// Marks an entity property as a field that can be rendered and filtered by a generic search form
/// (see <c>Flox.Shared.Components.Search.EntitySearchForm</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SearchableAttribute : Attribute
{
    /// <summary>
    /// Display label. Defaults to the property name.
    /// </summary>
    public string? Label { get; set; }

    public SearchControlType ControlType { get; set; } = SearchControlType.Auto;

    /// <summary>
    /// Filter operator used when translating the entered value into a <see cref="Filter"/>.
    /// <see cref="FilterOperator.None"/> picks a sensible default for the resolved <see cref="ControlType"/>.
    /// </summary>
    public FilterOperator Operator { get; set; } = FilterOperator.None;

    /// <summary>
    /// Fixed set of selectable values for <see cref="SearchControlType.Select"/> / <see cref="SearchControlType.MultiSelect"/>.
    /// If set and <see cref="ControlType"/> is <see cref="SearchControlType.Auto"/>, the control is rendered as a select.
    /// </summary>
    public string[]? Options { get; set; }

    public int Order { get; set; }
}
