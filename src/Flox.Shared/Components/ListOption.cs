namespace Flox.Shared.Components;

// Item of a dropdown or radio list: Radzen binds Text and Value by property name
public sealed record ListOption<T>
(
    string Text,
    T Value
);
