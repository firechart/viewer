using Microsoft.AspNetCore.Components.Forms;

namespace Flox.Shared.Components;

/// <summary>
/// State of a form over a model: validation, create, delete and submit with the handlers a derived state overrides.
/// </summary>
public abstract class FormState<TModel> : EntityState<TModel> where TModel : class
{
    /// <inheritdoc />
    protected FormState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    /// <summary>
    /// The validation messages of the form.
    /// </summary>
    public string[] Errors { get; set; } = [];
    /// <summary>
    /// The edit form of the page, set by the page once it is rendered.
    /// </summary>
    public EditForm? Form { get; set; }

    /// <summary>
    /// Validates the model beyond the annotations; true when it is valid.
    /// </summary>
    public virtual async Task<bool> ValidateAsync()
    {
        return true;
    }

    /// <summary>
    /// Whether the model passed validation.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Creates a new model through <see cref="HandleCreateAsync"/> and makes it the model of the form; reports a failure and goes back.
    /// </summary>
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

    /// <summary>
    /// Creates the new model; a derived state overrides it. Returns null when it cannot be created.
    /// </summary>
    protected virtual async Task<TModel?> HandleCreateAsync()
    {
        return default;
    }

    /// <summary>
    /// Asks for confirmation, deletes the model and goes back.
    /// </summary>
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

    /// <summary>
    /// Deletes the model; a derived state overrides it.
    /// </summary>
    protected virtual async Task HandleDeleteAsync(TModel value)
    {
    }

    /// <summary>
    /// Validates the edit context of the form and calls <see cref="OnValidSubmit"/> or <see cref="OnInvalidSubmit"/>.
    /// </summary>
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

    /// <summary>
    /// Saves the valid model through <see cref="HandleValidSubmitAsync"/> and goes back; reports a failure to save.
    /// </summary>
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

    /// <summary>
    /// Called when the record to work with does not exist; goes back by default.
    /// </summary>
    protected virtual async Task HandleRecordNotFoundAsync()
    {
        if (NavigationHistoryService.CanGoBack)
        {
            NavigationHistoryService.GoBack();
        }
    }

    /// <summary>
    /// Saves the model; a derived state overrides it.
    /// </summary>
    protected virtual async Task HandleValidSubmitAsync(EditContext editContext)
    {
    }

    /// <summary>
    /// Handles a submit of an invalid form.
    /// </summary>
    public async Task OnInvalidSubmit(EditContext editContext)
    {
        await HandleInvalidSubmitAsync(editContext);
    }

    /// <summary>
    /// Reacts to an invalid submit; shows a message by default.
    /// </summary>
    protected virtual async Task HandleInvalidSubmitAsync(EditContext editContext)
    {
        this.ShowSnack(Strings.Message_FormInvalid);
    }

    /// <summary>
    /// Tells the user that the record could not be saved.
    /// </summary>
    public virtual async Task NotifyCannotBeSavedAsync(Exception? ex = null)
    {
        this.ShowSnack(Strings.Message_FormSaveFailed);
    }
}
