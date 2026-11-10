using System.Text.Encodings.Web;
using System.Text.Json;
using Fluid;
using Fluid.Values;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace FireChart.Viewer.Core;

/// <summary>
/// Renders FHIR resources to HTML with Liquid (Fluid) templates: the page of a resource, the row of a list.
/// </summary>
/// <remarks>
/// <para>
/// The templates come from layers (<see cref="ITemplateSource"/>): the first layer that has a key wins, so overrides go before the vendor library. A layer
/// that is also an <see cref="ITemplateTexts"/> supplies the translations of the static texts. A template is chosen by the type of the element and the chain of
/// modes in force (<c>Patient@tile</c> before <c>Patient</c>); <c>{% apply x mode: "..." %}</c> renders a child element by its own template.
/// </para>
/// <para>
/// The renderer reads JSON of any of the releases the model of the SDK knows; a resource is never sent anywhere. The HTML is not trusted: the caller passes the
/// page through <see cref="FxHtml.Sanitize"/> (a row is sanitized already). One instance can be shared and reused; it keeps the parsed templates.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var renderer = new FhirRenderer([new VendorTemplates()], problem =&gt; logger.LogWarning("Template: {Problem}", problem));
/// var page = FxHtml.Sanitize(await renderer.RenderViewAsync(patientJson, "view"));
/// var row = await renderer.RenderTileAsync(patientJson);
/// </code>
/// </example>
public sealed class FhirRenderer
{
    /// <summary>
    /// How deep templates may apply templates of child elements before the rendering stops with an error (a template that applies itself).
    /// </summary>
    public const int MaxDepth = 32;

    private const string DepthKey = "fhir:depth";
    private const string ModeKey = "fhir:mode";
    private const string PoolKey = "fhir:pool";
    private const string ContextKey = "fhir:context";
    private const string ProblemsKey = "fhir:problems";

    private readonly TemplateLibrary _library;
    private readonly IReadOnlyList<ITemplateTexts> _texts;
    private readonly TemplateOptions _options = new();
    private readonly Action<string>? _onProblem;

    /// <summary>
    /// Creates a renderer over layers of templates.
    /// </summary>
    /// <param name="layers">The layers of templates, the strongest first (overrides, then the vendor library).</param>
    /// <param name="onProblem">Receives the problems of the templates (a template that does not parse, a missing partial); <see langword="null"/> ignores them.</param>
    /// <param name="clock">The clock of the ages and the time zone of the dates; <see cref="TimeProvider.System"/> when <see langword="null"/>.</param>
    public FhirRenderer(IReadOnlyList<ITemplateSource> layers, Action<string>? onProblem = null, TimeProvider? clock = null)
    {
        _onProblem = onProblem;
        var texts = layers.OfType<ITemplateTexts>().ToList();
        _texts = texts;
        FhirFilters.Register(_options.Filters, clock ?? TimeProvider.System, texts);
        var parser = new FhirTemplateParser(ApplyAsync);
        _options.Filters.AddFilter("json", (input, _, _) => ValueTask.FromResult<FluidValue>(new StringValue(ToJson(input), false)));
        _options.Filters.AddFilter("resolve", (input, _, context) => ValueTask.FromResult(Resolve(input, context)));
        // Whether the server said that the resource of a Reference does not exist (ResourcePool.Missing)
        _options.Filters.AddFilter("missing", (input, _, context) => ValueTask.FromResult<FluidValue>(BooleanValue.Create(IsMissing(input, context))));
        // A static text in the language of the interface, as i18next: the key is the English text, {{name}} takes the named argument, "count" picks the plural
        // form (key_one, key_few, key_many, key_other); the key itself when there is no translation
        _options.Filters.AddFilter("t", (input, arguments, _) => ValueTask.FromResult<FluidValue>(new StringValue(TemplateText.Translate(texts, input.ToStringValue(), Named(arguments)))));
        // Whether the page shows the resource of a Reference above the list (TileContext.Shown): a row leaves it out
        _options.Filters.AddFilter("in_context", (input, _, context) => ValueTask.FromResult<FluidValue>(BooleanValue.Create(InContext(input, context))));
        // The display of a Coding: its own, else the one a terminology server gave (the pool carries them); nil when there is none (the code is not a display)
        _options.Filters.AddFilter("coding_display", (input, _, context) => ValueTask.FromResult(CodingDisplay(input, context)));
        _library = new TemplateLibrary(layers, parser, onProblem);
        _options.FileProvider = new LibraryFileProvider(_library);
    }

