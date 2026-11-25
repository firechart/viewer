using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Serialization;

namespace FireChart.Viewer.Core.Bundles;

// The text of a Bundle as it was pasted, dropped or read: JSON as it is, XML turned into JSON by the model of a release
internal static class BundleText
{
    public static bool IsXml(string text)
    {
        return text.AsSpan().TrimStart().StartsWith("<");
    }

    // FormatException when the XML is not a resource of the release
    public static string ToJson(string xml, ModelInspector model)
    {
        try
        {
            var typed = FhirXmlNode.Parse(xml).ToTypedElement(model, settings: new TypedElementSettings { ErrorMode = TypedElementSettings.TypeErrorMode.Passthrough });
            return typed.ToJson();
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or FormatException or InvalidOperationException or StructuralTypeException)
        {
            throw new FormatException("The text is not well formed FHIR XML.", ex);
        }
    }
}
