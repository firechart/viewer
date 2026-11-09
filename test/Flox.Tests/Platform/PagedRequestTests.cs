using Flox.Core.Data;

namespace Flox.Tests.Platform;

[TestFixture]
public class PagedRequestTests
{
    private void Skip_IsTheRequest_AndPageIsAViewOfIt()
    {
        var request = new PagedRequest<object>
        {
            PageSize = 10,
            Skip = 25
        };

        Assert.Multiple(() =>
        {
            Assert.That(request.Skip, Is.EqualTo(25), "not rounded to a page");
            Assert.That(request.Page, Is.EqualTo(2));
            Assert.That(request.Top, Is.EqualTo(10));
        });
    }

    private void SkipThenPageSize_KeepsTheSkip()
    {
        var request = new PagedRequest<object>
        {
            Skip = 15
        };

        request.PageSize = 10;

        Assert.That(request.Skip, Is.EqualTo(15), "the order of the calls does not matter (a virtual list sets the skip first)");
    }

    private void Page_SetsTheSkip_OfThePageSize()
    {
        var request = new PagedRequest<object>
        {
            PageSize = 20
        };

        request.Page = 3;

        Assert.That(request.Skip, Is.EqualTo(60));
    }

    private void WithoutALimit_PageIsZero_AndSkipIsKept()
    {
        var request = new PagedRequest<object>
        {
            PageSize = -1,
            Skip = 7
        };
        request.Page = 4;

        Assert.Multiple(() =>
        {
            Assert.That(request.Page, Is.Zero);
            Assert.That(request.Skip, Is.EqualTo(7), "no page size, no page: the skip stays what was asked");
        });
    }

    [Test]
    public void PagedRequestScenario()
    {
        Assert.Multiple(() =>
        {
            Skip_IsTheRequest_AndPageIsAViewOfIt();
            SkipThenPageSize_KeepsTheSkip();
            Page_SetsTheSkip_OfThePageSize();
            WithoutALimit_PageIsZero_AndSkipIsKept();
        });
    }
}
