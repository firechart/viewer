using System.Collections.Frozen;
using Hl7.Fhir.Model;

namespace FireChart.Viewer.Core.Reports;

// The tone of a status or of an interpretation by its code, as the partials status-badge and interpretation-badge of the templates had it
internal static class ReportTones
{
    private static readonly FrozenDictionary<string, string> s_status = Tones(
        ("success", ["active", "finished", "completed", "complete", "final", "resolved", "confirmed", "amended", "corrected", "accepted", "fulfilled",
                     "permit", "available", "achieved", "sustaining"]),
        ("info", ["in-progress", "planned", "preliminary", "registered", "arrived", "triaged", "onleave", "draft", "requested", "received", "booked",
                  "pending", "proposed", "ready", "queued", "open", "in-use"]),
        ("warning", ["on-hold", "unconfirmed", "provisional", "differential", "suspended", "recurrence", "relapse", "preparation", "waitlist", "partial",
                     "urgent", "asap", "unable-to-assess"]),
        ("danger", ["entered-in-error", "cancelled", "stopped", "not-done", "refuted", "inactive", "rejected", "error", "failed", "declined", "noshow",
                    "revoked", "deny", "stat", "high"]));

    private static readonly FrozenDictionary<string, string> s_interpretation = Tones(
        ("danger", ["HH", "LL", "AA", "HU", "LU", "POS", "DET"]),
        ("warning", ["H", "L", "A", "<", ">", "IND", "E"]),
        ("success", ["N", "NEG", "ND", "S"]));

    public static string Status(string? code)
    {
        return code is null ? string.Empty : s_status.GetValueOrDefault(code, string.Empty);
    }

    public static string Interpretation(string? code)
    {
        return code is null ? string.Empty : s_interpretation.GetValueOrDefault(code, string.Empty);
    }

    // The code of a code, a Coding or the first coding of a CodeableConcept
    public static string? CodeOf(PocoNode node)
    {
        return node.Poco switch
        {
            PrimitiveType primitive => primitive.JsonValue?.ToString(),
            _ when node.Poco.TypeName == "Coding" => ReportText.Children(node, "code").FirstOrDefault() is { Poco: PrimitiveType code } ? code.JsonValue?.ToString() : null,
            _ when node.Poco.TypeName == "CodeableConcept" => ReportText.Children(node, "coding").FirstOrDefault() is { } coding ? CodeOf(coding) : null,
            _ => null
        };
    }

    private static FrozenDictionary<string, string> Tones(params (string Tone, string[] Codes)[] groups)
    {
        return groups.SelectMany(group => group.Codes.Select(code => KeyValuePair.Create(code, group.Tone))).ToFrozenDictionary(StringComparer.Ordinal);
    }
}
