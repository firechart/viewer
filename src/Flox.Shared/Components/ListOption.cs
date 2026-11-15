namespace Flox.Shared.Components;

/// <summary>
/// Item of a dropdown or radio list: Radzen binds <c>Text</c> and <c>Value</c> by property name.
/// </summary>
/// <param name="Text">The text shown to the user.</param>
/// <param name="Value">The value the item stands for.</param>
public sealed record ListOption<T>
(
    string Text,
    T Value
);
