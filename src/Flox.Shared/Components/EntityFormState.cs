namespace Flox.Shared.Components;

public abstract class EntityFormState<TModel> : FormState<TModel> where TModel : GuidEntity
{
    protected EntityFormState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

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

    protected virtual async Task<TModel?> HandleReadAsync(Guid value)
    {
        return null;
    }
}
