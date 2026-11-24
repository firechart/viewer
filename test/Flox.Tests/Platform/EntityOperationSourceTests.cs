using Flox.Core.Authorization;
using Flox.Core.Data;

namespace Flox.Tests.Platform;

// The permissions of a table and what a site gives by default for the grants of its registration (EntityGrants)
public class EntityOperationSourceTests
{
    private sealed class Sample : GuidEntity;

    private sealed class Lookup : GuidTableEntity;

    private sealed class Line : GuidDetailEntity<Sample>
    {
        [AclMaster]
        [Index]
        public Guid SampleId { get; set; }

        public override Guid MasterId
        {
            get
            {
                return SampleId;
            }
        }
    }

    private sealed class Orphan : Entity<Guid>, IGuidEntity;

    private sealed class Both : GuidEntity, ITableAccess;

    private sealed class Unmarked : GuidDetailEntity<Sample>
    {
        public Guid SampleId { get; set; }

        public override Guid MasterId
        {
            get
            {
                return SampleId;
            }
        }
    }

    [Test]
    public void ATable_HasFiveOperations_NamedAfterTheEntity_ManageAccessIncluded()
    {
        var source = new EntityOperationSource(typeof(Sample), EntityGrants.UserData);

        Assert.Multiple(() =>
        {
            Assert.That(source.GetOperations(), Is.EqualTo(new[] { "Sample.Read", "Sample.Create", "Sample.Update", "Sample.Delete", "Sample.ManageAccess" }));
            Assert.That(source.Category, Is.EqualTo("Flox data"));
            Assert.That(EntityPermissions.Name(typeof(Sample), EntityOperations.Update), Is.EqualTo("Sample.Update"));
        });
    }

    [TestCase(EntityGrants.None, new string[0])]
    [TestCase(EntityGrants.AuthenticatedRead, new[] { "Authenticated:Sample.Read" })]
    [TestCase(EntityGrants.AnonymousRead, new[] { "Anonymous:Sample.Read" })]
    [TestCase(EntityGrants.AuthenticatedWrite, new[] { "Authenticated:Sample.Create", "Authenticated:Sample.Update", "Authenticated:Sample.Delete" })]
    [TestCase(EntityGrants.UserData, new[] { "Authenticated:Sample.Read", "Authenticated:Sample.Create", "Authenticated:Sample.Update", "Authenticated:Sample.Delete" })]
    [TestCase(EntityGrants.UserData | EntityGrants.AnonymousRead, new[] { "Anonymous:Sample.Read", "Authenticated:Sample.Read", "Authenticated:Sample.Create", "Authenticated:Sample.Update", "Authenticated:Sample.Delete" })]
    [TestCase(EntityGrants.PublicRead, new[] { "Anonymous:Sample.Read", "Authenticated:Sample.Read" })]
    public void TheDefaultGrants_FollowTheFlagsOfTheRegistration(EntityGrants grants, string[] expected)
    {
        var given = new EntityOperationSource(typeof(Sample), grants).GetDefaultGrants()
                                                                      .Select(row => $"{row.Role}:{row.Permission}");

        Assert.That(given, Is.EquivalentTo(expected));
    }

    [TestCase(EntityGrants.None)]
    [TestCase(EntityGrants.UserData)]
    [TestCase(EntityGrants.UserData | EntityGrants.AnonymousRead)]
    [TestCase(EntityGrants.PublicRead)]
    public void NobodyGetsManageAccessByDefault_WhateverTheGrantsOfTheTableAre(EntityGrants grants)
    {
        var given = new EntityOperationSource(typeof(Sample), grants).GetDefaultGrants();

        Assert.That(given.Select(row => row.Permission), Does.Not.Contain("Sample.ManageAccess"));
    }

    [Test]
    public void ATableIsNotCheckedUntilItIsRegistered_AndAServiceTableIsCheckedForReadingOnly()
    {
        EntityPermissions.Register(typeof(Sample), EntityGrants.PublicRead);

        Assert.Multiple(() =>
        {
            Assert.That(EntityPermissions.IsChecked(typeof(Sample), EntityOperations.Read), Is.True);
            Assert.That(EntityPermissions.IsChecked(typeof(Sample), EntityOperations.Create), Is.False);
            Assert.That(EntityPermissions.IsChecked(typeof(Sample), EntityOperations.Delete), Is.False);
            Assert.That(EntityPermissions.IsChecked(typeof(AclEntry), EntityOperations.Read), Is.False, "a type that was not registered");
        });

        EntityPermissions.Register(typeof(Sample), EntityGrants.UserData);
        Assert.That(EntityPermissions.IsChecked(typeof(Sample), EntityOperations.Create), Is.True);
    }

