using System.Globalization;
using Flox.Shared.Services;
using Microsoft.AspNetCore.Components;
using BlazorBlueprint.Primitives;

namespace Flox.Shared.Components;

/// <summary>
/// The editor of the access list of one row: who may do which operation, the owner first. It reads the list and what the permission matrix
/// gives each subject from the server, shows an entry the matrix does not back as one without effect, keeps a sensitive operation for a role or
/// a user, asks before it gives a sensitive operation, and saves the whole list on OK. Opened by <see cref="IAccessDialogService"/>.
/// </summary>
public partial class AccessDialog
{
    private AccessEditor? _editor;
    private readonly List<SubjectRow> _rows = [];
    private List<Choice> _choices = [];
    private string? _choice;
    private string? _error;
    private bool _busy;

    /// <summary>The name of the entity, as its permissions name it.</summary>
    [Parameter]
    [EditorRequired]
    public string Entity { get; set; } = default!;

    /// <summary>The key of the row.</summary>
    [Parameter]
    [EditorRequired]
    public Guid Key { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _editor = await AccessLists.GetEditorAsync(Entity, Key);
            foreach (var subject in _editor.Subjects)
            {
                _rows.Add(new SubjectRow(subject, [.. _editor.Entries.Where(entry => entry.Subject == subject.Subject).Select(entry => entry.Operation)]));
            }

            _choices = FixedChoices();
        }
        catch (Exception exception) when (exception is ForbidException or NotFoundException or ArgumentException or NotSupportedException or UnauthenticatedException or HttpRequestException)
        {
            _error = Describe(exception);
        }
    }

    private List<Choice> FixedChoices()
    {
        return [new Choice(AclSubject.Everyone.Key, Strings.Access_Everyone), new Choice(AclSubject.Authenticated.Key, Strings.Access_Authenticated)];
    }

    private IEnumerable<SelectOption<string>> ChoiceOptions => _choices.Select(row => new SelectOption<string>(row.Key, row.Text));

    private async Task ChooseAsync(string? key)
    {
        _choice = key;
        await AddAsync();
    }

    private async Task SearchAsync(string? query)
    {
        var text = query ?? string.Empty;
        List<Choice> found = FixedChoices();
        try
        {
            found.AddRange((await AccessLists.FindPrincipalsAsync(Entity, text)).Select(row => new Choice(row.Subject.Key, Label(row.Subject, row.DisplayName), row)));
        }
        catch (Exception exception) when (exception is ForbidException or ArgumentException or NotSupportedException or HttpRequestException)
        {
            _error = Describe(exception);
        }

        _choices = [.. found.Where(row => _rows.All(present => present.Subject.Key != row.Key))
                            .Where(row => text.Length == 0 || row.Text.Contains(text, StringComparison.CurrentCultureIgnoreCase) || row.Info is not null)];
    }

    // A subject added once, described by the server when the directory did not describe it already
    private async Task AddAsync()
    {
        var picked = _choices.FirstOrDefault(row => row.Key == _choice);
        _choice = null;
        if (picked is null || _rows.Any(row => row.Subject.Key == picked.Key))
        {
            return;
        }

        try
        {
            var info = picked.Info ?? (await AccessLists.DescribeAsync(Entity, [AclSubject.Parse(picked.Key)])).Single();
            _rows.Add(new SubjectRow(info, []));
            _error = null;
        }
        catch (Exception exception) when (exception is ForbidException or ArgumentException or NotSupportedException or HttpRequestException)
        {
            _error = Describe(exception);
        }
    }

    private static void Toggle(SubjectRow row, string operation, bool value)
    {
        if (value)
        {
            row.Checked.Add(operation);
        }
        else
        {
            row.Checked.Remove(operation);
        }
    }

    // A sensitive operation goes to a role or a user by name, never to everyone, the signed-in users or the owner of the row
    private static bool MayGive(SubjectRow row, AccessOperation operation)
    {
        return !operation.Sensitive || row.Subject.Kind is AclSubjectKind.Role or AclSubjectKind.User || row.Checked.Contains(operation.Name);
    }

    private async Task SaveAsync()
    {
        if (_editor is null)
        {
            return;
        }

        List<AclEntry> entries = [.. _rows.SelectMany(row => row.Checked.Order(StringComparer.Ordinal).Select(operation => new AclEntry(row.Subject, operation)))];
        var sensitive = _editor.Operations.Where(row => row.Sensitive).Select(row => row.Name).ToHashSet(StringComparer.Ordinal);
        var added = entries.Where(entry => sensitive.Contains(entry.Operation) && !_editor.Entries.Contains(entry)).ToList();
        if (added.Count != 0)
        {
            var list = string.Join(", ", added.Select(entry => $"{Label(entry.Subject)}: {entry.Operation}"));
            var message = string.Format(CultureInfo.CurrentCulture, Strings.Access_ConfirmSensitive, list);
            if (added.Any(entry => _editor.CallerSubjects.Contains(entry.Subject.Key)))
            {
                message += Environment.NewLine + Strings.Access_ConfirmSelf;
            }

            if (await DialogService.ShowMessageBoxAsync(null, message, Strings.Caption_Ok, null, Strings.Caption_Cancel) != true)
            {
                return;
            }
        }

        _busy = true;
        try
        {
            await AccessLists.SetAsync(Entity, Key, entries);
            DialogService.Close(true);
        }
        catch (Exception exception) when (exception is ForbidException or NotFoundException or ArgumentException or NotSupportedException or UnauthenticatedException or HttpRequestException)
        {
            _error = Describe(exception);
        }
        finally
        {
            _busy = false;
        }
    }

    private static string Describe(Exception exception)
    {
        return exception switch
        {
            ForbidException => Strings.Error_AccessDenied,
            HttpRequestException => Strings.Error_NetworkUnavailable,
            var _ => exception.Message
        };
    }

    private string Label(SubjectRow row)
    {
        return Label(row.Subject, row.DisplayName);
    }

    private string Label(AclSubject subject)
    {
        return Label(subject, _rows.FirstOrDefault(row => row.Subject == subject)?.DisplayName ?? subject.Name ?? subject.Key);
    }

    private static string Label(AclSubject subject, string displayName)
    {
        return subject.Kind switch
        {
            AclSubjectKind.Owner when displayName == subject.Key => Strings.Access_Owner,
            AclSubjectKind.Owner => string.Format(CultureInfo.CurrentCulture, Strings.Access_OwnerOf, displayName),
            AclSubjectKind.Everyone => Strings.Access_Everyone,
            AclSubjectKind.Authenticated => Strings.Access_Authenticated,
            AclSubjectKind.Role => string.Format(CultureInfo.CurrentCulture, Strings.Access_Role, displayName),
            _ when displayName == subject.Name => string.Format(CultureInfo.CurrentCulture, Strings.Access_UnknownUser, displayName),
            var _ => displayName
        };
    }

    private string Ineffective(SubjectRow row, string operation)
    {
        return string.Format(CultureInfo.CurrentCulture, Strings.Access_Ineffective, operation, Label(row));
    }

    // A subject of the list being edited: what the matrix gives it and the operations checked
    private sealed class SubjectRow(AccessSubjectInfo info, HashSet<string> checkedOperations)
    {
        public AclSubject Subject { get; } = info.Subject;

        public string DisplayName { get; } = info.DisplayName;

        public HashSet<string> Granted { get; } = [.. info.Granted];

        public HashSet<string> Checked { get; } = checkedOperations;
    }

    private sealed record Choice(string Key, string Text, AccessSubjectInfo? Info = null);
}
