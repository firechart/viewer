using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// The static texts of the templates in the language of the interface (locales/{language}/fhir-templates.json, the format of i18next)
public partial class TemplateTextsTests
{
    private static readonly VendorTemplates s_vendor = new();

    private const string Appointment = """{ "resourceType": "Appointment", "id": "a1", "status": "booked", "description": "Check-up", "participant": [ PARTICIPANTS ] }""";

    [GeneratedRegex("\"([^\"]+)\"\\s*\\|\\s*t\\b|render \"(?:field|row)\",\\s*label: \"([^\"]+)\"|render \"badge\",\\s*text: \"([^\"]+)\"")]
    private static partial Regex TextOfATemplate();

    private static async Task<string> InCultureAsync(string culture, Func<Task<string>> render)
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            return await render();
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    private static Task<string> ParticipantsAsync(string culture, int count)
    {
        var json = Appointment.Replace("PARTICIPANTS", string.Join(", ", Enumerable.Repeat("""{ "status": "accepted" }""", count)), StringComparison.Ordinal);
        return InCultureAsync(culture, async () => (await new FhirRenderer([s_vendor]).RenderTileAsync(json)).Text);
    }

    [TestCase("en-US", 1, "1 participant")]
    [TestCase("en-US", 2, "2 participants")]
    [TestCase("ru-RU", 1, "1 участник")]
    [TestCase("ru-RU", 3, "3 участника")]
    [TestCase("ru-RU", 5, "5 участников")]
    [TestCase("ru-RU", 21, "21 участник")]
    [TestCase("ru-RU", 12, "12 участников")]
    [TestCase("fr-FR", 1, "1 participant")]
    [TestCase("de-DE", 2, "2 Beteiligte")]
    [TestCase("zh-CN", 2, "2 名参与者")]
    public async Task ACount_TakesThePluralFormOfTheLanguage(string culture, int count, string expected)
    {
        Assert.That(await ParticipantsAsync(culture, count), Does.Contain(expected));
    }

    [Test]
    public async Task ALabelOfAPage_IsInTheLanguageOfTheInterface_AndInEnglishWithoutATranslation()
    {
        const string condition = """{ "resourceType": "Condition", "id": "c1", "code": { "text": "Asthma" }, "category": [ { "text": "problem" } ] }""";

        var russian = System.Net.WebUtility.HtmlDecode(await InCultureAsync("ru-RU", () => new FhirRenderer([s_vendor]).RenderAsync(condition, "view")));
        var japanese = await InCultureAsync("ja-JP", () => new FhirRenderer([s_vendor]).RenderAsync(condition, "view"));

        Assert.Multiple(() =>
        {
            Assert.That(russian, Does.Contain("Сведения").And.Contain("Категория"));
            Assert.That(japanese, Does.Contain("Details").And.Contain("Category"));
        });
    }

    [Test]
    public void EveryTextOfTheTemplates_HasATranslation_InEveryLanguage()
    {
        var folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "FireChart.Viewer.Library");
        var keys = Directory.EnumerateFiles(Path.Combine(folder, "templates"), "*.liquid", System.IO.SearchOption.AllDirectories)
                            .SelectMany(file => TextOfATemplate().Matches(File.ReadAllText(file)))
                            .Select(match => match.Groups.Values.Skip(1).First(group => group.Success).Value)
                            .ToHashSet(StringComparer.Ordinal);
        keys.UnionWith(EngineTexts.Keys);
        var gaps = new List<string>();
        foreach (var language in new[] { "de", "es", "fr", "ru", "zh" })
        {
            var texts = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(folder, "locales", language, "fhir-templates.json")))!;
            gaps.AddRange(keys.Where(key => !texts.ContainsKey(key) && !texts.ContainsKey(key + "_other")).Select(key => $"{language}: {key}"));
            gaps.AddRange(texts.Keys.Where(key => !keys.Contains(Regex.Replace(key, "_(zero|one|two|few|many|other)$", string.Empty))).Select(key => $"{language}: unused {key}"));
        }

        Assert.That(gaps, Is.Empty);
    }

    [TestCase("ru", 1, "one")]
    [TestCase("ru", 4, "few")]
    [TestCase("ru", 11, "many")]
    [TestCase("ru", 111, "many")]
    [TestCase("ru", 101, "one")]
    [TestCase("fr", 1, "one")]
    [TestCase("en", 0, "other")]
    [TestCase("zh", 1, "other")]
    public void ThePluralCategory_IsTheOneOfCldr(string language, int count, string category)
    {
        Assert.That(PluralRules.Category(language, count), Is.EqualTo(category));
    }
}
