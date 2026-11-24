using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace FireChart.Viewer.Core.Reports;

// The texts of the components in the language of the reader: the resources locales/strings.resx (English, the keys) and
// locales/strings.{culture}.resx (de, he, zh-Hans), in the format of .NET, so Visual Studio and Rider edit them. A component names a text
// {{%strings.Key}}, with arguments {{%strings.Key(%a, %b)}} for {0}, {1}; a key the language has not translated is the English text
internal sealed class ReportStrings(IReadOnlyDictionary<string, string> texts)
{
    public const string Folder = "locales";

    public const string Name = "strings";

    public static readonly ReportStrings Empty = new(new Dictionary<string, string>());

    public IReadOnlyDictionary<string, string> Texts => texts;

    // The file of a culture: locales/strings.resx for none, locales/strings.de.resx
    public static string PathOf(string? culture)
    {
        return string.IsNullOrEmpty(culture) ? $"{Folder}/{Name}.resx" : $"{Folder}/{Name}.{culture}.resx";
    }

    // The cultures whose files make the texts of one, the least specific first: "" (English), he, he-IL
    public static IReadOnlyList<string> ChainOf(CultureInfo culture)
    {
        var chain = new List<string>();
        for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
        {
            chain.Insert(0, current.Name);
        }

        // English is the file without a culture (strings.resx): strings.en.resx and strings.en-US.resx are not asked for
        chain.RemoveAll(name => name.Equals("en", StringComparison.OrdinalIgnoreCase) || name.StartsWith("en-", StringComparison.OrdinalIgnoreCase));
        chain.Insert(0, string.Empty);
        return chain;
    }

    // The texts of the files of a chain, a later file over an earlier one
    public static ReportStrings Of(IEnumerable<IReadOnlyDictionary<string, string>> files)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (var (key, text) in file)
            {
                texts[key] = text;
            }
        }

        return new ReportStrings(texts);
    }

    // The text of a key with its arguments; a key no file has is shown as the key itself, a text whose placeholders the arguments do not fill as is
    public string Format(string key, IReadOnlyList<string> arguments, CultureInfo culture)
    {
        if (!texts.TryGetValue(key, out var text))
        {
            return key;
        }

        if (arguments.Count == 0)
        {
            return text;
        }

        try
        {
            return string.Format(culture, text, [.. arguments]);
        }
        catch (FormatException)
        {
            return text;
        }
    }

    // The texts of a .resx file: its <data name="…"><value>…</value></data> of strings (a data of another type, a file, is left out). An error of XML
    // is an InvalidDataException with its line. A byte order mark read as text (a file read as UTF-8 by Node) is skipped
    public static IReadOnlyDictionary<string, string> ReadResx(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml.TrimStart('﻿'), LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"{ex.LineNumber}: {ex.Message}", ex);
        }

        if (document.Root is not { Name.LocalName: "root" } root)
        {
            throw new InvalidDataException("1: a .resx file is a <root> of <data> elements");
        }

        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var data in root.Elements("data"))
        {
            if (data.Attribute("type") is not null || data.Attribute("mimetype") is not null || (string?)data.Attribute("name") is not { Length: > 0 } name)
            {
                continue;
            }

            texts[name] = (string?)data.Element("value") ?? string.Empty;
        }

        return texts;
    }
}
