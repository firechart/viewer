namespace Flox.Shared.Components;

public abstract class EntityState<T> : State where T : class
{
    protected EntityState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    protected T? _model;

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
