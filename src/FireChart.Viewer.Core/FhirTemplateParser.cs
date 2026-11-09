using System.Text.Encodings.Web;
using System.Text.Json;
using Fluid;
using Fluid.Ast;
using Fluid.Values;

namespace FireChart.Viewer.Core;

// Liquid with the {% apply expression [mode: "name"] %} tag: the XSLT-like dispatch of the elements to their templates
internal sealed class FhirTemplateParser : FluidParser
{
    public const string ApplyTag = "apply";
    public const string ModeArgument = "mode";
    public const string IncludeTag = "fhir_include";
    public const string RevIncludeTag = "fhir_revinclude";

    public FhirTemplateParser(Func<FhirNode, string?, TextWriter, TextEncoder, TemplateContext, ValueTask> apply)
    {
        RegisterParserTag(ApplyTag, Primary.And(ZeroOrOne(ArgumentsList)), async (arguments, writer, encoder, context) =>
        {
            var named = new NamedExpressionList(arguments.Item2 ?? []);
            string? mode = null;
            if (named.HasNamed(ModeArgument))
            {
                mode = (await named[ModeArgument].EvaluateAsync(context)).ToStringValue();
            }

            var value = await arguments.Item1.EvaluateAsync(context);
            var items = value is FhirNodeValue ? [value] : value.Enumerate(context);
            foreach (var item in items)
            {
                if (item is FhirNodeValue node)
                {
                    await apply(node.Node, mode, writer, encoder, context);
                }
            }

            return Completion.Normal;
        });

        // Declarations of what the template needs from the server (see FhirNeeds): they write nothing, and are read from the text of the template
        RegisterExpressionTag(IncludeTag, (_, _, _, _) => new ValueTask<Completion>(Completion.Normal));
        RegisterExpressionTag(RevIncludeTag, (_, _, _, _) => new ValueTask<Completion>(Completion.Normal));
    }
}
