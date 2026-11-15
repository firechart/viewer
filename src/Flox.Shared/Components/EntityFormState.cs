namespace Flox.Shared.Components;

/// <summary>
/// State of a form over an entity with a <see cref="Guid"/> key, which can be read by the key.
/// </summary>
public abstract class EntityFormState<TModel> : FormState<TModel> where TModel : GuidEntity
{
    /// <inheritdoc />
    protected EntityFormState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    /// <summary>
    /// Reads the entity with the key into <c>Model</c>; opens the not-found page when there is none.
    /// </summary>
    public async Task ReadAsync(Guid value)
    {
        try
        {
            IsBusy = true;
            var model = await HandleReadAsync(value);
            if (model == null)
            {
                NavigationManager.NotFound();
            }

            Model = model;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read the entity: {Error}", Strings.Message_CreateFailed);
            await this.ShowErrorAsync(Strings.Message_CreateFailed);
            GoBack();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Reads the entity from the storage; a derived state overrides it. Returns null when the entity does not exist.
    /// </summary>
    protected virtual async Task<TModel?> HandleReadAsync(Guid value)
    {
        return null;
    }
}
