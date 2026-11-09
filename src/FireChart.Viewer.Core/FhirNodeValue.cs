using Fluid;
using Fluid.Values;

namespace FireChart.Viewer.Core;

// Gives a template the children of an element by name: one value or a list, as the element is declared in the model
internal sealed class FhirNodeValue(FhirNode node) : ObjectValueBase(node)
{
    public FhirNode Node => node;

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
    {
        var children = node.Children(name);
        if (children.Count == 0)
        {
            return new ValueTask<FluidValue>(NilValue.Instance);
        }

        if (node.Tracked && context.AmbientValues.TryGetValue(RenderTracker.Key, out var tracker) && tracker is RenderTracker shown)
        {
            foreach (var child in children)
            {
                shown.Touch(child.Element.Location);
            }
        }

        FluidValue result = children.Count == 1 && !node.IsCollection(name)
            ? new FhirNodeValue(children[0])
            : new ArrayValue([.. children.Select(c => new FhirNodeValue(c))]);

        return new ValueTask<FluidValue>(result);
    }

    public override bool ToBooleanValue()
    {
        return true;
    }

    public override string ToStringValue()
    {
        return node.ValueText() ?? string.Empty;
    }
}
