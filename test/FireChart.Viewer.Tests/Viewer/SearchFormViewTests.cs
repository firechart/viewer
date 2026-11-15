using Bunit;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Core.Search;
using FireChart.Viewer.SearchForms;
using Radzen;

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
        context.Services.AddRadzenComponents();
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
        context.Services.AddRadzenComponents();
        var form = new SearchFormDefinition { Name = "plain" };
        form.Fields.Add(new SearchFormField { QueryName = "family", Control = SearchFieldControl.Text });

        var view = context.Render<SearchFormView>(parameters => parameters.Add(v => v.Definition, form));

        Assert.That(view.FindAll("button.form-show-all"), Is.Empty);
    }

    // The Enter that confirms a candidate of an input method (Chinese, Japanese, Korean) does not apply the search; the next Enter does
    [Test]
    public void TheEnterOfAnInputMethod_DoesNotReachTheSearchBox_TheNextEnterDoes()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddRadzenComponents();
        var keys = new List<bool>();
        var box = context.Render<Flox.Shared.Components.Search>(parameters => parameters.Add(search => search.OnKeyDown, (Microsoft.AspNetCore.Components.Web.KeyboardEventArgs args) => keys.Add(args.IsComposing)));

        box.Find("input").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter", IsComposing = true });
        box.Find("input").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Assert.That(keys, Is.EqualTo(new[] { false }));
    }
}
