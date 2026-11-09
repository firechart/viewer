using System.Globalization;
using Flox.Core.Diagnostics;
using Flox.Shared.Errors;
using Flox.Shared.Extensions;
using Flox.Shared.Logging;
using Flox.Shared.Services;
using Flox.Shared.Settings;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Flox.Shared.Layout;

public sealed partial class MainLayout : IAsyncDisposable
{
    // Snapshot of whatever culture the host (browser-negotiated in WASM) resolved before this
    // component ever touches CurrentCulture - "System (default)" in the language picker means
    // "go back to this", not CultureInfo.InstalledUICulture (meaningless in a browser/container).
    private static readonly CultureInfo s_systemCulture = CultureInfo.CurrentCulture;

    private ThemeSettings _themeSettings = new ThemeSettings();
    // Another language rebuilds the whole layout: components that do not take parameters are not rendered again by their parent
    private int _cultureVersion;
    private bool _isDarkMode = ThemeSettings.DEFAULT_DARK;
    private AppErrorBoundary? _bodyErrorBoundary;
    private bool _isLoading = true;
    private string? _progressMessage;
    private Exception? _initializationException;

    [Inject]
    private Radzen.DialogService DialogService { get; set; } = null!;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = null!;

    [Inject]
    private ILogger<MainLayout> Logger { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    private INavigationHistoryService NavigationHistoryService { get; set; } = null!;

    [Inject]
    private ThemeService ThemeService { get; set; } = null!;

    [Inject]
    private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    [Inject]
    private IEventsConsumerService EventsConsumer { get; set; } = null!;

    [Inject]
    private FloxLogLevels LogLevels { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private IUiExceptionHandler Errors { get; set; } = null!;

    private IAppSettingsProvider<ThemeSettings>? _themeSettingsProvider;

    private IAppSettingsProvider<GeneralAppSettings>? _generalAppSettingsProvider;

    private TelemetryConsentBanner? _consentBanner;

    private void SetSplashMessage(string msg)
    {
        _progressMessage = msg;
        StateHasChanged();
    }

    // Should be called in AfterRender since app settings providers depend on async storages
    private async Task PerformInititializationAsync()
    {
        StartupTimeline.Mark("MainLayout: initialization starts");
        SetSplashMessage("Initializing...");
        _isLoading = true;

        try
        {
            SetSplashMessage("Authenticating...");
            AuthenticationState authState;
            using (StartupTimeline.Span("MainLayout: authentication state"))
            {
                authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            }

            Logger.LogInformation("Authentication state loaded: {IsAuthenticated}", authState.User.Identity?.IsAuthenticated ?? false);
            AuthenticationStateProvider.AuthenticationStateChanged -= AuthenticationStateProvider_AuthenticationStateChanged;
            AuthenticationStateProvider.AuthenticationStateChanged += AuthenticationStateProvider_AuthenticationStateChanged;

            // The server sets the levels of the logs of the client (what is set in the administration)
            EventsConsumer.OnMessage -= EventsConsumer_OnMessage;
            EventsConsumer.OnMessage += EventsConsumer_OnMessage;

            using (StartupTimeline.Span("MainLayout: events subscribe"))
            {
                await EventsConsumer.SubscribeAsync();
            }

            NavigationManager.OnNotFound -= NavigationManager_OnNotFoundAsync;
            NavigationManager.OnNotFound += NavigationManager_OnNotFoundAsync;

            SetSplashMessage("Loading settings...");
            _generalAppSettingsProvider = ServiceProvider.GetRequiredService<IAppSettingsProvider<GeneralAppSettings>>();
            _generalAppSettingsProvider.SettingsChanged -= OnGeneralAppSettingsProviderSettingsChanged;
            _generalAppSettingsProvider.SettingsChanged += OnGeneralAppSettingsProviderSettingsChanged;
            _themeSettingsProvider = ServiceProvider.GetRequiredService<IAppSettingsProvider<ThemeSettings>>();
            _themeSettingsProvider.SettingsChanged -= OnThemeProviderSettingsChanged;
            _themeSettingsProvider.SettingsChanged += OnThemeProviderSettingsChanged;

            // Two independent requests (each provider has its own data source): one round trip instead of two
            GeneralAppSettings appSettings;
            ThemeSettings themeSettings;
            using (StartupTimeline.Span("MainLayout: general and theme settings"))
            {
                var general = _generalAppSettingsProvider.GetValue();
                var theme = _themeSettingsProvider.GetValue();
                appSettings = await general;
                themeSettings = await theme;
            }

            ApplyLanguage(appSettings.Language);
            ApplyTelemetryConsent(appSettings);

            SetSplashMessage("Loading theme...");

            _themeSettings = themeSettings!;
            ApplyTheme();
            SetSplashMessage("Theme loaded");

            SetSplashMessage("Ready");
            _isLoading = false;

            await InvokeAsync(StateHasChanged);
            await ReportStartupAsync();
        }
        catch (Exception ex)
        {
            _initializationException = ex;
            _isLoading = false;
            Logger.LogError(ex, "Initialization error");
            _ = InvokeAsync(StateHasChanged);
        }
    }

    // The timeline is the process's own clock; in the browser the time before Main (download, runtime start) is not on it,
    // so the browser's clock is read at the same moment to tell how late the timeline began
    private async Task ReportStartupAsync()
    {
        StartupTimeline.Mark("MainLayout: ready");
        var title = "Startup timeline of the client";
        var attributes = new Dictionary<string, string>();
        if (OperatingSystem.IsBrowser())
        {
            try
            {
                var browserNow = await JS.InvokeAsync<double>("eval", "performance.now()");
                var lateBy = browserNow - StartupTimeline.ElapsedMilliseconds;
                title += string.Create(CultureInfo.InvariantCulture, $" (the browser clock reads {browserNow:F0} ms since the navigation started, the timeline began {lateBy:F0} ms after it)");
                attributes["browser.clock.ms"] = browserNow.ToString("F0", CultureInfo.InvariantCulture);
                attributes["timeline.began.after.navigation.ms"] = lateBy.ToString("F0", CultureInfo.InvariantCulture);
            }
            catch (JSException)
            {
                // the report without the browser's clock is still worth having
            }
        }

        StartupTimeline.Report(Logger, title);

        // The table stays in the log of the client; the same spans go to the server as a trace, if the user agrees to it
        if (ServiceProvider.GetService<IStartupTraceExporter>() is { } exporter)
        {
            await exporter.ExportAsync(StartupTimeline.Snapshot(), attributes);
        }
    }

    private void ApplyTheme()
    {
        ThemeService.ApplyThemeSettings(_themeSettings);
        _isDarkMode = ThemeService.IsDarkMode;
    }

    private async Task RetryInitialization()
    {
        _bodyErrorBoundary?.Recover();
        _initializationException = null;
        await PerformInititializationAsync();
    }

    // Any dialog closes through Radzen's dynamic calls: the first one would freeze the page, so it happens when the page is idle
    private static async Task WarmUpDialogsAsync()
    {
        await Task.Delay(1000);
        DynamicBinderWarmup.Run();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            StartupTimeline.Mark("MainLayout: first render done");
            await PerformInititializationAsync();
            _ = WarmUpDialogsAsync();
        }
        else if (!_isLoading && _initializationException is null && _consentBanner is null && !NavigationManager.IsEmbedded() && _generalAppSettingsProvider is not null)
        {
            _consentBanner = new TelemetryConsentBanner(JS, OnBannerTelemetryChangedAsync);
            await _consentBanner.ShowAsync((await _generalAppSettingsProvider.GetValue()).AllowTelemetry);
        }
    }

    private async Task OnBannerTelemetryChangedAsync(bool allowed)
    {
        var appSettings = await _generalAppSettingsProvider!.GetValue();
        if (appSettings.AllowTelemetry != allowed)
        {
            appSettings.AllowTelemetry = allowed;
            await _generalAppSettingsProvider.SaveValue(appSettings);
        }
    }

    private void AuthenticationStateProvider_AuthenticationStateChanged(Task<AuthenticationState> task)
    {
        Errors.Fire(async () =>
                    {
                        try
                        {
                            await task;
                            await EventsConsumer.SubscribeAsync();
                        }
                        finally
                        {
                            await InvokeAsync(StateHasChanged);
                        }
                    },
                    nameof(AuthenticationStateProvider_AuthenticationStateChanged));
    }

    private void NavigationManager_OnNotFoundAsync(object? sender, Microsoft.AspNetCore.Components.Routing.NotFoundEventArgs e)
    {
        Errors.Fire(async () =>
                    {
                        await DialogService.ShowMessageBoxAsync("Error", "404");
                        if (NavigationHistoryService.CanGoBack)
                        {
                            e.Path = null;
                            NavigationHistoryService.GoBack();
                        }
                    },
                    nameof(NavigationManager_OnNotFoundAsync));
    }

    private void OnThemeProviderSettingsChanged(object? sender, EventArgs e)
    {
        Errors.Fire(async () =>
                    {
                        _themeSettings = await _themeSettingsProvider!.GetValue();
                        ApplyTheme();
                        await InvokeAsync(StateHasChanged);
                    },
                    nameof(OnThemeProviderSettingsChanged));
    }

    private void OnGeneralAppSettingsProviderSettingsChanged(object? sender, EventArgs e)
    {
        Errors.Fire(async () =>
                    {
                        var appSettings = await _generalAppSettingsProvider!.GetValue();
                        var language = CultureInfo.CurrentUICulture.Name;
                        ApplyLanguage(appSettings.Language);
                        if (language != CultureInfo.CurrentUICulture.Name)
                        {
                            _cultureVersion++;
                        }

                        ApplyTelemetryConsent(appSettings);
                        if (_consentBanner is not null)
                        {
                            await _consentBanner.SetAsync(appSettings.AllowTelemetry);
                        }

                        await InvokeAsync(StateHasChanged);
                    },
                    nameof(OnGeneralAppSettingsProviderSettingsChanged));
    }

    // CultureInfo.DefaultThreadCurrentCulture/DefaultThreadCurrentUICulture only seed threads
    // created afterwards - they don't affect the thread/execution context already rendering the
    // UI, so a language change made mid-session silently did nothing until a hard reload started
    // a fresh runtime. Setting CurrentCulture/CurrentUICulture directly updates the running
    // context immediately; DefaultThread* is kept too for any code that does spin up a new thread
    // (background tasks).
    // The sink of the client reads it; no sink (telemetry is off) means nothing to tell
    private void ApplyTelemetryConsent(GeneralAppSettings appSettings)
    {
        if (ServiceProvider.GetService<TelemetryConsent>() is { } consent)
        {
            consent.Allowed = appSettings.AllowTelemetry;
            Logger.LogInformation("Telemetry consent: {Allowed}", consent.Allowed);
        }
    }

    private static void ApplyLanguage(string? language)
    {
        CultureInfo? culture = null;
        if (!string.IsNullOrEmpty(language))
        {
            try
            {
                culture = new CultureInfo(language);
            }
            catch (CultureNotFoundException)
            {
                culture = null;
            }
        }

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture ?? s_systemCulture;
        CultureInfo.CurrentUICulture = culture ?? s_systemCulture;
    }

    private void EventsConsumer_OnMessage(object? sender, GenericEventArgs<EventMessage> evt)
    {
        try
        {
            if (evt.Value is { } message && LogLevels.TryApply(message))
            {
                Logger.LogInformation("The levels of the logs are set by the server: default {Default}", LogLevels.Current.Default);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "The levels of the logs sent by the server cannot be applied");
        }
    }

    public ValueTask DisposeAsync()
    {
        EventsConsumer.OnMessage -= EventsConsumer_OnMessage;
        _themeSettingsProvider?.SettingsChanged -= OnThemeProviderSettingsChanged;
        _generalAppSettingsProvider?.SettingsChanged -= OnGeneralAppSettingsProviderSettingsChanged;
        NavigationManager.OnNotFound -= NavigationManager_OnNotFoundAsync;
        AuthenticationStateProvider.AuthenticationStateChanged -= AuthenticationStateProvider_AuthenticationStateChanged;
        return _consentBanner?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
