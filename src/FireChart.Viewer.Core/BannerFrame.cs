using System.Net;

namespace FireChart.Viewer.Core;

// The banner of a context resource laid out of the slots its template filled (title, facts, flags): the template says what, the frame where. The patient
// is the main banner, any other resource (an encounter) a sub-banner below it; the title leads to the resource when the caller asks (banner_link)
internal static class BannerFrame
{
    public const string Patient = "Patient";

    public static string Compose(FrameSlots slots, string type, string? id, bool link)
    {
        var title = slots[FrameSlots.Title].Trim();
        if (title.Length == 0)
        {
            title = WebUtility.HtmlEncode(string.IsNullOrEmpty(id) ? type : id);
        }

        if (link && !string.IsNullOrEmpty(id))
        {
            title = $"<a href=\"{FxHtml.FhirScheme}{WebUtility.HtmlEncode(type)}/{WebUtility.HtmlEncode(id)}\">{title}</a>";
        }

        var kind = type == Patient ? "fx-banner" : "fx-banner fx-banner-sub";
        return $"<header class=\"{kind}\"><h2 class=\"fx-banner-title\">{title}</h2>" +
               $"<div class=\"fx-banner-facts\">{slots[FrameSlots.Facts].Trim()}</div>" +
               $"<div class=\"fx-banner-flags\">{slots[FrameSlots.Flags].Trim()}</div></header>";
    }
}
