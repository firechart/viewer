using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.FhirPath;

namespace FireChart.Viewer.Core.Reports;

// FHIRPath over the POCO nodes of a release (Firely 6). The variables (%context, the parameters, the item of a Repeat, the results of a search) are
// the environment of the evaluation; an expression is compiled once (Firely's cache)
internal static class ReportExpressions
{
    private static readonly FhirPathCompiler s_compiler = new();
    private static readonly FhirPathCompilerCache s_cache = new(s_compiler);

    // The check of a definition: an expression that does not compile is a problem with its line, not an exception at run time
    public static string? ErrorOf(string source)
    {
        try
        {
            s_cache.GetCompiledExpression(source);
            return null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ex.Message;
        }
    }

    public static IReadOnlyList<PocoNode> Evaluate(ReportExpression expression, PocoNode? focus, IReadOnlyDictionary<string, IReadOnlyList<PocoNode>> variables)
    {
        var context = new FhirEvaluationContext();
        foreach (var (name, value) in variables)
        {
            context.Environment[name] = value;
        }

        var start = focus ?? (variables.TryGetValue(ReportVariables.Context, out var resource) && resource.Count > 0 ? resource[0] : s_empty);
        return [.. s_cache.Select(start, expression.Source, context)];
    }

    // FHIRPath's truth of a result: a single boolean is itself, whether an element of the record (FhirBoolean) or the answer of an operator or a
    // function (empty(), and, =); anything else is true when not empty
    public static bool IsTrue(IReadOnlyList<PocoNode> result)
    {
        return result switch
        {
            [] => false,
            [{ Poco: PrimitiveType { JsonValue: bool value } }] => value,
            _ => true
        };
    }

    // A literal given to a parameter: a string node
    public static PocoNode String(string value)
    {
        return PocoNode.ForAnyPrimitive(value);
    }

    // Lenient, as the templates read a resource: an element the release does not know, or a value not of its type, is kept, not an error
    public static PocoNode Parse(string json, ModelInspector model)
    {
        var typed = FhirJsonNode.Parse(json).ToTypedElement(model, settings: new TypedElementSettings { ErrorMode = TypedElementSettings.TypeErrorMode.Passthrough });
        if (typed is Hl7.Fhir.Utility.IExceptionSource source)
        {
            source.ExceptionHandler = (_, _) => { };
        }

        return typed.ToPocoNode(model);
    }

    private static readonly PocoNode s_empty = PocoNode.ForAnyPrimitive(true);
}

internal static class ReportVariables
{
    public const string Context = "context";
    public const string Item = "item";

    // The parameter of a component of a mode ({Type}.{mode}): the element it draws
    public const string Record = "record";
}
