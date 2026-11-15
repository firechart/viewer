using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Viewer;

// The texts of the vendor summary forms are written in the forms (inline), in every language of the product: a text without one of them shows
// English to that user. The designer checks the forms of a project the same way (Check Project)
public class SummaryFormTextsTests
{
    private static readonly string[] s_languages = ["en-US", "de", "es", "he", "ru", "zh-Hans"];

    [Test]
    public void EveryTextOfAVendorSummaryForm_IsInEveryLanguageOfTheProduct()
    {
        var missing = new List<string>();
        foreach (var (code, text) in VendorSummaryForms.ReadFolder(TestLibrary.WebRoot()))
        {
            var form = ViewerJson.Read<SummaryFormDefinition>(text);
            Check($"{code}: title", form.Title);
            Check($"{code}: description", form.Description);
            foreach (var section in form.Sections)
            {
                Check($"{code}/{section.Code}: title", section.Title);
                foreach (var search in section.Searches)
                {
                    Check($"{code}/{section.Code}/{search.ResourceType}: sortLabel", search.SortLabel);
                }
            }
        }

        Assert.That(missing, Is.Empty);

        void Check(string where, LocalizedString texts)
        {
            if (texts.Count > 0 && s_languages.Except(texts.Keys, StringComparer.OrdinalIgnoreCase).ToList() is { Count: > 0 } lacking)
            {
                missing.Add($"{where} lacks {string.Join(", ", lacking)}");
            }
        }
    }
}
