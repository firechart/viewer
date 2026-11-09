using System.Globalization;
using FireChart.Viewer.Core.Forms;

namespace FireChart.Viewer.Pages;

/// <summary>One search of a block of the page: what it asks the server for and how far it has got.</summary>
/// <param name="definition">The search of the form.</param>
/// <param name="culture">The culture of the label of the order.</param>
public sealed class SectionSearch(ClinicalSummaryFormSearch definition, string culture)
{
    /// <summary>The size of a page of a search when the form does not say.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>The type the search reads.</summary>
    public string ResourceType { get; } = definition.ResourceType;

    /// <summary>The order the server is asked for (newest first); empty asks for none.</summary>
    public string Sort { get; } = definition.Sort?.Trim() ?? string.Empty;

    /// <summary>What the order is by, for the line under the title.</summary>
    public string SortedBy { get; } = definition.SortLabel.Resolve(culture);

    /// <summary>The size of a page.</summary>
    public int PageSize { get; } = definition.PageSize ?? DefaultPageSize;

    /// <summary>False when the server did not know the order and the records came in whatever order it likes.</summary>
    public bool ServerSorted { get; set; } = true;

    /// <summary>The link of the next page, or null on the last one.</summary>
    public string? Next { get; set; }

    /// <summary>The first page is in.</summary>
    public bool IsLoaded { get; set; }

    /// <summary>The last request failed.</summary>
    public bool Failed { get; set; }

    /// <summary>The server does not have the type or refuses to search it: the search is left out instead of an error.</summary>
    public bool Unsupported { get; set; }

    /// <summary>The query of the first page: the patient, the other parameters of the form, the order and the size of the page.</summary>
    /// <param name="patientId">The id of the patient.</param>
    /// <param name="sorted">Ask for the order of the form.</param>
    /// <returns>The query without the question mark.</returns>
    public string FirstQuery(string patientId, bool sorted)
    {
        var parts = new List<string> { $"{definition.PatientParam}={Uri.EscapeDataString(patientId)}" };
        if (!string.IsNullOrWhiteSpace(definition.Query))
        {
            parts.Add(definition.Query.Trim());
        }

        if (sorted && Sort.Length > 0)
        {
            parts.Add($"_sort={Sort}");
        }

        parts.Add($"_count={PageSize}");
        return string.Join('&', parts);
    }
}

/// <summary>One block of the page: the records of its searches together, newest first, loaded page by page.</summary>
public sealed class ClinicalSummarySection
{
    private readonly List<ResourceSummary> _items = [];

    /// <summary>Makes a block of the definition of the form.</summary>
    /// <param name="definition">The block of the form.</param>
    /// <param name="searches">Its searches the server can do.</param>
    /// <param name="culture">The culture of the titles.</param>
    public ClinicalSummarySection(ClinicalSummaryFormSection definition, IEnumerable<ClinicalSummaryFormSearch> searches, string culture)
    {
        Code = definition.Code;
        CollapsedByDefault = definition.CollapsedByDefault;
        Collapsed = definition.CollapsedByDefault;
        HideIfUnsupported = definition.HideIfUnsupported;
        Searches = [.. searches.Select(search => new SectionSearch(search, culture))];
        var title = definition.Title.Resolve(culture);
        Title = string.IsNullOrWhiteSpace(title) ? (Searches.Count > 0 ? Searches[0].ResourceType : Code) : title;
    }

    /// <summary>The code of the block in the form.</summary>
    public string Code { get; }

    /// <summary>The title of the block in the culture of the page.</summary>
    public string Title { get; }

    /// <summary>The rows are not shown, only the header with the count; the header opens and closes it.</summary>
    public bool Collapsed { get; set; }

    /// <summary>The form shows the block closed.</summary>
    public bool CollapsedByDefault { get; }

    /// <summary>The type that stands for the block (the first search): its icon is in the header.</summary>
    public string IconType => Searches.Count > 0 ? Searches[0].ResourceType : string.Empty;

    /// <summary>A search of a type the server does not have is left out instead of failing.</summary>
    public bool HideIfUnsupported { get; }

