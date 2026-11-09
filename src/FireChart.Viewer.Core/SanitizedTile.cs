namespace FireChart.Viewer.Core;

// A row of a list after the sanitizer, and its text for the tooltip and the search
internal sealed record SanitizedTile(string Html, string Text);