    [Test]
    public void AnEntity_RegistersItsOwnOperations_AfterTheBaseOnes_AndATableWithoutListsHasNoManageAccess()
    {
        var own = new EntityOperationSource(EntityPermissions.Register(typeof(Sample), new EntityAccessOptions { Operations = ["Pseudonymize", "Depseudonymize"], Sensitive = ["Depseudonymize"] }));
        var table = new EntityOperationSource(EntityPermissions.Register(typeof(Lookup), new EntityAccessOptions { Grants = EntityGrants.PublicRead }));

        Assert.Multiple(() =>
        {
            Assert.That(own.GetOperations(), Is.EqualTo(new[] { "Sample.Read", "Sample.Create", "Sample.Update", "Sample.Delete", "Sample.ManageAccess", "Sample.Pseudonymize", "Sample.Depseudonymize" }));
            Assert.That(table.GetOperations(), Is.EqualTo(new[] { "Lookup.Read", "Lookup.Create", "Lookup.Update", "Lookup.Delete" }));
            Assert.That(EntityPermissions.OperationOf(typeof(Sample), "Sample.Read"), Is.EqualTo("Read"));
            Assert.That(EntityPermissions.OperationOf(typeof(Sample), "Sample.Pseudonymize"), Is.EqualTo("Pseudonymize"));
            Assert.That(EntityPermissions.OperationOf(typeof(Sample), "Sample.Fly"), Is.Null);
            Assert.That(EntityPermissions.OperationOf(typeof(Sample), "Other.Read"), Is.Null);
        });
    }

    // The kind of an entity is the interface it implements, exactly one of three; a detail names the property of the key of its master
    [Test]
    public void TheKindOfAnEntity_IsItsInterface_ExactlyOne()
    {
        var acl = EntityPermissions.Register(typeof(Sample), new EntityAccessOptions());
        var table = EntityPermissions.Register(typeof(Lookup), new EntityAccessOptions());
        var detail = EntityPermissions.Register(typeof(Line), new EntityAccessOptions { Operations = ["Sign"] });

        Assert.Multiple(() =>
        {
            Assert.That(acl.Kind, Is.EqualTo(EntityAccessKind.Acl));
            Assert.That(table.Kind, Is.EqualTo(EntityAccessKind.Table));
            Assert.That(detail.Kind, Is.EqualTo(EntityAccessKind.Detail));
            Assert.That(detail.MasterType, Is.EqualTo(typeof(Sample)));
            Assert.That(detail.MasterProperty?.Name, Is.EqualTo(nameof(Line.SampleId)));
            Assert.That(detail.Operations, Is.EqualTo(new[] { "Read", "Create", "Update", "Delete", "Sign" }), "no ManageAccess: a detail has no list to manage");
            Assert.That(detail.DefaultAcl, Is.Empty);
            Assert.That(table.DefaultAcl, Is.Empty);
            Assert.That(() => EntityPermissions.Register(typeof(Orphan), new EntityAccessOptions()), Throws.InvalidOperationException.With.Message.Contains("none"), "an entity of no kind is not registered");
            Assert.That(() => EntityPermissions.Register(typeof(Both), new EntityAccessOptions()), Throws.InvalidOperationException, "nor one of two");
            Assert.That(() => EntityPermissions.Register(typeof(Unmarked), new EntityAccessOptions()), Throws.InvalidOperationException.With.Message.Contains("[AclMaster]"), "a detail names the key of its master");
            Assert.That(() => EntityPermissions.Register(typeof(Lookup), new EntityAccessOptions { DefaultAcl = [new AclEntry(AclSubject.Everyone, "Read")] }), Throws.ArgumentException, "a table has no list");
        });
    }

    [TestCase("Bad Name")]
    [TestCase("Has.Dot")]
    [TestCase(" ")]
    public void AnOperationOfAnEntity_IsOneWord(string operation)
    {
        Assert.That(() => EntityPermissions.Register(typeof(Sample), new EntityAccessOptions { Operations = [operation] }), Throws.ArgumentException);
    }

    [Test]
    public void ASensitiveOperation_IsOneOfTheEntity_AndADefaultListNamesOperationsOfARow()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => EntityPermissions.Register(typeof(Sample), new EntityAccessOptions { Sensitive = ["Pseudonymize"] }), Throws.ArgumentException);
            Assert.That(() => EntityPermissions.Register(typeof(Sample), new EntityAccessOptions { DefaultAcl = [new AclEntry(AclSubject.Owner, EntityOperations.Create)] }), Throws.ArgumentException, "Create is of the table");
            Assert.That(() => EntityPermissions.Register(typeof(Sample), new EntityAccessOptions { DefaultAcl = [new AclEntry(AclSubject.Owner, "Fly")] }), Throws.ArgumentException);
        });
    }

    [Test]
    public void TheDefaultListOfANewRow_GivesTheOwnerEveryOperationOfARow_ButTheSensitiveOnes()
    {
        var descriptor = EntityPermissions.Register(typeof(Sample), new EntityAccessOptions { Operations = ["Pseudonymize", "Depseudonymize"], Sensitive = ["Depseudonymize"] });

        Assert.That(descriptor.DefaultAcl, Is.EquivalentTo(AclEntry.For(AclSubject.Owner, "Read", "Update", "Delete", "ManageAccess", "Pseudonymize")));
    }
}
