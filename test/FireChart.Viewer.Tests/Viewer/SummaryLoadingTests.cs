using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Pages;

namespace FireChart.Viewer.Tests.Viewer;

// How a summary loads: the page size of a search (the form, else the option, else 20, within the range); its dialog is LoadingPanel of Flox
public class SummaryLoadingTests
{
    [Test]
    public void ThePageSizeOfASearch_IsTheForms_ElseTheOption_ElseTheDefault_WithinTheRange()
    {
        var own = new SummaryFormSearch { ResourceType = "Observation", PageSize = 5 };
        var none = new SummaryFormSearch { ResourceType = "Condition" };

        Assert.Multiple(() =>
        {
            Assert.That(new SectionSearch(own, "en", pageSize: 100).PageSize, Is.EqualTo(5));
            Assert.That(new SectionSearch(none, "en", pageSize: new FhirOptions { PageSize = 100 }.EffectivePageSize()).PageSize, Is.EqualTo(100));
            Assert.That(new SectionSearch(none, "en").PageSize, Is.EqualTo(SectionSearch.DefaultPageSize));
            Assert.That(new FhirOptions { PageSize = 5000 }.EffectivePageSize(), Is.EqualTo(FhirOptions.MaxPageSize));
        });
    }
}