    /// <summary>
    /// Checks the syntax of a template.
    /// </summary>
    /// <param name="text">The Liquid text of a template.</param>
    /// <returns>The first syntax error with its line and column, or <see langword="null"/> when the template parses.</returns>
    /// <example>
    /// <code>
    /// FhirRenderer.Validate("{% if self.active %}yes");   // an error: the if is not closed
    /// </code>
    /// </example>
    public static string? Validate(string text)
    {
        var parser = new FhirTemplateParser((_, _, _, _, _) => ValueTask.CompletedTask);
        return parser.TryParse(text, out _, out var error) ? null : error;
    }

    /// <summary>
    /// Renders a resource of the SDK.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <param name="mode">A chain of modes, the strongest first (<c>tile,family-first</c>): the templates <c>key@mode</c> are preferred to the plain ones for the
    /// whole document, until <c>{% apply x mode: "..." %}</c> changes it (<c>#current</c> in that mode stands for the chain in force).</param>
    /// <returns>The HTML, not yet sanitized.</returns>
    public Task<string> RenderAsync(Base resource, string? mode = null)
    {
        return RenderAsync(ToTyped(resource), mode, HtmlEncoder.Default, null);
    }

    /// <summary>
    /// Renders a resource as the JSON text the server sent.
    /// </summary>
    /// <param name="json">The resource as JSON.</param>
    /// <param name="mode">A chain of modes, the strongest first (the page of a resource uses <c>view</c>).</param>
    /// <param name="model">The model of the release of the resource; R4 when <see langword="null"/>.</param>
    /// <param name="pool">The other resources the templates may reach through a Reference.</param>
    /// <returns>The HTML; it is not trusted, the caller passes it through <see cref="FxHtml.Sanitize"/>.</returns>
    public Task<string> RenderAsync(string json, string? mode = null, ModelInspector? model = null, ResourcePool? pool = null)
    {
        return RenderAsync(ToTyped(json, model ?? ModelInfo.ModelInspector), mode, HtmlEncoder.Default, pool);
    }

    /// <summary>
    /// Renders the page of a resource with the safety net: whatever the templates did not show is not lost.
    /// </summary>
    /// <remarks>
    /// The same as <see cref="RenderAsync(string, string?, ModelInspector?, ResourcePool?)"/>, and the elements of <c>modifierExtension</c> are shown at the top,
    /// a notice says how many elements are not shown, and "Other data" at the end lists them. The templates cannot switch it off.
    /// </remarks>
    /// <param name="json">The resource as JSON.</param>
    /// <param name="mode">A chain of modes, the strongest first (<c>view</c>).</param>
    /// <param name="model">The model of the release of the resource; R4 when <see langword="null"/>.</param>
    /// <param name="pool">The other resources the templates may reach through a Reference.</param>
    /// <returns>The HTML of the page; it is not trusted, the caller passes it through <see cref="FxHtml.Sanitize"/>.</returns>
    /// <example>
    /// <code>
    /// var page = FxHtml.Sanitize(await renderer.RenderViewAsync(conditionJson, "view"), link =&gt; "/server/1/" + link[FxHtml.FhirScheme.Length..]);
    /// </code>
    /// </example>
    public async Task<string> RenderViewAsync(string json, string? mode = null, ModelInspector? model = null, ResourcePool? pool = null)
    {
        var root = ToTyped(json, model ?? ModelInfo.ModelInspector);
        var tracker = new RenderTracker();
        var html = await RenderAsync(root, mode, HtmlEncoder.Default, pool, tracker);
        SafetyNetBlocks net;
        try
        {
            net = SafetyNet.Build(root, tracker);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _onProblem?.Invoke($"The check for data that is not shown failed: {ex.Message}");
            net = SafetyNet.Failed();
        }

        return net.Top + html + net.Bottom;
    }

