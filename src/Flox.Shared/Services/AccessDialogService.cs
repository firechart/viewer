using Flox.Shared.Components;


namespace Flox.Shared.Services;

/// <summary>
/// Opens the editor of the access list of a row from any page or form: the page names the entity and the key, the dialog does the rest (it
/// reads the list from the server, lets the caller change it and saves it).
/// </summary>
public interface IAccessDialogService
{
    /// <summary>
    /// Whether the application has access lists to edit: it has a server (<see cref="IFloxRuntime.HasServer"/>). Lite keeps its data on the
    /// device, where a row has no list; a button of the dialog is hidden when this is false.
    /// </summary>
    bool Available { get; }

    /// <summary>Opens the access list of a row; does nothing where the lists are not <see cref="Available"/>.</summary>
    /// <param name="entity">The name of the entity, as its permissions name it (<c>FhirServer</c>).</param>
    /// <param name="key">The key of the row.</param>
    /// <param name="title">What the dialog calls the row, else the name of the entity.</param>
    /// <returns>Whether the list was changed.</returns>
    Task<bool> EditAsync(string entity, Guid key, string? title = null);
}

/// <summary>The editor of an access list as a dialog (<see cref="AccessDialog"/>).</summary>
/// <param name="dialogs">The dialog service of Flox.</param>
/// <param name="runtime">The host, which says whether there is a server.</param>
public sealed class AccessDialogService(FloxDialogService dialogs, IFloxRuntime runtime) : IAccessDialogService
{
    /// <inheritdoc />
    public bool Available
    {
        get
        {
            return runtime.HasServer;
        }
    }

    /// <inheritdoc />
    public async Task<bool> EditAsync(string entity, Guid key, string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);
        if (!Available)
        {
            return false;
        }

        var parameters = new Dictionary<string, object?>
        {
            [nameof(AccessDialog.Entity)] = entity,
            [nameof(AccessDialog.Key)] = key
        };
        var result = await dialogs.OpenAsync<AccessDialog>(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Access_TitleOf, title ?? entity),
                                                            parameters,
                                                            new FloxDialogOptions
                                                            {
                                                                Width = "760px",
                                                                CloseOnEscape = true,
                                                                CloseOnOverlayClick = false
                                                            });
        return result is true;
    }
}

/// <summary>The editor of an access list for an entity type.</summary>
public static class AccessDialogServiceExtensions
{
    /// <summary>Opens the access list of a row of the entity type.</summary>
    /// <returns>Whether the list was changed.</returns>
    public static Task<bool> EditAsync<TEntity>(this IAccessDialogService dialogs, Guid key, string? title = null) where TEntity : IEntity
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        return dialogs.EditAsync(typeof(TEntity).Name, key, title);
    }
}
