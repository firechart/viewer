using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Bunit;
using Flox.Shared.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Radzen;

namespace FireChart.Viewer.Tests.Shared;

// The fields of the forms of Flox.Shared: an error is tied to its input for a screen reader, a field is not called invalid while it is first
// typed, the first invalid field takes the focus after a failed submit; a load is told in a status line
[TestFixture]
public class FormAccessibilityTests
{
    private sealed class Server
    {
        [Required]
        [Url]
        public string BaseUrl { get; set; } = string.Empty;
    }

    private sealed record Form(IRenderedComponent<EditForm> Component, EditContext Context, FormFieldFocus Focus);

    private static Form RenderForm(BunitContext context, Server model)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddRadzenComponents();
        var focus = new FormFieldFocus();
        EditContext? editContext = null;
        var receiver = new object();
        RenderFragment field = builder =>
        {
            builder.OpenComponent<FormTextField>(0);
            builder.AddAttribute(1, nameof(FormTextField.Text), "Base URL");
            builder.AddAttribute(2, nameof(FormTextField.Name), "BaseUrl");
            builder.AddAttribute(3, nameof(FormTextField.Value), model.BaseUrl);
            builder.AddAttribute(4, nameof(FormTextField.ValueChanged), EventCallback.Factory.Create<string>(receiver, value => model.BaseUrl = value));
            builder.AddAttribute(5, nameof(FormTextField.ValueExpression), (Expression<Func<string>>)(() => model.BaseUrl));
            builder.AddAttribute(6, nameof(FormTextField.Required), true);
            builder.AddAttribute(7, nameof(FormTextField.Kind), FormTextKind.Url);
            builder.CloseComponent();
        };
        var component = context.Render<EditForm>(parameters => parameters.Add(form => form.Model, model).Add(form => form.ChildContent, edit => builder =>
        {
            editContext = edit;
            builder.OpenComponent<DataAnnotationsValidator>(0);
            builder.CloseComponent();
            builder.OpenComponent<CascadingValue<FormFieldFocus>>(1);
            builder.AddAttribute(2, "Value", focus);
            builder.AddAttribute(3, "IsFixed", true);
            builder.AddAttribute(4, "ChildContent", field);
            builder.CloseComponent();
        }));
        return new Form(component, editContext!, focus);
    }

    [Test]
    public void AFieldIsValidatedWhenLeft_ThenOnEveryKeyWhileInvalid_AndItsMessageDescribesTheInput()
    {
        using var context = new BunitContext();
        var form = RenderForm(context, new Server());

        var input = form.Component.Find("input");
        Assert.Multiple(() =>
        {
            Assert.That(input.GetAttribute("aria-invalid"), Is.EqualTo("false"), "valid until told otherwise, not unset");
            Assert.That(input.HasAttribute("aria-describedby"), Is.False);
            Assert.That(input.GetAttribute("aria-required"), Is.EqualTo("true"));
            Assert.That(input.GetAttribute("inputmode"), Is.EqualTo("url"));
            Assert.That(input.GetAttribute("spellcheck"), Is.EqualTo("false"));
            Assert.That(input.GetAttribute("autocapitalize"), Is.EqualTo("none"));
            Assert.That(form.Component.Find("label span[aria-hidden=true]").TextContent.Trim(), Is.EqualTo("*"));
            Assert.Throws<MissingEventHandlerException>(() => input.Input("h"), "the first typing is not validated key by key");
        });

        form.Component.Find("input").Change("h");

        input = form.Component.Find("input");
        var messages = form.Component.Find("#" + input.GetAttribute("aria-describedby"));
        Assert.Multiple(() =>
        {
            Assert.That(input.GetAttribute("aria-invalid"), Is.EqualTo("true"));
            Assert.That(messages.TextContent, Is.Not.Empty, "the input is described by its message");
        });

        form.Component.Find("input").Input("https://example.org");

        input = form.Component.Find("input");
        Assert.Multiple(() =>
        {
            Assert.That(input.GetAttribute("aria-invalid"), Is.EqualTo("false"), "an invalid field is checked on every key");
            Assert.That(input.HasAttribute("aria-describedby"), Is.False);
        });
    }

    [Test]
    public async Task AfterAFailedSubmit_TheFirstInvalidFieldTakesTheFocus()
    {
        using var context = new BunitContext();
        var form = RenderForm(context, new Server());

        var before = await form.Component.InvokeAsync(() => form.Focus.FocusFirstInvalidAsync(form.Context));
        await form.Component.InvokeAsync(() => form.Context.Validate());

        var focused = await form.Component.InvokeAsync(() => form.Focus.FocusFirstInvalidAsync(form.Context));

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.False, "nothing is invalid before a validation");
            Assert.That(focused, Is.True);
            Assert.That(context.JSInterop.VerifyFocusAsyncInvoke().Arguments[0], Is.Not.Null);
        });
    }

    [Test]
    public async Task ALoadThatLasts_IsToldInAStatusLineThatIsAlwaysThere()
    {
        using var context = new BunitContext();

        var indicator = context.Render<BusyIndicator>(parameters => parameters.Add(component => component.Visible, true));

        Assert.That(indicator.Find("[role=status]").TextContent, Is.Empty, "a quick load is not told");
        await Task.Delay(400);
        indicator.WaitForAssertion(() => Assert.That(indicator.Find("[role=status]").TextContent, Is.EqualTo(Flox.Resources.Strings.Caption_Loading)));

        indicator.Render(parameters => parameters.Add(component => component.Visible, false));
        Assert.That(indicator.Find("[role=status]").TextContent, Is.Empty);
    }
}