    /// <summary>
    /// Renders the row of a list for a resource of the SDK.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <param name="mode">More modes after <see cref="TileModel.Mode"/>, the strongest first.</param>
    /// <param name="context">What the page shows above the list and the options of the rows; <see cref="TileContext.None"/> when <see langword="null"/>.</param>
    /// <returns>The row: the template of the mode <see cref="TileModel.Mode"/> writes a daisyUI list row; a type without such a template shows the id and the
    /// type, a template that fails shows the error in the row.</returns>
    public Task<TileModel> RenderTileAsync(Base resource, string? mode = null, TileContext? context = null)
    {
        return RenderTileAsync(ToTyped(resource), mode, null, context);
    }

    /// <summary>
    /// Renders a resource through a template of named slots (a partial such as <c>lab-row</c>, which fills <c>{% slot "value" %}</c>...): the cells of a
    /// row of a table of a component.
    /// </summary>
    /// <param name="json">The resource as JSON.</param>
    /// <param name="template">The key of the template (a partial of the library, overridable like any template); <c>self</c> is the resource.</param>
    /// <param name="model">The model of the release of the resource; R4 when <see langword="null"/>.</param>
    /// <param name="pool">The other resources the template may reach through a Reference (see <see cref="CreatePool"/>).</param>
    /// <param name="context">What the page shows above and the options (<c>fhir_options.decimals</c> of the format of the section).</param>
    /// <returns>The slots by name, each sanitized like a tile; empty when the template is missing or failed (the problem is reported).</returns>
    /// <example>
    /// <code>
    /// var cells = await renderer.RenderSlotsAsync(observationJson, "lab-row", context: new TileContext([], TileOptions.Default, format));
    /// var value = cells.GetValueOrDefault("value");
    /// </code>
    /// </example>
    public async Task<IReadOnlyDictionary<string, string>> RenderSlotsAsync(string json, string template, ModelInspector? model = null, ResourcePool? pool = null, TileContext? context = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        var slots = new FrameSlots();
        var problems = new List<string>();
        try
        {
            await RenderAsync(ToTyped(json, model ?? ModelInfo.ModelInspector), null, HtmlEncoder.Default, pool, null, problems, context, slots, template);
        }
        catch (InvalidOperationException ex)
        {
            problems.Add(ex.Message);
        }

        foreach (var problem in problems)
        {
            _onProblem?.Invoke(problem);
        }

        return problems.Count > 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : slots.Html.ToDictionary(slot => slot.Key, slot => FxHtml.SanitizeTile(slot.Value).Trim(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Renders the row of a list for a resource as the JSON text the server sent: no POCO, any release.
    /// </summary>
    /// <param name="json">The resource as JSON.</param>
    /// <param name="mode">More modes after <see cref="TileModel.Mode"/>, the strongest first.</param>
    /// <param name="model">The model of the release of the resource (the SDK of that release); R4 when <see langword="null"/>. Elements the model does not know
    /// are not shown.</param>
    /// <param name="pool">The other resources the template may reach through a Reference (see <see cref="CreatePool"/>).</param>
    /// <param name="context">What the page shows above the list and the options of the rows.</param>
    /// <returns>The row, sanitized.</returns>
    /// <example>
    /// <code>
    /// var row = await renderer.RenderTileAsync(observationJson, pool: pool, context: new TileContext(["Patient/p1"], TileOptions.Default));
    /// </code>
    /// </example>
    public Task<TileModel> RenderTileAsync(string json, string? mode = null, ModelInspector? model = null, ResourcePool? pool = null, TileContext? context = null)
    {
        return RenderTileAsync(ToTyped(json, model ?? ModelInfo.ModelInspector), mode, pool, context);
    }

    /// <summary>
    /// Creates the pool of the resources of a Bundle, read with the model of their release.
    /// </summary>
    /// <param name="entries">The resources and their <c>fullUrl</c>; a resource that cannot be read is skipped.</param>
    /// <param name="model">The model of the release; R4 when <see langword="null"/>.</param>
    /// <returns>The pool.</returns>
    public static ResourcePool CreatePool(IEnumerable<PoolEntry> entries, ModelInspector? model = null)
    {
        var pool = new ResourcePool();
        pool.AddAll(entries, model);
        return pool;
    }

    internal static void AddEntries(ResourcePool pool, IEnumerable<PoolEntry> entries, ModelInspector? model)
    {
        foreach (var entry in entries)
        {
            try
            {
                pool.Add(ToTyped(entry.Json, model ?? ModelInfo.ModelInspector), entry.FullUrl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // a resource that cannot be read is not reachable: the templates see an unresolved reference
            }
        }
    }

    /// <summary>
    /// Whether the layers have a template for a type in a mode; a layer that loads its templates on demand loads it.
    /// </summary>
    /// <param name="resourceType">The type (<c>Patient</c>).</param>
    /// <param name="mode">The mode (<c>tile</c>); the plain templates of the elements do not count.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns><see langword="true"/> when <c>resourceType@mode</c> exists in a layer.</returns>
    /// <example>
    /// <code>
    /// var hasPage = await renderer.HasTemplateAsync("Observation", "view");
    /// </code>
    /// </example>
    public async ValueTask<bool> HasTemplateAsync(string resourceType, string mode, CancellationToken cancellationToken = default)
    {
        return await _library.FindAsync(FhirNode.ModeKey(resourceType, mode), cancellationToken) is not null;
    }

    /// <summary>
    /// What the template of a type asks the server to send with the resources of a search.
    /// </summary>
    /// <param name="resourceType">The type (<c>Encounter</c>).</param>
    /// <param name="mode">A chain of modes: the template of the first mode that has one; with no mode, the plain template.</param>
    /// <returns>The <c>_include</c> and <c>_revinclude</c> the template declares, or <see cref="FhirNeeds.None"/>.</returns>
    /// <param name="cancellationToken">Cancels the load of the template.</param>
    /// <example>
    /// <code>
    /// var needs = await renderer.NeedsForAsync("Encounter", "tile");   // _include=Encounter:subject
    /// </code>
    /// </example>
    public async ValueTask<FhirNeeds> NeedsForAsync(string resourceType, string? mode = null, CancellationToken cancellationToken = default)
    {
        var modes = FhirNode.ModesOf(mode);
        var keys = modes.Count == 0 ? [resourceType] : modes.Select(name => FhirNode.ModeKey(resourceType, name));
        foreach (var key in keys)
        {
            if (await _library.FindAsync(key, cancellationToken) is { } text)
            {
                return TemplateNeeds.Of(text);
            }
        }

        return FhirNeeds.None;
    }

    private static FluidValue CodingDisplay(FluidValue input, TemplateContext context)
    {
        if (input is not FhirNodeValue node)
        {
            return NilValue.Instance;
        }

        string? Child(string name)
        {
            return node.Node.Children(name) is [{ } first, ..] ? first.ValueText() : null;
        }

        if (Child("display") is { Length: > 0 } own)
        {
            return new StringValue(own);
        }

        var found = context.AmbientValues.TryGetValue(PoolKey, out var pool) && pool is ResourcePool { Terminology: { } map } ? map.Find(Child("system"), Child("code")) : null;
        return found is null ? NilValue.Instance : new StringValue(found);
    }

    private static bool InContext(FluidValue input, TemplateContext context)
    {
        var reference = input switch
        {
            FhirNodeValue node => node.Node.Children("reference") is [{ } first, ..] ? first.ValueText() : null,
            _ when input.IsNil() => null,
            _ => input.ToStringValue()
        };
        return ResourcePool.KeyOf(reference) is { } key && context.AmbientValues.TryGetValue(ContextKey, out var value) && value is TileContext tile
               && tile.Shown.Any(shown => string.Equals(ResourcePool.KeyOf(shown), key, StringComparison.Ordinal));
    }

    private static FluidValue Resolve(FluidValue input, TemplateContext context)
    {
        if (input is FhirNodeValue node && context.AmbientValues.TryGetValue(PoolKey, out var pool) && pool is ResourcePool resources)
        {
            var reference = node.Node.Children("reference") is [{ } first, ..] ? first.ValueText() : null;
            if (resources.Find(reference) is { } found)
            {
                return new FhirNodeValue(resources.NodeOf(found));
            }

            resources.Missed(reference);
        }

        return NilValue.Instance;
    }

    private static bool IsMissing(FluidValue input, TemplateContext context)
    {
        var reference = input is FhirNodeValue node && node.Node.Children("reference") is [{ } first, ..] ? first.ValueText() : null;
        return ResourcePool.KeyOf(reference) is { } key && context.AmbientValues.TryGetValue(PoolKey, out var pool) && pool is ResourcePool resources && resources.Missing.Contains(key);
    }

    private static Dictionary<string, FluidValue> Named(FilterArguments arguments)
    {
        return arguments.Names.ToDictionary(name => name, name => arguments[name], StringComparer.Ordinal);
    }

    private async Task<TileModel> RenderTileAsync(ITypedElement root, string? mode, ResourcePool? pool, TileContext? tileContext)
    {
        var type = root.InstanceType ?? root.Name;
        var id = root.Children("id").FirstOrDefault()?.Value as string;
        var plain = string.IsNullOrEmpty(id) ? TileModel.Plain(type, null) : TileModel.Plain(id, type);
        if (await _library.FindAsync(FhirNode.ModeKey(type, TileModel.Mode)) is null)
        {
            return plain;
        }

        try
        {
            var problems = new List<string>();
            var slots = new FrameSlots();
            // what the template writes outside its slots is dropped
            await RenderAsync(root, string.Join(',', [TileModel.Mode, .. FhirNode.ModesOf(mode)]), HtmlEncoder.Default, pool, null, problems, tileContext, slots);
            if (problems.Count > 0)
            {
                return TileModel.Error(string.IsNullOrEmpty(id) ? type : id, string.Join("; ", problems));
            }

            var tile = TileOf(slots, (tileContext ?? TileContext.None).Options);
            if (tile is null)
            {
                // the template wrote no row for this data (a condition of its own): the id and the type, no error
                _onProblem?.Invoke($"Template '{type}@{TileModel.Mode}' wrote no row");
                return plain;
            }

            return tile;
        }
        catch (InvalidOperationException ex)
        {
            _onProblem?.Invoke($"Template '{type}@{TileModel.Mode}' failed: {ex.Message}");
            return TileModel.Error(string.IsNullOrEmpty(id) ? type : id, ex.Message);
        }
    }

    // Each slot sanitized on its own; no row when the template filled no title
    private static TileModel? TileOf(FrameSlots slots, TileOptions options)
    {
        var title = FxHtml.SanitizeTile(slots[FrameSlots.Title]).Trim();
        if (FxHtml.PlainText(title).Length == 0)
        {
            return null;
        }

        return new TileModel(title, Slot(FrameSlots.Subtitle), Slot(FrameSlots.Trailing), Slot(FrameSlots.Status), Slot(FrameSlots.Patient), Slot(FrameSlots.Encounter),
                             Slot(FrameSlots.Age), options);

        string Slot(string name)
        {
            var html = FxHtml.SanitizeTile(slots[name]).Trim();
            return FxHtml.PlainText(html).Length == 0 ? string.Empty : html;
        }
    }

    private static string ToJson(FluidValue input)
    {
        return input.IsNil() ? "null" : JsonSerializer.Serialize(input.ToStringValue());
    }

    private static ITypedElement ToTyped(Base resource)
    {
#pragma warning disable SDK0001 // the only way to get typed elements from a POCO in this SDK version
        return resource.ToTypedElement(ModelInfo.ModelInspector);
#pragma warning restore SDK0001
    }

    // Lenient: a release that is not the one of the model, or an element it does not know, is passed through, not an error; a value that is not of its
    // type (a date with a time where only a date is allowed) is kept as the server sent it instead of failing the whole template
    // A resource is read once per string: the pool of a page, its rows and the blocks of a summary all pass the same JSON again
    private static ITypedElement ToTyped(string json, ModelInspector model)
    {
        if (s_typed.TryGetValue(json, out var cached) && ReferenceEquals(cached.Model, model))
        {
            return cached.Element;
        }

        var typed = FhirJsonNode.Parse(json).ToTypedElement(model, settings: new TypedElementSettings { ErrorMode = TypedElementSettings.TypeErrorMode.Passthrough });
        if (typed is Hl7.Fhir.Utility.IExceptionSource source)
        {
            source.ExceptionHandler = (_, _) => { };
        }

        s_typed.AddOrUpdate(json, new TypedResource(model, typed));
        return typed;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string, TypedResource> s_typed = new();

    private sealed record TypedResource(ModelInspector Model, ITypedElement Element);

    // problems: when the caller passes the list, it shows the errors of the templates itself; otherwise they are written before the text (HTML only)
    // tileContext: every template sees fhir_context (the Type/id the page shows above) and fhir_options (show_age, show_status), the defaults when not given
    private async Task<string> RenderAsync(ITypedElement root, string? mode, TextEncoder encoder, ResourcePool? pool, RenderTracker? tracker = null, List<string>? problems = null,
                                           TileContext? tileContext = null, FrameSlots? slots = null, string? templateKey = null)
    {
        foreach (var texts in _texts)
        {
            await texts.PrepareAsync(System.Globalization.CultureInfo.CurrentUICulture);
        }

        using var writer = new StringWriter();
        var context = new TemplateContext(_options);
        tileContext ??= TileContext.None;
        context.AmbientValues[ContextKey] = tileContext;
        context.SetValue("fhir_context", new ArrayValue([.. tileContext.Shown.Select(shown => new StringValue(shown))]));
        context.SetValue("fhir_options", FluidValue.Create(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["show_age"] = tileContext.Options.ShowAge,
            ["show_status"] = tileContext.Options.ShowStatus,
            ["decimals"] = tileContext.Format?.Decimals
        }, _options));
        var own = problems is null;
        problems ??= [];
        context.AmbientValues[ProblemsKey] = problems;
        if (pool is not null)
        {
            context.AmbientValues[PoolKey] = pool;
        }

        if (tracker is not null)
        {
            context.AmbientValues[RenderTracker.Key] = tracker;
        }

        if (slots is not null)
        {
            context.AmbientValues[FrameSlots.Key] = slots;
        }

        if (templateKey is null)
        {
            await ApplyAsync(new FhirNode(root, tracker is not null), mode, writer, encoder, context);
        }
        else
        {
            await RenderKeyAsync(new FhirNode(root, false), templateKey, encoder, context);
        }
        return own && !ReferenceEquals(encoder, NullEncoder.Default) ? ProblemBlock(problems) + writer.ToString() : writer.ToString();
    }

    // A template that is broken is not hidden: the page says which one and why (the text of the error is encoded)
    private static string ProblemBlock(IReadOnlyList<string> problems)
    {
        if (problems.Count == 0)
        {
            return string.Empty;
        }

        var text = new System.Text.StringBuilder("<div class=\"fx-notice fx-notice-danger\"><div class=\"fx-notice-title\">Template error</div>");
        foreach (var problem in problems)
        {
            text.Append("<div>").Append(HtmlEncoder.Default.Encode(problem)).Append("</div>");
        }

        return text.Append("</div>").ToString();
    }

    private void Report(TemplateContext context, string problem)
    {
        if (context.AmbientValues.TryGetValue(ProblemsKey, out var list) && list is List<string> problems && !problems.Contains(problem))
        {
            problems.Add(problem);
        }

        _onProblem?.Invoke(problem);
    }

    // A named template with the resource as self (a row of a table into its slots): what it writes outside the slots is dropped
    private async ValueTask RenderKeyAsync(FhirNode node, string key, TextEncoder encoder, TemplateContext context)
    {
        var resolved = await _library.ResolveKeyedAsync([key]) ?? throw new InvalidOperationException($"No template '{key}'");
        foreach (var skipped in resolved.Skipped)
        {
            Report(context, skipped);
        }

        context.EnterChildScope();
        try
        {
            context.SetValue("self", new FhirNodeValue(node));
            context.SetValue("fhir_type", node.TypeName);
            context.SetValue("fhir_path", node.Path);
            context.SetValue("fhir_is_primitive", node.IsPrimitive);
            await resolved.Template.RenderAsync(TextWriter.Null, encoder, context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Report(context, $"Template '{key}' failed: {ex.Message}");
        }
        finally
        {
            context.ReleaseScope();
        }
    }

    private async ValueTask ApplyAsync(FhirNode node, string? mode, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        var depth = context.AmbientValues.TryGetValue(DepthKey, out var d) ? (int)d : 0;
        if (depth >= MaxDepth)
        {
            throw new InvalidOperationException($"Templates are nested deeper than {MaxDepth} levels at {node.Path}");
        }

        var inherited = context.AmbientValues.TryGetValue(ModeKey, out var m) ? m as string : null;
        var effective = FhirNode.ExpandMode(mode, inherited);
        var resolved = await _library.ResolveKeyedAsync(node.TemplateKeys(effective))
                       ?? throw new InvalidOperationException($"No template for {node.Path} ({node.TypeName})");

        foreach (var skipped in resolved.Skipped)
        {
            if (context.AmbientValues.TryGetValue(ProblemsKey, out var list) && list is List<string> problems && !problems.Contains(skipped))
            {
                problems.Add(skipped);
            }
        }

        if (node.Tracked && context.AmbientValues.TryGetValue(RenderTracker.Key, out var tracked) && tracked is RenderTracker tracker)
        {
            tracker.Applied(node.Element.Location, FhirNode.IsFallbackKey(resolved.Key));
        }

        context.AmbientValues[DepthKey] = depth + 1;
        context.AmbientValues[ModeKey] = effective;
        // A banner is a frame: its template fills slots, BannerFrame writes the header (what the template writes outside the slots is dropped)
        var banner = resolved.Key.EndsWith(TemplateKey.ModeSeparator + IResourceViews.BannerMode, StringComparison.Ordinal) ? new FrameSlots() : null;
        context.AmbientValues.TryGetValue(FrameSlots.Key, out var outerSlots);
        if (banner is not null)
        {
            context.AmbientValues[FrameSlots.Key] = banner;
        }

        context.EnterChildScope();
        try
        {
            context.SetValue("self", new FhirNodeValue(node));
            context.SetValue("fhir_type", node.TypeName);
            context.SetValue("fhir_path", node.Path);
            context.SetValue("fhir_is_primitive", node.IsPrimitive);
            await resolved.Template.RenderAsync(banner is null ? writer : TextWriter.Null, encoder, context);
            if (banner is not null)
            {
                var id = node.Element.Children("id").FirstOrDefault()?.Value as string;
                await writer.WriteAsync(BannerFrame.Compose(banner, node.TypeName, id, context.GetValue("banner_link").ToBooleanValue()));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Report(context, $"Template '{resolved.Key}' failed: {ex.Message}");
        }
        finally
        {
            context.ReleaseScope();
            if (banner is not null)
            {
                context.AmbientValues[FrameSlots.Key] = outerSlots;
            }

            context.AmbientValues[DepthKey] = depth;
            context.AmbientValues[ModeKey] = inherited;
        }
    }
}
