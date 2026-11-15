namespace Flox.Shared.Components;

/// <summary>
/// State of a page that shows one entity.
/// </summary>
public abstract class EntityState<T> : State where T : class
{
    /// <inheritdoc />
    protected EntityState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    /// <summary>
    /// The entity backing field.
    /// </summary>
    protected T? _model;

    /// <summary>
    /// The entity of the page; changing it notifies the page.
    /// </summary>
    public T? Model
    {
        get
        {
            return _model;
        }

        set
        {
            if (_model != value)
            {
                _model = value;
                NotifyChanged();
            }
        }
    }
}
