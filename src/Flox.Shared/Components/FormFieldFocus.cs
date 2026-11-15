using Microsoft.AspNetCore.Components.Forms;

namespace Flox.Shared.Components;

/// <summary>
/// The fields of a form that can take the focus (cascaded by the form): after a submit that fails validation the first invalid one gets it, so
/// a screen reader reads its message.
/// </summary>
public sealed class FormFieldFocus
{
    private readonly List<Entry> _fields = [];

    private sealed record Entry(object Owner, FieldIdentifier Field, Func<ValueTask> Focus);

    /// <summary>
    /// Adds a field after the ones added before; the owner removes it again with <see cref="Remove"/>.
    /// </summary>
    public void Add(object owner, FieldIdentifier field, Func<ValueTask> focus)
    {
        _fields.Add(new Entry(owner, field, focus));
    }

    /// <summary>
    /// Removes the fields of an owner.
    /// </summary>
    public void Remove(object owner)
    {
        _fields.RemoveAll(entry => ReferenceEquals(entry.Owner, owner));
    }

    /// <summary>
    /// Focuses the first field, in the order they were added, that has a validation message.
    /// </summary>
    /// <returns>Whether a field was focused.</returns>
    public async Task<bool> FocusFirstInvalidAsync(EditContext context)
    {
        foreach (var entry in _fields)
        {
            if (FieldAria.IsInvalid(context, entry.Field))
            {
                await entry.Focus();
                return true;
            }
        }

        return false;
    }
}