    /// <summary>The searches of the block.</summary>
    public IReadOnlyList<SectionSearch> Searches { get; }

    /// <summary>False when a search fell back to the server's own order.</summary>
    public bool ServerSorted => Searches.All(search => search.ServerSorted);

    /// <summary>The records were put in order by their moment here, because the server's order was different.</summary>
    public bool Reordered { get; private set; }

    /// <summary>What the orders of the searches are by, for the line under the title.</summary>
    public string SortedBy => string.Join(", ", Searches.Select(search => search.SortedBy).Where(text => text.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));

    /// <summary>The records of the block, newest first.</summary>
    public IReadOnlyList<ResourceSummary> Items => _items;

    /// <summary>Adds records; they are kept newest first by their real moment, whatever the server meant by its order; a record without a moment goes last.</summary>
    /// <param name="items">The records of a page.</param>
    public void Add(IEnumerable<ResourceSummary> items)
    {
        _items.AddRange(items);
        var ordered = _items.Select((item, index) => new Ranked(item, ResourceMoment.Of(item.ResourceType, item.Json), index))
            .OrderByDescending(entry => entry.Moment.HasValue).ThenByDescending(entry => entry.Moment).ThenBy(entry => entry.Index).ToList();
        if (ordered.Select(entry => entry.Index).SequenceEqual(Enumerable.Range(0, ordered.Count)))
        {
            return;
        }

        Reordered = true;
        _items.Clear();
        _items.AddRange(ordered.Select(entry => entry.Item));
    }

    private sealed record Ranked(ResourceSummary Item, DateTimeOffset? Moment, int Index);

    /// <summary>The row of every record as the template of its tile makes it, by "Type/id".</summary>
    public Dictionary<string, TileModel> Tiles { get; } = [];

    /// <summary>The key of a record in Tiles: Type/id.</summary>
    /// <param name="item">The record.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(ResourceSummary item)
    {
        return $"{item.ResourceType}/{item.Id}";
    }

    /// <summary>A page of the block is being loaded.</summary>
    public bool IsLoading { get; set; }

    /// <summary>Every search that is not left out has its first page.</summary>
    public bool IsLoaded => Searches.Where(search => !search.Unsupported).All(search => search.IsLoaded);

    /// <summary>A search or the rendering failed: the block offers a retry.</summary>
    public bool Failed { get; set; }

    /// <summary>Every search was left out: nothing to show, not even a title.</summary>
    public bool Hidden => HideIfUnsupported && Searches.Count > 0 && Searches.All(search => search.Unsupported);

    /// <summary>A search has a next page.</summary>
    public bool HasMore => Searches.Any(search => search.Next is not null);

    /// <summary>The total is not asked for: the sign says there is more.</summary>
    public string CountText
    {
        get
        {
            var count = Items.Count.ToString(CultureInfo.CurrentCulture);
            return HasMore ? count + "+" : count;
        }
    }
}

/// <summary>Which blocks of the form the page has for a server: the visible ones, without the searches the server cannot do.</summary>
public static class ClinicalSummarySections
{
    /// <summary>The blocks of a form for a server.</summary>
    /// <param name="form">The form.</param>
    /// <param name="declaredTypes">The types the server says it has; null when it does not say (every search is tried).</param>
    /// <param name="culture">The culture of the titles.</param>
    /// <returns>The visible blocks that have a search.</returns>
    public static IReadOnlyList<ClinicalSummarySection> Build(ClinicalSummaryFormDefinition form, IReadOnlyCollection<string>? declaredTypes, string culture)
    {
        var result = new List<ClinicalSummarySection>();
        foreach (var definition in form.Sections.Where(section => section.Visible))
        {
            var searches = definition.Searches.Where(search => !definition.HideIfUnsupported || declaredTypes is null || declaredTypes.Contains(search.ResourceType)).ToList();
            if (searches.Count > 0)
            {
                result.Add(new ClinicalSummarySection(definition, searches, culture));
            }
        }

        return result;
    }
}
