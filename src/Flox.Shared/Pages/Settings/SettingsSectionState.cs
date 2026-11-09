using Flox.Core.Settings;
using Microsoft.AspNetCore.Components.Forms;

namespace Flox.Shared.Pages.Settings;

public sealed class SettingsSectionState
{
    public SettingsSectionState(SettingsSectionDescriptor descriptor, IAppSettingsProvider provider, object model)
    {
        Descriptor = descriptor;
        Provider = provider;
        Model = model;
        EditContext = new EditContext(model);
    }

    public SettingsSectionDescriptor Descriptor { get; }

    public EditContext EditContext { get; private set; }

    public bool IsModified
    {
        get
        {
            return EditContext.IsModified();
        }
    }

    public object Model { get; private set; }

    public IAppSettingsProvider Provider { get; }

    public async Task ReloadAsync()
    {
        Model = await Provider.GetObjectAsync();
        EditContext = new EditContext(Model);
    }

    public async Task ResetAsync()
    {
        await Provider.ResetAsync();
        await ReloadAsync();
    }

    public async Task SaveAsync()
    {
        await Provider.SaveObjectAsync(Model);
        EditContext.MarkAsUnmodified();
    }
}
