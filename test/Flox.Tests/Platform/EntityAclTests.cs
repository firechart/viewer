using System.Security.Claims;
using System.Text.Json;
using Flox.Core;
using Flox.Core.Authorization;
using Flox.Core.Data;

namespace Flox.Tests.Platform;

// The rule of the access lists of the rows (EntityAcl) without a database: who a subject is, what a list allows, the administrator and the
// sensitive operations, a table without lists, and the old rights of the rows translated without losses
public class EntityAclTests
{
    private const string OwnerId = "owner-1";

    private sealed class Row : GuidEntity;

    private sealed class Lookup : GuidTableEntity;

    private sealed class Line : GuidDetailEntity<Row>
    {
        [AclMaster]
        [Index]
        public Guid RowId { get; set; }

        public override Guid MasterId
        {
            get
            {
                return RowId;
            }
        }
    }

    private static ClaimsPrincipal User(string id, params string[] roles)
    {
        return new ClaimsPrincipal(new ClaimsIdentity([new Claim(Constants.SUB_CLAIM, id), .. roles.Select(role => new Claim(ClaimTypes.Role, role))], "test"));
    }

    private static readonly ClaimsPrincipal s_anonymous = new(new ClaimsIdentity());

    private static EntityAccessDescriptor Descriptor(params string[] sensitive)
    {
        return EntityPermissions.Register(typeof(Row), new EntityAccessOptions { Operations = ["Pseudonymize", "Depseudonymize"], Sensitive = sensitive });
    }

    [Test]
    public void ASubject_HasAKey_ThatReadsBack_AndARoleIsComparedWithoutCase()
    {
        AclSubject[] subjects = [AclSubject.Owner, AclSubject.Everyone, AclSubject.Authenticated, AclSubject.Role("Doctors"), AclSubject.User("Ab-12")];

        Assert.Multiple(() =>
        {
            Assert.That(subjects.Select(row => row.Key), Is.EqualTo(new[] { "owner", "everyone", "authenticated", "role:DOCTORS", "user:Ab-12" }));
            Assert.That(subjects.Select(row => AclSubject.Parse(row.Key)), Is.EqualTo(subjects));
            Assert.That(AclSubject.Role("doctors"), Is.EqualTo(AclSubject.Role("DOCTORS")));
            Assert.That(AclSubject.User("ab-12"), Is.Not.EqualTo(AclSubject.User("Ab-12")), "the id of a user is compared as it is");
        });
    }

    [TestCase("")]
    [TestCase("role:")]
    [TestCase("user:")]
    [TestCase("group:x")]
    [TestCase("Owner")]
    public void AKeyThatIsNoSubject_IsRefused(string key)
    {
        Assert.That(() => AclSubject.Parse(key), Throws.InstanceOf<FormatException>());
    }

    [Test]
    public void AnEntry_IsWrittenInJson_WithTheKeyOfItsSubject()
    {
        var response = new EntityAccessList([new AclEntry(AclSubject.Role("Doctors"), EntityOperations.Read)]);

        var json = JsonSerializer.Serialize(response, JsonSerializerOptions.Web);

        Assert.Multiple(() =>
        {
            Assert.That(json, Is.EqualTo("""{"entries":[{"subject":"role:DOCTORS","operation":"Read"}]}"""));
            Assert.That(JsonSerializer.Deserialize<EntityAccessList>(json, JsonSerializerOptions.Web)!.Entries, Is.EqualTo(response.Entries));
            Assert.That(() => JsonSerializer.Deserialize<EntityAccessList>("""{"entries":[{"subject":"nobody","operation":"Read"}]}""", JsonSerializerOptions.Web), Throws.InstanceOf<JsonException>());
        });
    }

