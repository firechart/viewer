using Flox.Core.Settings;

namespace Flox.Shared.Pages.Settings;

public class SettingsPageState : State
{
    private readonly ISettingsCatalog _catalog;

    public SettingsPageState(IServiceProvider serviceProvider, ISettingsCatalog catalog) : base(serviceProvider)
    {
        _catalog = catalog;
    }

    public bool IsModified
    {
        get
        {
            return Sections.Any(section => section.IsModified);
        }
    }

    public IReadOnlyList<SettingsSectionState> Sections { get; private set; } = [];

    public async Task DiscardAsync()
    {
        foreach (var section in Sections)
        {
            await section.ReloadAsync();
        }

        NotifyChanged();
    }

    // Every section back to its defaults, unsaved edits included
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

    public async Task SaveAsync()
    {
        var modified = Sections.Where(section => section.IsModified)
                               .ToList();
        if (modified.Any(section => !section.EditContext.Validate()))
        {
            NotifyChanged();
            return;
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

    // Values changed elsewhere (another tab, a sync) are picked up; the edits in progress are kept
    protected override async Task OnResumeAsync()
    {
        foreach (var section in Sections.Where(section => !section.IsModified))
        {
            await section.ReloadAsync();
        }

        NotifyChanged();
    }
}
