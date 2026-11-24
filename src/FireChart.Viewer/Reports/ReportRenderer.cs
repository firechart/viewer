using System.Collections.Frozen;
using FireChart.Viewer.Core.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FireChart.Viewer.Reports;

// Turns the nodes of a component into the render tree: a built-in is its Blazor block, a component of the project a CompositeBlock, a text a
// TextBlock. There is no markup string anywhere: what a definition can show is what the blocks render
internal static class ReportRenderer
{
    private static readonly FrozenDictionary<string, Type> s_blocks = new Dictionary<string, Type>
    {
        [ReportSchema.Section] = typeof(SectionBlock),
        [ReportSchema.Card] = typeof(CardBlock),
        [ReportSchema.Columns] = typeof(ColumnsBlock),
        [ReportSchema.Field] = typeof(FieldBlock),
        [ReportSchema.Value] = typeof(ValueBlock),
        [ReportSchema.Age] = typeof(AgeBlock),
        [ReportSchema.Badge] = typeof(BadgeBlock),
        [ReportSchema.Text] = typeof(SpanBlock),
        [ReportSchema.Repeat] = typeof(RepeatBlock),
        [ReportSchema.If] = typeof(IfBlock),
        [ReportSchema.SlotOutlet] = typeof(SlotOutletBlock),
        [ReportSchema.Row] = typeof(RowBlock),
        [ReportSchema.Status] = typeof(StatusBlock),
        [ReportSchema.Interpretation] = typeof(StatusBlock),
        [ReportSchema.Link] = typeof(LinkBlock),
        [ReportSchema.Table] = typeof(TableBlock),
        [ReportSchema.Column] = typeof(ColumnBlock),
        [ReportSchema.Details] = typeof(DetailsBlock),
        [ReportSchema.Notice] = typeof(NoticeBlock),
        [ReportSchema.Show] = typeof(ShowBlock),
        [ReportSchema.Resolve] = typeof(ResolveBlock),
        [ReportSchema.Parts] = typeof(PartsBlock),
        [ReportSchema.Subtitle] = typeof(PartBlock),
        [ReportSchema.Main] = typeof(PartBlock),
        [ReportSchema.Otherwise] = typeof(PartBlock),
        [ReportSchema.Feed] = typeof(FeedBlock),
        [ReportSchema.Tile] = typeof(TileBlock),
        [ReportSchema.Leading] = typeof(LeadingBlock),
        [ReportSchema.Trailing] = typeof(PartBlock),
        [ReportSchema.Banner] = typeof(BannerBlock),
        [ReportSchema.Facts] = typeof(PartBlock),
        [ReportSchema.Flags] = typeof(PartBlock),
        [ReportNode.TextName] = typeof(TextBlock),
    }.ToFrozenDictionary();

    public static RenderFragment Render(IEnumerable<ReportNode> nodes, ReportScope scope)
    {
        return builder =>
        {
            foreach (var node in nodes)
            {
                var type = s_blocks.GetValueOrDefault(node.Name) ?? (scope.Runtime.Components.ContainsKey(node.Name) ? typeof(CompositeBlock) : typeof(UnknownBlock));
                builder.OpenComponent(0, type);
                builder.AddComponentParameter(1, nameof(ReportBlock.Node), node);
                builder.AddComponentParameter(2, nameof(ReportBlock.Scope), scope);
                builder.CloseComponent();
            }
        };
    }

    public static void Note(RenderTreeBuilder builder, string message)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "fx-notice fx-notice-warning");
        builder.AddAttribute(2, "data-report-note", true);
        builder.AddContent(3, message);
        builder.CloseElement();
    }
}
