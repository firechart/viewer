using Flox.Core.Models;
using Flox.Shared.Components;

namespace Flox.Tests.Platform;

// The selection of a list: rows accumulate across pages, are told apart by key, and a command acts on the selection only when the row belongs to it
[TestFixture]
public class SelectionModelTests
{
    private static UserBookmark Row(Guid? uid = null)
    {
        return new UserBookmark { Uid = uid ?? Guid.NewGuid() };
    }

    private void Rows_AreToldApartByKey_NotByReference()
    {
        var selection = new SelectionModel<Guid, UserBookmark>(row => row.Uid);
        var first = Row();
        var sameRowReloaded = Row(first.Uid);

        selection.Select(first);

        Assert.Multiple(() =>
        {
            Assert.That(selection.IsSelected(sameRowReloaded), Is.True);
            Assert.That(selection.Count, Is.EqualTo(1));
        });
    }

    private void Toggle_AccumulatesRows_AndDeselectsOnTheSecondCall()
    {
        var selection = new SelectionModel<Guid, UserBookmark>(row => row.Uid);
        var a = Row();
        var b = Row();

        selection.Toggle(a);
        selection.Toggle(b);
        Assert.That(selection.Count, Is.EqualTo(2), "rows of different pages accumulate");

        selection.Toggle(a);
        Assert.Multiple(() =>
        {
            Assert.That(selection.IsSelected(a), Is.False);
            Assert.That(selection.IsSelected(b), Is.True);
        });
    }

    private void Targets_AreTheWholeSelection_WhenTheRowBelongsToIt_AndTheRowAloneOtherwise()
    {
        var selection = new SelectionModel<Guid, UserBookmark>(row => row.Uid);
        var a = Row();
        var b = Row();
        var other = Row();
        selection.Select(a);
        selection.Select(b);

        Assert.Multiple(() =>
        {
            Assert.That(selection.Targets(a), Is.EquivalentTo(new[] { a, b }));
            Assert.That(selection.Targets(other), Is.EqualTo(new[] { other }), "a menu on a row outside the selection acts on that row only");
        });
    }

    private void Refresh_SwapsInTheReloadedRows_AndSaysSoOnlyWhenSomethingChanged()
    {
        var selection = new SelectionModel<Guid, UserBookmark>(row => row.Uid);
        var row = Row();
        selection.Select(row);
        selection.SetCurrent(row);
        var raised = 0;
        selection.Changed += (_, _) => raised++;

        selection.Refresh([row]);
        Assert.That(raised, Is.Zero, "the same references: nothing to tell");

        var reloaded = Row(row.Uid);
        selection.Refresh([reloaded, Row()]);

        Assert.Multiple(() =>
        {
            Assert.That(selection.Items, Is.EqualTo(new[] { reloaded }).Using<UserBookmark>(ReferenceEquals), "the selection holds the current data");
            Assert.That(selection.Current, Is.SameAs(reloaded));
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(selection.Count, Is.EqualTo(1), "a row that was not selected stays unselected");
        });
    }

    private void Clear_SaysSoOnce_AndNothingWhenThereIsNothing()
    {
        var selection = new SelectionModel<Guid, UserBookmark>(row => row.Uid);
        var raised = 0;
        selection.Changed += (_, _) => raised++;

        selection.Clear();
        Assert.That(raised, Is.Zero);

        selection.Select(Row());
        selection.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(raised, Is.EqualTo(2), "one for the select, one for the clear");
            Assert.That(selection.Any, Is.False);
            Assert.That(selection.Current, Is.Null);
        });
    }

    [Test]
    public void SelectionModelScenario()
    {
        Assert.Multiple(() =>
        {
            Rows_AreToldApartByKey_NotByReference();
            Toggle_AccumulatesRows_AndDeselectsOnTheSecondCall();
            Targets_AreTheWholeSelection_WhenTheRowBelongsToIt_AndTheRowAloneOtherwise();
            Refresh_SwapsInTheReloadedRows_AndSaysSoOnlyWhenSomethingChanged();
            Clear_SaysSoOnce_AndNothingWhenThereIsNothing();
        });
    }
}