    [Test]
    public void TheSubjectsOfACaller_AreEveryone_AndForASignedInOne_AuthenticatedTheUserAndTheRoles()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EntityAcl.SubjectKeysOf(s_anonymous), Is.EqualTo(new[] { "everyone" }));
            Assert.That(EntityAcl.SubjectKeysOf(User("u1", "Doctors", "Nurses")), Is.EqualTo(new[] { "everyone", "authenticated", "user:u1", "role:DOCTORS", "role:NURSES" }));
        });
    }

    [TestCase(AclSubjectKind.Everyone, null, "anonymous", true)]
    [TestCase(AclSubjectKind.Everyone, null, "stranger", true)]
    [TestCase(AclSubjectKind.Authenticated, null, "anonymous", false)]
    [TestCase(AclSubjectKind.Authenticated, null, "stranger", true)]
    [TestCase(AclSubjectKind.Owner, null, "owner", true)]
    [TestCase(AclSubjectKind.Owner, null, "stranger", false)]
    [TestCase(AclSubjectKind.Owner, null, "anonymous", false)]
    [TestCase(AclSubjectKind.Role, "Doctors", "doctor", true)]
    [TestCase(AclSubjectKind.Role, "doctors", "doctor", true)]
    [TestCase(AclSubjectKind.Role, "Doctors", "stranger", false)]
    [TestCase(AclSubjectKind.User, "u-7", "named", true)]
    [TestCase(AclSubjectKind.User, "u-7", "stranger", false)]
    [TestCase(AclSubjectKind.User, "u-7", "anonymous", false)]
    public void AnEntry_IsForTheCallersItNames(AclSubjectKind kind, string? name, string caller, bool matches)
    {
        var subject = kind switch
        {
            AclSubjectKind.Role => AclSubject.Role(name!),
            AclSubjectKind.User => AclSubject.User(name!),
            AclSubjectKind.Owner => AclSubject.Owner,
            AclSubjectKind.Everyone => AclSubject.Everyone,
            _ => AclSubject.Authenticated
        };
        var user = caller switch
        {
            "anonymous" => s_anonymous,
            "owner" => User(OwnerId),
            "doctor" => User("d1", "Doctors"),
            "named" => User("u-7"),
            _ => User("s1", "Nurses")
        };

        Assert.That(EntityAcl.Matches(subject, OwnerId, user), Is.EqualTo(matches));
    }

    [Test]
    public void AnAnonymousCaller_OwnsNothing_EvenARowOfTheEmptyOwner()
    {
        Assert.That(EntityAcl.Matches(AclSubject.Owner, s_anonymous.GetUserId(), s_anonymous), Is.False);
    }

    [Test]
    public void AList_AllowsTheOperationsOfItsEntries_OnlyToTheirSubjects()
    {
        AclEntry[] acl = [new AclEntry(AclSubject.Owner, EntityOperations.Update), new AclEntry(AclSubject.Role("Doctors"), "Pseudonymize")];

        Assert.Multiple(() =>
        {
            Assert.That(EntityAcl.Allows(acl, OwnerId, User(OwnerId), EntityOperations.Update), Is.True);
            Assert.That(EntityAcl.Allows(acl, OwnerId, User(OwnerId), EntityOperations.Read), Is.False, "an operation is not implied by another one");
            Assert.That(EntityAcl.Allows(acl, OwnerId, User("d1", "Doctors"), "Pseudonymize"), Is.True);
            Assert.That(EntityAcl.Allows(acl, OwnerId, User("d1", "Doctors"), EntityOperations.Update), Is.False);
            Assert.That(EntityAcl.Allows([], OwnerId, User(OwnerId), EntityOperations.Read), Is.False, "an empty list allows nothing");
        });
    }

    [Test]
    public void TheAdministrator_PassesTheList_ButNotForASensitiveOperation()
    {
        var descriptor = Descriptor(sensitive: "Depseudonymize");
        var administrator = User("a1", AppRoles.ADMINISTRATOR);

        Assert.Multiple(() =>
        {
            Assert.That(EntityAcl.RowAllows(descriptor, [], OwnerId, administrator, EntityOperations.Delete), Is.True);
            Assert.That(EntityAcl.RowAllows(descriptor, [], OwnerId, administrator, "Pseudonymize"), Is.True);
            Assert.That(EntityAcl.RowAllows(descriptor, [], OwnerId, administrator, "Depseudonymize"), Is.False, "a sensitive operation needs an entry");
            Assert.That(EntityAcl.RowAllows(descriptor, [new AclEntry(AclSubject.Role(AppRoles.ADMINISTRATOR), "Depseudonymize")], OwnerId, administrator, "Depseudonymize"), Is.True);
            Assert.That(EntityAcl.RowAllows(descriptor, [new AclEntry(AclSubject.User("a1"), "Depseudonymize")], OwnerId, administrator, "Depseudonymize"), Is.True);
            Assert.That(EntityAcl.RowAllows(descriptor, [new AclEntry(AclSubject.User("d1"), "Depseudonymize")], OwnerId, administrator, "Depseudonymize"), Is.False, "the entry of another user");
            Assert.That(EntityAcl.RowAllows(descriptor, [], OwnerId, User("d1", "Doctors"), EntityOperations.Read), Is.False, "anyone else needs an entry");
            Assert.That(EntityAcl.RowAllows(descriptor, [new AclEntry(AclSubject.Owner, "Depseudonymize"), new AclEntry(AclSubject.Authenticated, "Depseudonymize")], OwnerId, User(OwnerId), "Depseudonymize"), Is.False,
                        "an entry of the owner or of all for a sensitive operation (one stored before the operation became sensitive) gives nothing");
        });
    }

    [Test]
    public void ATableWithoutLists_AllowsEveryOperationOfItsRows_ThePermissionsDecideAlone()
    {
        var descriptor = EntityPermissions.Register(typeof(Lookup), new EntityAccessOptions());

        Assert.Multiple(() =>
        {
            Assert.That(EntityAcl.RowAllows(descriptor, [], OwnerId, s_anonymous, EntityOperations.Read), Is.True);
            Assert.That(EntityAcl.RowAllows(descriptor, [], OwnerId, User("s1"), EntityOperations.Delete), Is.True);
        });
    }

    // A detail has no list: the list and the owner of its master decide, Read of the master for reading it, Update of the master for the rest
    [Test]
    public void ADetail_FollowsTheListOfItsMaster_ReadForReading_UpdateForTheRest()
    {
        var descriptor = EntityPermissions.Register(typeof(Line), new EntityAccessOptions { Operations = ["Sign"], Sensitive = ["Sign"] });
        AclEntry[] master = [new AclEntry(AclSubject.Role("Doctors"), EntityOperations.Read), new AclEntry(AclSubject.Owner, EntityOperations.Update)];
        var doctor = User("d1", "Doctors");
        var owner = User(OwnerId);

        Assert.Multiple(() =>
        {
            Assert.That(EntityAcl.MasterOperation(EntityOperations.Read), Is.EqualTo(EntityOperations.Read));
            Assert.That(EntityAcl.MasterOperation(EntityOperations.Delete), Is.EqualTo(EntityOperations.Update));
            Assert.That(EntityAcl.MasterOperation(EntityOperations.Create), Is.EqualTo(EntityOperations.Update));
            Assert.That(EntityAcl.RowAllows(descriptor, master, OwnerId, doctor, EntityOperations.Read), Is.True, "the master gives the doctors Read");
            Assert.That(EntityAcl.RowAllows(descriptor, master, OwnerId, doctor, EntityOperations.Update), Is.False, "but not Update");
            Assert.That(EntityAcl.RowAllows(descriptor, master, OwnerId, owner, EntityOperations.Create), Is.True, "the owner of the master changes it, so creates its details");
            Assert.That(EntityAcl.RowAllows(descriptor, master, OwnerId, owner, EntityOperations.Read), Is.False, "an operation is not implied by another one");
            Assert.That(EntityAcl.RowAllows(descriptor, [], OwnerId, User("a1", AppRoles.ADMINISTRATOR), EntityOperations.Delete), Is.True, "the administrator passes");
            Assert.That(EntityAcl.RowAllows(descriptor, [], OwnerId, User("a1", AppRoles.ADMINISTRATOR), "Sign"), Is.False, "unless the operation of the detail is sensitive");
        });
    }

    [Test]
    public void TheKeyOfARow_IsTheTextOfItsUid()
    {
        var row = new Row { Uid = Guid.Parse("0198A2B3-0000-7000-8000-00000000000A") };

        Assert.Multiple(() =>
        {
            Assert.That(EntityAcl.KeyOf(row), Is.EqualTo("0198a2b3-0000-7000-8000-00000000000a"), "the text PostgreSQL gives a uuid");
            Assert.That(EntityAcl.KeyText(row.Uid), Is.EqualTo(EntityAcl.KeyOf(row)));
        });
    }

    private static string[] Keys(IEnumerable<AclEntry> acl)
    {
        return [.. acl.Select(row => $"{row.Subject.Key}:{row.Operation}").Order(StringComparer.Ordinal)];
    }

    // The levels of before: 0 Owner, 1 Public, 2 Role, 3 Authenticated; the JSON of a document held their numbers or their names
    [TestCase("0", null, "0", null, new[] { "owner:Delete", "owner:ManageAccess", "owner:Read", "owner:Update" })]
    [TestCase("Owner", null, "Owner", null, new[] { "owner:Delete", "owner:ManageAccess", "owner:Read", "owner:Update" })]
    [TestCase(null, null, null, null, new[] { "owner:Delete", "owner:ManageAccess", "owner:Read", "owner:Update" })]
    [TestCase("1", null, "0", null, new[] { "everyone:Read", "owner:Delete", "owner:ManageAccess", "owner:Read", "owner:Update" })]
    [TestCase("Public", null, "Role", "Administrator", new[] { "everyone:Read", "owner:Read", "role:ADMINISTRATOR:Delete", "role:ADMINISTRATOR:Update" })]
    [TestCase("1", null, "2", "Administrator", new[] { "everyone:Read", "owner:Read", "role:ADMINISTRATOR:Delete", "role:ADMINISTRATOR:Update" })]
    [TestCase("3", null, "3", null, new[] { "authenticated:Delete", "authenticated:Read", "authenticated:Update", "owner:Read" })]
    [TestCase("Authenticated", null, "Owner", null, new[] { "authenticated:Read", "owner:Delete", "owner:ManageAccess", "owner:Read", "owner:Update" })]
    [TestCase("2", "Doctors", "2", "Nurses", new[] { "owner:Read", "role:DOCTORS:Read", "role:NURSES:Delete", "role:NURSES:Update" })]
    [TestCase("Role", null, "Owner", null, new[] { "owner:Delete", "owner:ManageAccess", "owner:Read", "owner:Update" })]
    [TestCase("0", null, "1", null, new[] { "owner:Read" })]
    public void TheOldRightsOfARow_BecomeItsAccessList(string? readAccess, string? readRole, string? writeAccess, string? writeRole, string[] expected)
    {
        Assert.That(Keys(EntityAcl.FromLegacy(readAccess, readRole, writeAccess, writeRole)), Is.EqualTo(expected));
    }

    // The old rule on every kind of caller against the new list: the same answer for reading and writing (writing needed reading, as the
    // storage still asks for both)
    [Test]
    public void TheTranslation_LosesNothing_ForEveryCallerAndEveryLevel()
    {
        string[] levels = ["0", "1", "2", "3"];
        ClaimsPrincipal[] callers = [s_anonymous, User(OwnerId), User("d1", "Doctors"), User("n1", "Nurses"), User("s1")];
        foreach (var read in levels)
        {
            foreach (var write in levels.Where(row => row != "1"))
            {
                var acl = EntityAcl.FromLegacy(read, read == "2" ? "Doctors" : null, write, write == "2" ? "Nurses" : null);
                foreach (var caller in callers)
                {
                    var authenticated = caller.IsAuthenticated();
                    var owner = authenticated && caller.GetUserId() == OwnerId;
                    var roles = caller.GetRoles();
                    var oldRead = read == "1" || (authenticated && (owner || read == "3" || (read == "2" && roles.Contains("Doctors"))));
                    var oldWrite = authenticated && oldRead && ((write == "0" && owner) || write == "3" || (write == "2" && roles.Contains("Nurses")));

                    var newRead = EntityAcl.Allows(acl, OwnerId, caller, EntityOperations.Read);
                    var newWrite = newRead && EntityAcl.Allows(acl, OwnerId, caller, EntityOperations.Update) && EntityAcl.Allows(acl, OwnerId, caller, EntityOperations.Delete);
                    Assert.That((newRead, newWrite), Is.EqualTo((oldRead, oldWrite)), $"read {read}, write {write}, caller {caller.GetUserId()} {string.Join(",", roles)}");
                }
            }
        }
    }

    [Test]
    public void AChangeOfAList_TellsWhatItAdded_AndASensitiveOperationAnAuthorGaveToThemselves()
    {
        Descriptor(sensitive: "Depseudonymize");
        var author = User("a1", AppRoles.ADMINISTRATOR);
        var change = new EntityAccessChange(typeof(Row),
                                            "k",
                                            [new AclEntry(AclSubject.Owner, EntityOperations.Read)],
                                            [new AclEntry(AclSubject.Role(AppRoles.ADMINISTRATOR), "Depseudonymize"), new AclEntry(AclSubject.User("d1"), "Depseudonymize"), new AclEntry(AclSubject.User("d1"), "Pseudonymize")],
                                            author);

        Assert.Multiple(() =>
        {
            Assert.That(Keys(change.Added), Is.EqualTo(new[] { "role:ADMINISTRATOR:Depseudonymize", "user:d1:Depseudonymize", "user:d1:Pseudonymize" }));
            Assert.That(Keys(change.Removed), Is.EqualTo(new[] { "owner:Read" }));
            Assert.That(Keys(change.SensitiveAdded), Is.EqualTo(new[] { "role:ADMINISTRATOR:Depseudonymize", "user:d1:Depseudonymize" }));
            Assert.That(Keys(change.SensitiveSelfGranted(OwnerId)), Is.EqualTo(new[] { "role:ADMINISTRATOR:Depseudonymize" }));
        });
    }

    [Test]
    public void ACapability_TellsItsOperations()
    {
        var capability = new EntityCapability<Guid>(Guid.Empty, [EntityOperations.Read, "Pseudonymize"]);

        Assert.Multiple(() =>
        {
            Assert.That(capability.CanRead, Is.True);
            Assert.That(capability.CanUpdate, Is.False);
            Assert.That(capability.Allows("Pseudonymize"), Is.True);
            Assert.That(EntityCapability<Guid>.None(Guid.Empty).Operations, Is.Empty);
            Assert.That(JsonSerializer.Serialize(capability, JsonSerializerOptions.Web), Is.EqualTo("""{"key":"00000000-0000-0000-0000-000000000000","operations":["Read","Pseudonymize"]}"""));
        });
    }
}
