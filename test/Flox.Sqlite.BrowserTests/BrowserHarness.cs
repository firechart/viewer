using System.Buffers.Binary;
using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
    private static TaskCompletionSource s_downloadStarted = new();
    private static TaskCompletionSource s_downloadReleased = new();

    public const int HeldUserVersion = 7;

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
        s_server.MapGet("/held.sqlite.gz", SendHeldBaseAsync);
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

    // The download of /held.sqlite.gz stops after its first half until ReleaseDownload
    public static Task HoldDownload()
    {
        s_downloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        s_downloadReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return s_downloadStarted.Task;
    }

    public static void ReleaseDownload()
    {
        s_downloadReleased.TrySetResult();
    }

    // An empty base of one page of 512 bytes with the user_version
    public static byte[] EmptyBase(int userVersion)
    {
        var bytes = new byte[512];
        "SQLite format 3\0"u8.CopyTo(bytes);
        bytes[16] = 2;
        bytes[18] = 1;
        bytes[19] = 1;
        bytes[21] = 64;
        bytes[22] = 32;
        bytes[23] = 32;
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(24), 1);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(28), 1);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(44), 4);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(56), 1);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(60), userVersion);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(92), 1);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(96), 3046000);
        bytes[100] = 0x0D;
        bytes[105] = 2;
        return bytes;
    }

    private static async Task SendHeldBaseAsync(HttpContext context)
    {
        var bytes = EmptyBase(HeldUserVersion);
        context.Response.ContentType = "application/gzip";
        await using var gzip = new GZipStream(context.Response.Body, CompressionLevel.Fastest, true);
        await gzip.WriteAsync(bytes.AsMemory(0, 256));
        await gzip.FlushAsync();
        await context.Response.Body.FlushAsync();
        s_downloadStarted.TrySetResult();
        await s_downloadReleased.Task.WaitAsync(context.RequestAborted);
        await gzip.WriteAsync(bytes.AsMemory(256));
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
