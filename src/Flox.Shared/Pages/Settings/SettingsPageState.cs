using Flox.Core.Settings;

namespace Flox.Shared.Pages.Settings;

/// <summary>State of the settings page: one section for every group of user settings, edited together.</summary>
public class SettingsPageState : State
{
    private readonly ISettingsCatalog _catalog;

    /// <summary>Creates the state over the catalog of settings.</summary>
    public SettingsPageState(IServiceProvider serviceProvider, ISettingsCatalog catalog) : base(serviceProvider)
    {
        _catalog = catalog;
    }

    /// <summary>Whether any section has unsaved edits.</summary>
    public bool IsModified
    {
        get
        {
            return Sections.Any(section => section.IsModified);
        }
    }

    /// <summary>The sections shown, one for every group of settings the user may edit.</summary>
    public IReadOnlyList<SettingsSectionState> Sections { get; private set; } = [];

    /// <summary>Throws away unsaved edits by reading every section again.</summary>
    public async Task DiscardAsync()
    {
        foreach (var section in Sections)
        {
            await section.ReloadAsync();
        }

        NotifyChanged();
    }

    /// <summary>After confirmation brings every section back to its defaults, unsaved edits included.</summary>
    public async Task ResetAsync()
    {
        if (await this.ConfirmAsync(Strings.Message_ConfirmResetSettings) != true)
        {
            return;
        }

        await RunAsync(async () =>
        {
            foreach (var section in Sections)
            {
                await section.ResetAsync();
            }
        });

        NotifyChanged();
    }

    /// <summary>Validates and saves the sections that have edits.</summary>
    public async Task SaveAsync()
    {
        var modified = Sections.Where(section => section.IsModified)
                               .ToList();
        foreach (var section in modified)
        {
            if (!await section.EditContext.ValidateAsync())
            {
                NotifyChanged();
                return;
            }
        }

        var saved = await RunAsync(async () =>
        {
            foreach (var section in modified)
            {
                await section.SaveAsync();
            }
        });

        if (saved)
        {
            this.ShowSnack(Strings.Caption_Saved);
        }

        NotifyChanged();
    }

    /// <inheritdoc />
    protected override async Task OnStartAsync(object? key)
    {
        await RunAsync(async () =>
        {
            var loaded = new List<SettingsSectionState>();
            // A class of hidden values only (the state of a view) has nothing to edit here
            foreach (var descriptor in _catalog.Sections.Where(section => section.Scope == SettingsScope.User && section.Fields.Count > 0))
            {
                var providerType = typeof(IAppSettingsProvider<>).MakeGenericType(descriptor.ModelType);
                var provider = (IAppSettingsProvider)ServiceProvider.GetRequiredService(providerType);
                loaded.Add(new SettingsSectionState(descriptor, provider, await provider.GetObjectAsync()));
            }

            // One form per turn of the event loop: the first one shows up at once and the page never freezes for all of them
            var shown = new List<SettingsSectionState>();
            Sections = shown;
            foreach (var section in loaded)
            {
                shown.Add(section);
                NotifyChanged();
                await Task.Delay(1);
            }
        });
    }

    /// <summary>Picks up values changed elsewhere (another tab); the edits in progress are kept.</summary>
    protected override async Task OnResumeAsync()
    {
        foreach (var section in Sections.Where(section => !section.IsModified))
        {
            await section.ReloadAsync();
        }

        NotifyChanged();
    }
}
