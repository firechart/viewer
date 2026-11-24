using Bunit;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Reports;
using FireChart.Viewer.Routing;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Tests.Reports;

// The host of the components of a list is left (the page changed) while it still asks the server for its release
[TestFixture]
public class ReportHostTests
{
    [Test]
    public async Task AHostLeftWhileItLoads_EndsQuietly()
    {
        using var context = new BunitContext();
        var types = new SlowTypes();
        context.Services.AddSingleton<IFhirServerReader>(new TestReader("{}", _ => []));
        context.Services.AddSingleton<IReleaseModels, R4Models>();
        context.Services.AddSingleton<IServerResourceTypeProvider>(types);
        context.Services.AddSingleton<IViewerAssets>(new TextAssets(new Dictionary<string, string>()));
        context.Services.AddSingleton<ITerminologyResolver, NoWords>();
        context.Services.AddSingleton<ViewerTerminology>();
        var session = new ViewerSession(new TestServer(), new FixedLocation("resources/Patient"));
        context.Render<CascadingValue<ViewerSession>>(parameters => parameters.Add(value => value.Value, session).AddChildContent<ReportHost>());

        await context.DisposeComponentsAsync();
        types.Gate.SetResult();
        await Task.Delay(100);

        var failed = context.Renderer.UnhandledException;
        Assert.That(failed.IsCompleted ? (await failed).GetType().Name : null, Is.Null);
    }

    private sealed class FixedLocation(string location) : IViewerLocationStore
    {
        public string? Location => location;

        public event EventHandler? LocationChanged
        {
            add { }
            remove { }
        }

        public void NavigateTo(string location, bool replace = false)
        {
        }

        public string HrefOf(string location)
        {
            return location;
        }

        public void Dispose()
        {
        }
    }

    private sealed class SlowTypes : IServerResourceTypeProvider
    {
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ServerResourceTypes> GetAsync(IViewerServer server, bool refresh = false, CancellationToken cancellationToken = default)
        {
            await Gate.Task;
            return new ServerResourceTypes("4.0.1", "R4", ["Patient"]);
        }
    }

    private sealed class R4Models : IReleaseModels
    {
        public Task<ModelInspector> GetAsync(string? release, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ModelInfo.ModelInspector);
        }
    }

    private sealed class NoWords : ITerminologyResolver
    {
        public event EventHandler? Resolved
        {
            add { }
            remove { }
        }

        public Task<TerminologyMap> ResolveAsync(string serverAddress, IReadOnlyCollection<CodeKey> codes, TimeSpan wait, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
