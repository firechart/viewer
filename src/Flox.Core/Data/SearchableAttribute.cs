namespace Flox.Core.Data;

/// <summary>The kind of control that a generic search form renders for a property.</summary>
public enum SearchControlType
{
    /// <summary>
    /// Inferred from the property type (and from <see cref="SearchableAttribute.Options"/>).
    /// </summary>
    Auto,
    /// <summary>A text box.</summary>
    Text,
    /// <summary>A drop-down with one choice.</summary>
    Select,
    /// <summary>A drop-down with several choices.</summary>
    MultiSelect,
    /// <summary>A yes or no choice.</summary>
    Bool,
    /// <summary>A range of dates.</summary>
    DateRange,
    /// <summary>A range of numbers.</summary>
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

    /// <summary>The control to render; <see cref="SearchControlType.Auto"/> infers it.</summary>
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

    /// <summary>The position of the field in the form; lower comes first.</summary>
    public int Order { get; set; }
}
