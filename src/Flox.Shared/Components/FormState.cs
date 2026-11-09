using Microsoft.AspNetCore.Components.Forms;

namespace Flox.Shared.Components;

public abstract class FormState<TModel> : EntityState<TModel> where TModel : class
{
    protected FormState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public string[] Errors { get; set; } = [];
    public EditForm? Form { get; set; }

    public virtual async Task<bool> ValidateAsync()
    {
        return true;
    }

    public bool IsValid { get; set; }

    public async Task CreateAsync()
    {
        try
        {
            IsBusy = true;
            var model = await HandleCreateAsync();
            if (model == null)
            {
                this.ShowSnack(Strings.Message_RecordIsUndefined);
                await HandleRecordNotFoundAsync();
            }

            Model = model;
        }
        catch (Exception ex)
        {
            IsBusy = false;
            Logger.LogError(ex, "{Error}", Strings.Message_CreateFailed);
            await this.ShowErrorAsync(Strings.Message_CreateFailed);
            GoBack();
        }
    }

    protected virtual async Task<TModel?> HandleCreateAsync()
    {
        return default;
    }

    public async Task DeleteAsync()
    {
        try
        {
            if (Model == null)
            {
                this.ShowSnack(Strings.Message_RecordIsUndefined);
                await HandleRecordNotFoundAsync();
                return;
            }

            if (await this.ConfirmAsync(Strings.Caption_ConfirmDelete) == false)
            {
                return;
            }

            await HandleDeleteAsync(Model);

            // TODO: log

            this.ShowSnack(Strings.Message_DeleteSucceeded);
            GoBack();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Error}", Strings.Message_CreateFailed);
            await this.ShowErrorAsync(Strings.Message_CreateFailed);
        }
    }

    protected virtual async Task HandleDeleteAsync(TModel value)
    {
    }

    public async Task SubmitAsync()
    {
        if (Form != null && Form.EditContext != null)
        {
            if (Form.EditContext.Validate())
            {
                await OnValidSubmit(Form.EditContext);
            }
            else
            {
                await OnInvalidSubmit(Form.EditContext);
            }
        }
    }

    public async Task OnValidSubmit(EditContext editContext)
    {
        try
        {
            if (Model == null)
            {
                this.ShowSnack(Strings.Message_RecordIsUndefined);
                await HandleRecordNotFoundAsync();
                GoBack();
                return;
            }

            IsBusy = true;

            await HandleValidSubmitAsync(editContext);

            // TODO: log

            GoBack();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save record");
            await NotifyCannotBeSavedAsync(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected virtual async Task HandleRecordNotFoundAsync()
    {
        if (NavigationHistoryService.CanGoBack)
        {
            NavigationHistoryService.GoBack();
        }
    }

    protected virtual async Task HandleValidSubmitAsync(EditContext editContext)
    {
    }

    public async Task OnInvalidSubmit(EditContext editContext)
    {
        await HandleInvalidSubmitAsync(editContext);
    }

    protected virtual async Task HandleInvalidSubmitAsync(EditContext editContext)
    {
        this.ShowSnack(Strings.Message_FormInvalid);
    }

    public virtual async Task NotifyCannotBeSavedAsync(Exception? ex = null)
    {
        this.ShowSnack(Strings.Message_FormSaveFailed);
    }
}
