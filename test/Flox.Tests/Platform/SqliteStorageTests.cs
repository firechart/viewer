using Flox.Core.Data;
using Flox.Sqlite;
using Flox.Sqlite.Files;

namespace Flox.Tests.Platform;

// A request of a data source over a SQLite base read only: the columns are SQL, the rest is done in memory with the rules of the other storages;
// the answer is the same either way
[TestFixture]
public class SqliteStorageTests
{
    public class Note : GuidEntity
    {
        public string Shelf { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public int Size { get; set; }

        public bool Pinned { get; set; }

        public string? Body { get; set; }
    }

    private string _root = null!;
    private FileSqliteEngine _engine = null!;
    private SqliteSource _source = null!;
    private SqliteStorage<Note> _storage = null!;

    private static readonly SqliteMapping<Note> s_notes = new()
    {
        Table = 7,
        From = "note n",
        RowId = "n.id",
        Select = "n.id, n.shelf, n.title, n.size, n.pinned, n.body",
        FullText = "n.id IN (SELECT rowid FROM note_fts WHERE note_fts MATCH $fts)",
        LargeFields = new HashSet<string>(StringComparer.Ordinal) { nameof(Note.Body) },
        Columns = new Dictionary<string, SqliteColumn>(StringComparer.Ordinal)
        {
            [nameof(Note.Shelf)] = new("n.shelf"),
            [nameof(Note.Title)] = new("n.title"),
            [nameof(Note.Size)] = new("n.size"),
            [nameof(Note.Pinned)] = new("n.pinned", SqliteColumn.Flag)
        },
        Read = (row, read) => new Note
        {
            Uid = SqliteKeys.Of(7, row.GetInt64(0)),
            Shelf = row.GetString(1),
            Title = row.GetString(2),
            Size = row.GetInt32(3),
            Pinned = row.GetBoolean(4),
            Body = read.Full ? row.GetString(5) : null
        }
    };

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "flox-sqlite-" + Guid.NewGuid().ToString("N"));
        _engine = new FileSqliteEngine(_root);
        await using (var db = await _engine.OpenAsync("notes.sqlite", SqliteAccess.ReadWrite))
        {
            await db.ExecuteAsync("PRAGMA user_version = 3");
            await db.ExecuteBatchAsync([
                new SqliteStatement("CREATE TABLE note (id INTEGER PRIMARY KEY, shelf TEXT NOT NULL, title TEXT NOT NULL, size INTEGER NOT NULL, pinned INTEGER NOT NULL, body TEXT NOT NULL)"),
                new SqliteStatement("CREATE VIRTUAL TABLE note_fts USING fts5 (title, body, content = 'note', content_rowid = 'id')"),
                .. new (string Shelf, string Title, long Size, long Pinned, string Body)[]
                {
                    ("home", "Alpha", 3, 1, "first note"), ("home", "Beta", 1, 0, "second"), ("home", "alpine", 2, 0, "a mountain"),
                    ("work", "Gamma", 5, 1, "third note"), ("work", "Delta", 4, 0, "fourth")
                }.Select(n => new SqliteStatement("INSERT INTO note (shelf, title, size, pinned, body) VALUES ($s, $t, $z, $p, $b)",
                                                  new Dictionary<string, object?> { ["$s"] = n.Shelf, ["$t"] = n.Title, ["$z"] = n.Size, ["$p"] = n.Pinned, ["$b"] = n.Body })),
                new SqliteStatement("INSERT INTO note_fts (note_fts) VALUES ('rebuild')")
            ]);
        }

