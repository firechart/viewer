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
    public const string SlotTag = "slot";
    public const string SlotDataTag = "slot_data";

    private IFluidTemplate? _patientLine;
    private IFluidTemplate? _encounterLine;
    private IFluidTemplate? _ageLine;

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
            // The other named arguments are variables of the applied templates and of what they apply in turn ({% apply self.value mode: "text", decimals: 1 %})
            var passed = new Dictionary<string, FluidValue>(StringComparer.Ordinal);
            foreach (var name in named.Names.Where(name => name != ModeArgument))
            {
                passed[name] = await named[name].EvaluateAsync(context);
            }

            if (passed.Count > 0)
            {
                context.EnterChildScope();
                foreach (var (name, argument) in passed)
                {
                    context.SetValue(name, argument);
                }
            }

            try
            {
                foreach (var item in items)
                {
                    if (item is FhirNodeValue node)
                    {
                        await apply(node.Node, mode, writer, encoder, context);
                    }
                }
            }
            finally
            {
                if (passed.Count > 0)
                {
                    context.ReleaseScope();
                }
            }

            return Completion.Normal;
        });

        // Declarations of what the template needs from the server (see FhirNeeds): they write nothing, and are read from the text of the template
        RegisterExpressionTag(IncludeTag, (_, _, _, _) => new ValueTask<Completion>(Completion.Normal));
        RegisterExpressionTag(RevIncludeTag, (_, _, _, _) => new ValueTask<Completion>(Completion.Normal));

        // {% slot "title" %}...{% endslot %}: a named piece of a tile (FrameSlots); it writes nothing, and outside a tile it is dropped
        RegisterExpressionBlock(SlotTag, async (name, statements, _, encoder, context) =>
        {
            if (FrameSlots.Of(context) is not { } slots)
            {
                return Completion.Normal;
            }

            using var piece = new StringWriter();
            foreach (var statement in statements)
            {
                var completion = await statement.WriteToAsync(piece, encoder, context);
                if (completion != Completion.Normal)
                {
                    break;
                }
            }

            slots.Html[(await name.EvaluateAsync(context)).ToStringValue()] = piece.ToString();
            return Completion.Normal;
        });

        // {% slot_data patient: self.subject, encounter: self.encounter, at: self.effective %}: the references of the record; the frame names the patient and the
        // encounter on the second line (the partials tile-patient and tile-encounter, which leave out what the page shows above) and the age at the moment
        RegisterParserTag(SlotDataTag, ArgumentsList, async (arguments, _, encoder, context) =>
        {
            if (FrameSlots.Of(context) is not { } slots)
            {
                return Completion.Normal;
            }

            var named = new NamedExpressionList(arguments);
            var patient = named.HasNamed(FrameSlots.Patient) ? await named[FrameSlots.Patient].EvaluateAsync(context) : NilValue.Instance;
            var encounter = named.HasNamed(FrameSlots.Encounter) ? await named[FrameSlots.Encounter].EvaluateAsync(context) : NilValue.Instance;
            var at = named.HasNamed(FrameSlots.At) ? await named[FrameSlots.At].EvaluateAsync(context) : NilValue.Instance;
            if (!patient.IsNil())
            {
                slots.Html[FrameSlots.Patient] = await LineAsync(_patientLine ??= this.Parse("{% render \"tile-patient\", ref: fx_ref %}"), patient, NilValue.Instance, encoder, context);
            }

            if (!encounter.IsNil())
            {
                slots.Html[FrameSlots.Encounter] = await LineAsync(_encounterLine ??= this.Parse("{% render \"tile-encounter\", ref: fx_ref, at: fx_at %}"), encounter, at, encoder, context);
            }

            if (!patient.IsNil() && !at.IsNil())
            {
                slots.Html[FrameSlots.Age] = await LineAsync(_ageLine ??= this.Parse("{% render \"age-at\", patient: fx_ref, when: fx_at %}"), patient, at, encoder, context);
            }

            return Completion.Normal;
        });
    }

    private static async Task<string> LineAsync(IFluidTemplate template, FluidValue reference, FluidValue at, TextEncoder encoder, TemplateContext context)
    {
        using var line = new StringWriter();
        context.EnterChildScope();
        try
        {
            context.SetValue("fx_ref", reference);
            context.SetValue("fx_at", at);
            await template.RenderAsync(line, encoder, context);
        }
        finally
        {
            context.ReleaseScope();
        }

        return line.ToString();
    }
}
