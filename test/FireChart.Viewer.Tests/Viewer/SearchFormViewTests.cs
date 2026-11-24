using Flox.Shared.Components;
using Bunit;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Core.Search;
using FireChart.Viewer.SearchForms;

namespace FireChart.Viewer.Tests.Viewer;

// The search dialog shows what the author left in the form, and never hides what narrows the list
[TestFixture]
public class SearchFormViewTests
{
    private static SearchFormDefinition Form()
    {
        var form = new SearchFormDefinition { Name = "test" };
        form.Groups.Add(new SearchFormGroup { Code = "spare", Order = 1, Hidden = true });
        form.Groups[0].Caption.SetDefault("Spare");
        foreach (var (name, order, hidden, group) in new[] { ("family", 1, false, (string?)null), ("gender", 2, true, null), ("phone", 3, false, "spare") })
        {
            var field = new SearchFormField { QueryName = name, Order = order, Hidden = hidden, GroupCode = group, Control = SearchFieldControl.Text };
            field.Label.SetDefault(name);
            form.Fields.Add(field);
        }

        return form;
    }

    private static IRenderedComponent<SearchFormView> Render(BunitContext context, IReadOnlyDictionary<string, SearchFieldValue>? values = null)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        return context.Render<SearchFormView>(parameters => parameters.Add(view => view.Definition, Form()).Add(view => view.Values, values));
    }

    private static string[] Labels(IRenderedComponent<SearchFormView> view)
    {
        return [.. view.FindAll("label").Select(label => label.TextContent.Trim()).Where(text => text.Length > 0)];
    }

    [Test]
    public void TheHiddenFieldsAndGroups_ShowOnlyWhenAllTheFieldsAreAskedFor()
    {
        using var context = new BunitContext();
        var view = Render(context);

        var shown = Labels(view);
        view.Find("button.form-show-all").Click();
        var all = Labels(view);

        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.EqualTo(new[] { "family" }));
            Assert.That(all, Is.EquivalentTo(new[] { "family", "gender", "phone" }));
            Assert.That(view.Find("button.form-show-all").TextContent, Does.Contain(FireChart.Resources.Strings.Caption_ShowFormFields));
        });
    }

    [Test]
    public void AHiddenFieldWithAValue_IsShown_EvenInAHiddenGroup()
    {
        using var context = new BunitContext();
        var values = new Dictionary<string, SearchFieldValue> { ["gender"] = new() { Text = "male" }, ["phone"] = new() { Text = "555" } };

        var view = Render(context, values);

        Assert.That(Labels(view), Is.EquivalentTo(new[] { "family", "gender", "phone" }));
    }

    [Test]
    public void AFormWithoutHiddenFields_HasNoButtonToShowThem()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        var form = new SearchFormDefinition { Name = "plain" };
        form.Fields.Add(new SearchFormField { QueryName = "family", Control = SearchFieldControl.Text });

        var view = context.Render<SearchFormView>(parameters => parameters.Add(v => v.Definition, form));

        Assert.That(view.FindAll("button.form-show-all"), Is.Empty);
    }

    // Enter in a field submits the form, which applies it as OK does; the other commands are plain buttons, so they do not submit it
    [Test]
    public void SubmittingTheForm_AppliesIt_AndOnlyOkSubmits()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        var applied = new List<SearchFormDialogResult>();
        var view = context.Render<SearchFormView>(parameters => parameters.Add(v => v.Definition, Form())
                                                                           .Add(v => v.OnApply, (SearchFormDialogResult result) => applied.Add(result)));

        view.Find("form").Submit();

        Assert.Multiple(() =>
        {
            Assert.That(applied, Has.Count.EqualTo(1));
            Assert.That(view.FindAll("form button[type=submit]"), Has.Count.EqualTo(1));
        });
    }

    // The Enter that confirms a candidate of an input method (Chinese, Japanese, Korean) does not apply the search; the next Enter does
    [Test]
    public async Task TheEnterOfAnInputMethod_DoesNotReachTheSearchBox_TheNextEnterDoes()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        var keys = new List<bool>();
        var box = context.Render<Flox.Shared.Components.Search>(parameters => parameters.Add(search => search.OnKeyDown, (Microsoft.AspNetCore.Components.Web.KeyboardEventArgs args) => keys.Add(args.IsComposing)));

        await box.Find("input").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter", IsComposing = true });
        await box.Find("input").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Assert.That(keys, Is.EqualTo(new[] { false }));
    }
}
