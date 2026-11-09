
using FireChart.Viewer.Core.Forms;
using Flox.Core.Data;

namespace FireChart.Viewer.Core.Search;

/// <summary>
/// One query parameter of a search made by a form.
/// </summary>
/// <param name="Name">The name of the parameter (<c>birthdate</c>); a chain keeps its dots (<c>subject:Patient.name</c>).</param>
/// <param name="Modifier">The modifier (<c>exact</c>, <c>missing</c>), or <see langword="null"/>.</param>
/// <param name="Comparator">The prefix of a number, a date or a quantity (<c>ge</c>, <c>lt</c>), or <see langword="null"/> for equality.</param>
/// <param name="Type">The type of the parameter.</param>
/// <param name="Values">The FHIR text forms of the value, already escaped; several are alternatives (OR).</param>
/// <example>
/// <code>
/// var born = new SearchCondition("birthdate", null, "ge", SearchParameterType.Date, ["1970-01-01"]);
/// var query = $"{born.ParameterName}={born.FhirValue}"; // birthdate=ge1970-01-01
/// </code>
/// </example>
public sealed record SearchCondition
(
    string Name,
    string? Modifier,
    string? Comparator,
    SearchParameterType Type,
    IReadOnlyList<string> Values
)
{
    /// <summary>
    /// The name of the query parameter with its modifier (<c>name:exact</c>).
    /// </summary>
    public string ParameterName
    {
        get
        {
            return string.IsNullOrEmpty(Modifier) ? Name : $"{Name}:{Modifier}";
        }
    }

    /// <summary>
    /// The value of the query parameter: the alternatives joined by commas, the comparator before every one of them.
    /// </summary>
    public string FhirValue
    {
        get
        {
            return string.Join(',', Values.Select(v => Comparator + v));
        }
    }

    /// <summary>
    /// The condition as an operator of a filter of Flox (the lists of conformance resources): the comparator, else <c>In</c> for alternatives, else what
    /// a FHIR string search means (it matches the beginning of the value unless a modifier says otherwise).
    /// </summary>
    public FilterOperator Operator
    {
        get
        {
            if (Comparator != null)
            {
                return Comparator switch
                {
                    "ne" => FilterOperator.Ne,
                    "gt" or "sa" => FilterOperator.Gt,
                    "ge" => FilterOperator.Gte,
                    "lt" or "eb" => FilterOperator.Lt,
                    "le" => FilterOperator.Lte,
                    var _ => FilterOperator.Eq
                };
            }

            if (Values.Count > 1)
            {
                return FilterOperator.In;
            }

            // A FHIR string search matches the beginning of the value unless a modifier says otherwise
            return Type == SearchParameterType.String
                ? Modifier switch
                {
                    "exact" => FilterOperator.Eq,
                    "contains" => FilterOperator.Contains,
                    var _ => FilterOperator.StartsWithIgnoreCase
                }
                : FilterOperator.Eq;
        }
    }
}
