using Flox.Shared.Components;

namespace Flox.Tests.Platform;

// The rows of a list given as an array: selected by reference, dropped from the selection when they leave, and handed out in ranges
[TestFixture]
public class ArrayListStateTests
{
    private sealed class Row(int number)
    {
        public int Number { get; } = number;
    }

    private static List<Row> Rows(int count)
    {
        return [.. Enumerable.Range(1, count).Select(i => new Row(i))];
    }

    private void ARowThatLeaves_LeavesTheSelection()
    {
        var rows = Rows(3);
        var state = new ArrayListState<Row>();
        state.SetItems(rows);
        state.ToggleSelected(rows[0]);
        state.ToggleSelected(rows[2]);

        state.SetItems([rows[0], rows[1]]);

        Assert.Multiple(() =>
        {
            Assert.That(state.SelectedItems, Is.EquivalentTo(new[] { rows[0] }));
            Assert.That(state.IsSelected(rows[2]), Is.False);
        });
    }

    private void Targets_AreTheSelection_WhenTheRowIsInIt_AndTheRowAloneOtherwise()
    {
        var rows = Rows(3);
        var state = new ArrayListState<Row>();
        state.SetItems(rows);
        state.ToggleSelected(rows[0]);
        state.ToggleSelected(rows[1]);

        Assert.Multiple(() =>
        {
            Assert.That(state.Targets(rows[1]), Is.EquivalentTo(new[] { rows[0], rows[1] }));
            Assert.That(state.Targets(rows[2]), Is.EqualTo(new[] { rows[2] }));
        });
    }

    private async Task ARange_IsASlice_WithTheTotalOfAllRows()
    {
        var rows = Rows(10);
        var state = new ArrayListState<Row>();
        state.SetItems(rows);

        var range = await state.LoadRangeAsync(4, 3, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(range.Items, Is.EqualTo(new[] { rows[4], rows[5], rows[6] }));
            Assert.That(range.Total, Is.EqualTo(10));
        });
    }

    private void TheState_SaysItChanged_WhenTheRowsOrTheSelectionChange()
    {
        var rows = Rows(2);
        var state = new ArrayListState<Row>();
        var raised = 0;
        state.Changed += (_, _) => raised++;

        state.SetItems(rows);
        state.ToggleSelected(rows[0]);
        state.ClearSelection();
        state.ClearSelection();

        Assert.That(raised, Is.EqualTo(3), "the rows, the toggle, the first clear; the second clear has nothing to clear");
    }

    [Test]
    public async Task ArrayListStateScenario()
    {
        await Assert.MultipleAsync(async () =>
        {
            ARowThatLeaves_LeavesTheSelection();
            Targets_AreTheSelection_WhenTheRowIsInIt_AndTheRowAloneOtherwise();
            await ARange_IsASlice_WithTheTotalOfAllRows();
            TheState_SaysItChanged_WhenTheRowsOrTheSelectionChange();
        });
    }
}
