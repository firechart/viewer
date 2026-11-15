using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Playwright;

namespace Flox.Sqlite.BrowserTests;

// One server of the static files and one browser for the run. The browser is Chrome or Edge as installed (FLOX_BROWSER_CHANNEL: chrome, msedge;
// chrome by default), so Playwright downloads none. A test takes a context of its own: its tabs share one origin storage, the next test starts empty
[SetUpFixture]
public sealed class BrowserHarness
{
    private static WebApplication? s_server;
    private static IPlaywright? s_playwright;
    private static IBrowser? s_browser;

    public static Uri Address { get; private set; } = null!;

    public static IBrowser Browser => s_browser ?? throw new InvalidOperationException("The browser is not started.");

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        s_server = builder.Build();
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".mjs"] = "text/javascript";
        types.Mappings[".wasm"] = "application/wasm";
        s_server.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "wwwroot")),
            ContentTypeProvider = types,
            ServeUnknownFileTypes = false
        });
        await s_server.StartAsync();
        var address = s_server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        Address = new Uri(address.TrimEnd('/') + "/");

        s_playwright = await Playwright.CreateAsync();
        s_browser = await s_playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = Environment.GetEnvironmentVariable("FLOX_BROWSER_CHANNEL") ?? "chrome",
            Headless = true
        });
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        if (s_browser is not null)
        {
            await s_browser.DisposeAsync();
        }

        s_playwright?.Dispose();
        if (s_server is not null)
        {
            await s_server.DisposeAsync();
        }
    }

    // A tab of the context with the harness loaded and the pool initialized; the first tab of a pool to call this leads it
    public static async Task<IPage> OpenTabAsync(IBrowserContext context, string pool)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(Address, "harness.html").AbsoluteUri);
        await page.WaitForFunctionAsync("() => window.harnessReady === true");
        await page.EvaluateAsync("pool => window.harness.init(pool)", pool);
        return page;
    }
}
