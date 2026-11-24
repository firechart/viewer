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
        var expression = await TitlesAsync(r => r.Filters.Add(new Filter { Expression = (System.Linq.Expressions.Expression<Func<Note, bool>>)(n => n.Title.EndsWith('a', StringComparison.Ordinal)) }));
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
    public async Task Text_is_sorted_and_compared_without_the_case_the_same_in_SQL_and_in_memory()
    {
        await using (var db = await _engine.OpenAsync("words.sqlite", SqliteAccess.ReadWrite))
        {
            await db.ExecuteAsync("PRAGMA user_version = 3");
            await db.ExecuteBatchAsync([
                new SqliteStatement("CREATE TABLE note (id INTEGER PRIMARY KEY, shelf TEXT NOT NULL, title TEXT NOT NULL, size INTEGER NOT NULL, pinned INTEGER NOT NULL, body TEXT NOT NULL)"),
                new SqliteStatement("CREATE VIRTUAL TABLE note_fts USING fts5 (title, body, content = 'note', content_rowid = 'id')"),
                .. new[] { "Zebra", "\u0451\u0436", "apple", "\u0401\u043b\u043a\u0430", "Banana" }
                    .Select(title => new SqliteStatement("INSERT INTO note (shelf, title, size, pinned, body) VALUES ('x', $t, 0, 0, $t)", new Dictionary<string, object?> { ["$t"] = title }))
            ]);
        }

        await using var source = new SqliteSource(_engine, "words.sqlite", 3);
        var storage = new SqliteStorage<Note>(source, s_notes);
        async Task<List<string>> Titles(Action<PagedRequest<Note>> build)
        {
            var request = new PagedRequest<Note> { PageSize = 0 };
            build(request);
            return [.. (await storage.FindAsync(request)).Items.Select(n => n.Title)];
        }

        var expected = new[] { "apple", "Banana", "Zebra", "\u0401\u043b\u043a\u0430", "\u0451\u0436" };
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await Titles(r => r.Sorts.Add(new Sort { Field = nameof(Note.Title) })), Is.EqualTo(expected), "a column, sorted by SQL");
            Assert.That(await Titles(r => r.Sorts.Add(new Sort { Field = nameof(Note.Body) })), Is.EqualTo(expected), "no column, sorted in memory");
            Assert.That(await Titles(r => r.Filters.SetValue(nameof(Note.Title), "\u0451", FilterOperator.StartsWithIgnoreCase)),
                        Is.EquivalentTo(new[] { "\u0451\u0436", "\u0401\u043b\u043a\u0430" }), "letters SQLite does not fold");
            Assert.That(await Titles(r => r.Filters.SetValue(nameof(Note.Title), "\u0401\u0416", FilterOperator.EqualsIgnoreCase)), Is.EqualTo(new[] { "\u0451\u0436" }));
            Assert.That(await Titles(r => r.Filters.SetValue(nameof(Note.Title), "\u0451\u041b*", FilterOperator.Like)), Is.EqualTo(new[] { "\u0401\u043b\u043a\u0430" }));
        });
    }

    [Test]
    public async Task A_base_closed_under_a_request_is_asked_for_again_and_a_disposed_source_has_none()
    {
        SqliteSource? source = null;
        var closing = new ClosingEngine(_engine, () => source!.ResetAsync());
        source = new SqliteSource(closing, "notes.sqlite", 3);
        var storage = new SqliteStorage<Note>(source, s_notes);

        var found = await storage.FindAsync(new PagedRequest<Note> { PageSize = 0, ReturnTotal = true });
        Assert.That(found.Total, Is.EqualTo(5), "the base was closed before the first query of the request and opened again");
        Assert.That(closing.Closed, Is.EqualTo(1));

        await source.DisposeAsync();
        Assert.That((await storage.FindAsync(new PagedRequest<Note> { PageSize = 0 })).Items, Is.Empty);
        Assert.That(await storage.GetByIdAsync(SqliteKeys.Of(7, 1)), Is.Null);
    }

    [Test]
    public void A_value_of_NET_becomes_one_of_SQLite_or_is_refused()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SqliteValues.Normalize((short)5), Is.EqualTo(5L));
            Assert.That(SqliteValues.Normalize(true), Is.EqualTo(1L));
            Assert.That(SqliteValues.Normalize(1.5m), Is.EqualTo(1.5d));
            Assert.That(SqliteValues.Normalize(FilterOperator.Like), Is.EqualTo("Like"));
            Assert.That(SqliteValues.Normalize(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.FromHours(3))), Is.EqualTo("2026-11-18T09:00:00.0000000+00:00"));
            Assert.That(SqliteValues.Normalize(new DateOnly(2026, 11, 18)), Is.EqualTo("2026-11-18"));
            Assert.That(() => SqliteValues.Normalize(TimeSpan.FromSeconds(1)), Throws.ArgumentException);
        });
    }

    // Closes the base of the source before the first query of a database it opened
    private sealed class ClosingEngine(ISqliteEngine inner, Func<Task> close) : ISqliteEngine
    {
        public int Closed { get; private set; }

        public async Task<ISqliteDatabase> OpenAsync(string name, SqliteAccess access, CancellationToken cancellationToken = default)
        {
            var database = await inner.OpenAsync(name, access, cancellationToken);
            return Closed == 0 ? new ClosingDatabase(database, this, close) : database;
        }

        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
        {
            return inner.ExistsAsync(name, cancellationToken);
        }

        public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            return inner.DeleteAsync(name, cancellationToken);
        }

        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
        {
            return inner.ListAsync(cancellationToken);
        }

        public Task<long> ImportAsync(string name, Uri source, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            return inner.ImportAsync(name, source, progress, cancellationToken);
        }

        private sealed class ClosingDatabase(ISqliteDatabase database, ClosingEngine engine, Func<Task> close) : ISqliteDatabase
        {
            private bool _versionRead;

            public string Name => database.Name;

            public SqliteAccess Access => database.Access;

            public async Task<SqliteRows> QueryAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
            {
                // The source reads the version first; the next query is the request
                if (_versionRead && engine.Closed == 0)
                {
                    engine.Closed++;
                    await close();
                }

                _versionRead = true;
                return await database.QueryAsync(sql, parameters, cancellationToken);
            }

            public Task<int> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
            {
                return database.ExecuteAsync(sql, parameters, cancellationToken);
            }

            public Task<IReadOnlyList<int>> ExecuteBatchAsync(IReadOnlyList<SqliteStatement> statements, CancellationToken cancellationToken = default)
            {
                return database.ExecuteBatchAsync(statements, cancellationToken);
            }

            public ValueTask DisposeAsync()
            {
                return database.DisposeAsync();
            }
        }
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
