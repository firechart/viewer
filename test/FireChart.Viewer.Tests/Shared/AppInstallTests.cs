using Bunit;
using Flox.Shared.Components;
using Microsoft.AspNetCore.Components;

namespace FireChart.Viewer.Tests.Shared;

// The content of AppInstall is there only while the browser offers to install the application, and its action starts the installation
[TestFixture]
public class AppInstallTests
{
    private const string Module = "./_content/Flox.Shared/Components/AppInstall.razor.js";

    [Test]
    public async Task TheContent_IsShownWhileTheBrowserOffers_AndItsActionInstalls()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var module = context.JSInterop.SetupModule(Module);
        module.Mode = JSRuntimeMode.Loose;

        var install = context.Render<AppInstall>(parameters => parameters.Add(component => component.ChildContent,
                                                                                (Func<Task> action) => (RenderFragment)(builder =>
                                                                                {
                                                                                    builder.OpenElement(0, "button");
                                                                                    builder.AddAttribute(1, "onclick", EventCallback.Factory.Create(this, action));
                                                                                    builder.CloseElement();
                                                                                })));
        Assert.That(install.FindAll("button"), Is.Empty);

        await install.InvokeAsync(() => install.Instance.SetAvailable(true));
        await install.Find("button").ClickAsync(new());
        Assert.That(module.Invocations.Select(invocation => invocation.Identifier), Does.Contain("install"));

        await install.InvokeAsync(() => install.Instance.SetAvailable(false));
        Assert.That(install.FindAll("button"), Is.Empty);
    }
}
