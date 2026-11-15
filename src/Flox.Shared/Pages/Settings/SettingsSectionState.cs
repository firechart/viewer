using Flox.Core.Settings;
using Microsoft.AspNetCore.Components.Forms;

namespace Flox.Shared.Pages.Settings;

/// <summary>The state of one group of settings on the settings page: its model, the form context and the provider that stores it.</summary>
public sealed class SettingsSectionState
{
    /// <summary>Creates the state of a section from the loaded <paramref name="model"/>.</summary>
    public SettingsSectionState(SettingsSectionDescriptor descriptor, IAppSettingsProvider provider, object model)
    {
        Descriptor = descriptor;
        Provider = provider;
        Model = model;
        EditContext = new EditContext(model);
    }

    /// <summary>What describes the section: its fields and scope.</summary>
    public SettingsSectionDescriptor Descriptor { get; }

    /// <summary>The edit context of the current model, which tracks the edits.</summary>
    public EditContext EditContext { get; private set; }

    /// <summary>Whether the model has unsaved edits.</summary>
    public bool IsModified
    {
        get
        {
            return EditContext.IsModified();
        }
    }

    /// <summary>The settings object being edited.</summary>
    public object Model { get; private set; }

    /// <summary>The provider that reads and saves the settings of the section.</summary>
    public IAppSettingsProvider Provider { get; }

    /// <summary>Reads the settings again, dropping unsaved edits.</summary>
    public async Task ReloadAsync()
    {
        Model = await Provider.GetObjectAsync();
        EditContext = new EditContext(Model);
    }

    /// <summary>Brings the settings back to their defaults and reads them again.</summary>
    public async Task ResetAsync()
    {
        await Provider.ResetAsync();
        await ReloadAsync();
    }

    /// <summary>Saves the model and marks it unmodified.</summary>
    public async Task SaveAsync()
    {
        await Provider.SaveObjectAsync(Model);
        EditContext.MarkAsUnmodified();
    }
}
