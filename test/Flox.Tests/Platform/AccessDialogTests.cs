using System.Security.Claims;
using Bunit;
using Flox.Core.Data;
using Flox.Core.Services;
using Flox.Shared.Components;
using Flox.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Flox.Tests.Platform;

// The dialog of an access list over a server it does not see: the owner first, an entry the matrix does not back marked, a sensitive operation
// kept for a role or a user, the whole list saved on OK, and a sensitive grant confirmed first
public class AccessDialogTests
{
    private const string Entity = "Secret";
    private static readonly Guid s_key = Guid.NewGuid();
    private static readonly AclSubject s_doctors = AclSubject.Role("Doctors");

    private sealed class FakeAccessLists(AccessEditor editor) : IAccessListClient
    {
        public List<IReadOnlyCollection<AclEntry>> Saved { get; } = [];

        public Task<IReadOnlyList<AclEntry>> GetAsync(string entity, Guid key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(editor.Entries);
        }

        public Task<IReadOnlyList<AclEntry>> SetAsync(string entity, Guid key, IReadOnlyCollection<AclEntry> acl, CancellationToken cancellationToken = default)
        {
            Saved.Add(acl);
            return Task.FromResult<IReadOnlyList<AclEntry>>([.. acl]);
        }

        public Task<AccessEditor> GetEditorAsync(string entity, Guid key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(editor);
        }

        public Task<IReadOnlyList<AccessSubjectInfo>> FindPrincipalsAsync(string entity, string? search, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AccessSubjectInfo>>([]);
        }

        public Task<IReadOnlyList<AccessSubjectInfo>> DescribeAsync(string entity, IReadOnlyCollection<AclSubject> subjects, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AccessSubjectInfo>>([.. subjects.Select(row => new AccessSubjectInfo(row, row.Key, []))]);
        }
    }

    // Read, Mask and the sensitive Reveal; the owner reads, Doctors may Reveal and Mask by the list but hold only Reveal in the matrix; Everyone reads
    private static AccessEditor Editor()
    {
        return new AccessEditor(Entity, s_key,
                                [new AccessOperation("Read", false), new AccessOperation("Mask", false), new AccessOperation("Reveal", true)],
                                [new AclEntry(AclSubject.Owner, "Read"), new AclEntry(s_doctors, "Mask"), new AclEntry(AclSubject.Everyone, "Read")],
                                [new AccessSubjectInfo(AclSubject.Owner, "alice", ["Read", "Mask"]), new AccessSubjectInfo(s_doctors, "Doctors", ["Read", "Reveal"]), new AccessSubjectInfo(AclSubject.Everyone, "everyone", ["Read"])],
                                ["user:me", "role:DOCTORS"]);
    }

