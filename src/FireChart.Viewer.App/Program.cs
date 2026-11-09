using FireChart.Viewer.App;
using FireChart.Viewer.Core.Fhir;
using Flox.Shared.Pages.BackgroundTask;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var bootstrapper = new ViewerAppBootstrapper(builder.Services, builder.Configuration, builder.Logging, builder.HostEnvironment.BaseAddress, builder.HostEnvironment.Environment);
bootstrapper.Configure();

// After the bootstrapper: the last registration wins over the default loader of the Viewer, which loads nothing
builder.Services.AddSingleton<IReleaseAssemblyLoader, WasmReleaseAssemblyLoader>();
// The pages of the background tasks of Flox.Shared need the full client (events, OData): the Viewer has none, and the validation of the
// services in Development would stop the start
builder.Services.RemoveAll<BackgroundTasksState>();
builder.Services.RemoveAll<BackgroundTaskViewState>();

await builder.Build().RunAsync();