        _source = new SqliteSource(_engine, "notes.sqlite", 3);
        _storage = new SqliteStorage<Note>(_source, s_notes);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _source.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_root, true);
    }

    private async Task<List<string>> TitlesAsync(Action<PagedRequest<Note>> build)
    {
        var request = new PagedRequest<Note> { PageSize = 0 };
        build(request);
        return [.. (await _storage.FindAsync(request)).Items.Select(n => n.Title)];
    }

    [Test]
    public async Task The_columns_are_SQL()
    {
        var page = await _storage.FindAsync(new PagedRequest<Note>
        {
            PageSize = 1,
            Skip = 1,
            Filters = { new Filter { Field = nameof(Note.Shelf), Operator = FilterOperator.Eq, Value = "home" } },
            Sorts = { new Sort { Field = nameof(Note.Size), Ascending = false } }
        });

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(page.Total, Is.EqualTo(3));
            Assert.That(page.Items.Single().Title, Is.EqualTo("alpine"), "the second of home by size, descending");
            Assert.That(page.Items.Single().Body, Is.EqualTo("a mountain"));
            Assert.That(await TitlesAsync(r => r.Filters.SetValue(nameof(Note.Title), "AL*", FilterOperator.Like)), Is.EquivalentTo(new[] { "Alpha", "alpine" }));
            Assert.That(await TitlesAsync(r => r.Filters.SetValue(nameof(Note.Title), "Al", FilterOperator.StartsWith)), Is.EqualTo(new[] { "Alpha" }));
            Assert.That(await TitlesAsync(r => r.Filters.SetValue(nameof(Note.Title), "gamma", FilterOperator.EqualsIgnoreCase)), Is.EqualTo(new[] { "Gamma" }));
            Assert.That(await TitlesAsync(r => r.Filters.SetValue(nameof(Note.Pinned), true)), Is.EquivalentTo(new[] { "Alpha", "Gamma" }));
            Assert.That(await TitlesAsync(r => r.Filters.SetValue(nameof(Note.Size), new[] { 1, 4 }, FilterOperator.In)), Is.EquivalentTo(new[] { "Beta", "Delta" }));
            Assert.That(await TitlesAsync(r => r.Filters.SetValue(nameof(Note.Size), 3, FilterOperator.Gte)), Is.EquivalentTo(new[] { "Alpha", "Gamma", "Delta" }));
            Assert.That(await TitlesAsync(r => r.Filters.SetValue(nameof(Note.Shelf), "garage")), Is.Empty);
        });
    }

    [Test]
    public async Task What_is_not_SQL_is_done_in_memory_with_the_same_answer()
    {
        var expression = await TitlesAsync(r => r.Filters.Add(new Filter { Expression = (System.Linq.Expressions.Expression<Func<Note, bool>>)(n => n.Title.EndsWith('a')) }));
        var sorted = await TitlesAsync(r => r.Sorts.Add(new Sort { Field = nameof(Note.Body) }));
        var search = await TitlesAsync(r => r.FullTextSearchString = "not");
        var count = await _storage.CountAsync(n => n.Size > 2);

        Assert.Multiple(() =>
        {
            Assert.That(expression, Is.EquivalentTo(new[] { "Alpha", "Beta", "Gamma", "Delta" }));
            Assert.That(sorted, Is.EqualTo(new[] { "alpine", "Alpha", "Delta", "Beta", "Gamma" }), "a sort of a property without a column");
            Assert.That(search, Is.EquivalentTo(new[] { "Alpha", "Gamma" }), "full text, a prefix of a word");
            Assert.That(count, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task A_row_is_read_again_by_its_key_without_its_large_fields_when_not_asked_and_nothing_is_written()
    {
        var request = new PagedRequest<Note> { PageSize = 0 };
        request.Fields.Add(nameof(Note.Title));
        request.Filters.SetValue(nameof(Note.Title), "Delta");
        var light = (await _storage.FindAsync(request)).Items.Single();
        var again = await _storage.GetByIdAsync(light.Uid);

        Assert.Multiple(() =>
        {
            Assert.That(light.Body, Is.Null);
            Assert.That(again?.Body, Is.EqualTo("fourth"));
            Assert.That(again?.Shelf, Is.EqualTo("work"));
            Assert.That(async () => await _storage.InsertAsync(new Note()), Throws.TypeOf<NotSupportedException>());
        });
    }

    [Test]
    public async Task A_base_that_is_not_there_reads_as_empty_and_one_of_another_schema_is_refused()
    {
        await using var missing = new SqliteSource(_engine, "missing.sqlite", 3);
        await using var older = new SqliteSource(_engine, "notes.sqlite", 2);
        var empty = await new SqliteStorage<Note>(missing, s_notes).FindAsync(new PagedRequest<Note> { PageSize = 0, ReturnTotal = true });

        Assert.That(empty.Items, Is.Empty);
        Assert.That(empty.Total, Is.EqualTo(0));
        await Assert.ThatAsync(async () => await new SqliteStorage<Note>(older, s_notes).CountAsync(), Throws.TypeOf<SqliteEngineException>());
    }
}