    private static IRenderedComponent<AccessDialog> Render(BunitContext context, FakeAccessLists lists)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        context.Services.AddSingleton<IAccessListClient>(lists);
        return context.Render<AccessDialog>(parameters => parameters.Add(row => row.Entity, Entity).Add(row => row.Key, s_key));
    }

    // The checkbox of a subject and an operation, by the position of the operation in the editor
    private static AngleSharp.Dom.IElement Box(IRenderedComponent<AccessDialog> dialog, AclSubject subject, int operation)
    {
        return dialog.Find($"tr[data-subject='{subject.Key}'] td:nth-child({operation + 2}) button[role=checkbox]");
    }

    [Test]
    public async Task TheOwnerComesFirst_AnEntryWithoutTheMatrixIsMarked_AndASensitiveOperationIsKeptForARoleOrAUser()
    {
        await using var context = new BunitContext();
        var dialog = Render(context, new FakeAccessLists(Editor()));

        var rows = dialog.FindAll("tbody tr").Select(row => row.GetAttribute("data-subject")).ToList();
        var doctorsMarks = dialog.FindAll($"tr[data-subject='{s_doctors.Key}'] .ineffective");
        var everyoneReveal = Box(dialog, AclSubject.Everyone, 2);
        var doctorsReveal = Box(dialog, s_doctors, 2);

        Assert.Multiple(() =>
        {
            Assert.That(rows, Is.EqualTo(new[] { "owner", s_doctors.Key, "everyone" }));
            Assert.That(doctorsMarks, Has.Count.EqualTo(1), "Mask of Doctors has no effect: the matrix does not give it");
            Assert.That(dialog.Find($"tr[data-subject='owner']").TextContent, Does.Contain("alice"));
            Assert.That(everyoneReveal.HasAttribute("disabled"), Is.True, "a sensitive operation is not given to everyone");
            Assert.That(doctorsReveal.HasAttribute("disabled"), Is.False);
            Assert.That(dialog.FindAll($"tr[data-subject='owner'] button:not([role=checkbox])"), Is.Empty, "the owner is not removed");
        });
    }

    [Test]
    public async Task OK_SavesTheWholeList_AsChanged()
    {
        await using var context = new BunitContext();
        var lists = new FakeAccessLists(Editor());
        var dialog = Render(context, lists);

        await Box(dialog, AclSubject.Owner, 1).ClickAsync(new());
        await dialog.Find($"tr[data-subject='everyone'] button:not([role=checkbox])").ClickAsync(new());
        await dialog.Find(".access-ok").ClickAsync(new());

        Assert.That(lists.Saved.Single().Select(row => $"{row.Subject.Key}:{row.Operation}"), Is.EquivalentTo(new[] { "owner:Mask", "owner:Read", $"{s_doctors.Key}:Mask" }));
    }

    [Test]
    public async Task ASensitiveGrant_IsConfirmedFirst_AndToldWhenTheCallerGivesItToItself()
    {
        await using var context = new BunitContext();
        var lists = new FakeAccessLists(Editor());
        var dialog = Render(context, lists);
        var dialogs = context.Services.GetRequiredService<FloxDialogService>();
        string? asked = null;
        dialogs.Changed += (_, _) =>
        {
            if (asked is null && dialogs.Dialogs.Count > 0 && dialogs.Dialogs[^1].Parameters.TryGetValue(nameof(MessageDialog.Message), out var message))
            {
                asked = message as string;
            }
        };

        await Box(dialog, s_doctors, 2).ClickAsync(new());
        var saving = dialog.Find(".access-ok").ClickAsync(new());
        var savedBeforeTheAnswer = lists.Saved.Count;
        await dialog.InvokeAsync(() => dialogs.Close(true));
        await saving;

        Assert.Multiple(() =>
        {
            Assert.That(savedBeforeTheAnswer, Is.Zero, "nothing is saved before the answer");
            Assert.That(asked, Does.Contain("Reveal").And.Contain(Flox.Resources.Strings.Access_ConfirmSelf), "the caller is a Doctor");
            Assert.That(lists.Saved.Single().Select(row => $"{row.Subject.Key}:{row.Operation}"), Does.Contain($"{s_doctors.Key}:Reveal"));
        });
    }

    private sealed class FakeRuntime : FloxRuntime
    {
        public override FloxPlatform Platform => FloxPlatform.Wasm;

        public override string EnvironmentName => "Production";

        public bool Server { get; init; }

        public override bool HasServer => Server;
    }

    private sealed class AllowAll : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            return Task.FromResult(AuthorizationResult.Success());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
        {
            return Task.FromResult(AuthorizationResult.Success());
        }
    }

    // Lite has no server and no access lists: the button is hidden and the service opens nothing, whatever the matrix says
    [TestCase(false)]
    [TestCase(true)]
    public async Task TheButton_IsShownOnlyInAnApplicationWithAServer(bool shown)
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        context.Services.AddSingleton<IFloxRuntime>(new FakeRuntime { Server = shown });
        context.Services.AddSingleton<IAuthorizationService, AllowAll>();
        context.Services.AddScoped<IAccessDialogService, AccessDialogService>();
        var opened = 0;
        var dialogs = context.Services.GetRequiredService<FloxDialogService>();
        dialogs.Changed += (_, _) => opened = dialogs.Dialogs.Count;
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u")], "test"));

        var button = context.Render<AccessButton>(parameters => parameters.Add(row => row.Entity, Entity)
                                                                          .Add(row => row.Key, s_key)
                                                                          .AddCascadingValue(Task.FromResult(new AuthenticationState(user))));
        _ = context.Services.GetRequiredService<IAccessDialogService>().EditAsync(Entity, s_key);

        Assert.Multiple(() =>
        {
            Assert.That(button.FindAll(".access-button"), Has.Count.EqualTo(shown ? 1 : 0));
            Assert.That(opened, Is.EqualTo(shown ? 1 : 0));
        });
    }
}
