namespace Flox.Shared.Components;

/// <summary>
/// Item of a dropdown or radio list.
/// </summary>
/// <param name="Text">The text shown to the user.</param>
/// <param name="Value">The value the item stands for.</param>
public sealed record ListOption<T>
(
    string Text,
    T Value
);
