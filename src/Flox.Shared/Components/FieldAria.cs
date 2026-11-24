using Microsoft.AspNetCore.Components.Forms;

namespace Flox.Shared.Components;

/// <summary>
/// The ARIA attributes of a form control from the validation state of its field: <c>aria-invalid</c> and, while the field is invalid, its
/// messages in <c>aria-describedby</c>.
/// </summary>
public static class FieldAria
{
    /// <summary>
    /// Whether the field has a validation message.
    /// </summary>
    public static bool IsInvalid(EditContext? context, FieldIdentifier field)
    {
        return context is not null && context.GetValidationMessages(field).Any();
    }

    /// <summary>
    /// The attributes to splat on the control.
    /// </summary>
    /// <param name="context">The edit context of the form; no attributes without one.</param>
    /// <param name="field">The field the control edits.</param>
    /// <param name="messagesId">The id of the element that holds the messages of the field.</param>
    /// <param name="invalidAttribute">Whether <c>aria-invalid</c> is written; false for a control that writes its own.</param>
    /// <returns>The attributes; <c>aria-invalid</c> is "false" rather than missing while the field is valid.</returns>
    public static Dictionary<string, object> For(EditContext? context, FieldIdentifier field, string messagesId, bool invalidAttribute = true)
    {
        var attributes = new Dictionary<string, object>();
        if (context is null)
        {
            return attributes;
        }

        var invalid = IsInvalid(context, field);
        if (invalidAttribute)
        {
            attributes["aria-invalid"] = invalid ? "true" : "false";
        }

        if (invalid)
        {
            attributes["aria-describedby"] = messagesId;
        }

        return attributes;
    }
}
