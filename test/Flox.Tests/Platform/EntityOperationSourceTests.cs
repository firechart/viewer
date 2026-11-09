using Flox.Core.Authorization;
using Flox.Core.Data;

namespace Flox.Tests.Platform;

// The permissions of a table and what a site gives by default for the grants of its registration (EntityGrants)
public class EntityOperationSourceTests
{
    private sealed class Sample : GuidEntity;

    [Test]
    public void ATable_HasFiveOperations_NamedAfterTheEntity()
    {
        var source = new EntityOperationSource(typeof(Sample), EntityGrants.UserData);

        Assert.Multiple(() =>
        {
            Assert.That(source.GetOperations(), Is.EqualTo(new[] { "Sample.Read", "Sample.Create", "Sample.Update", "Sample.Delete", "Sample.Publish" }));
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
    public void NobodyGetsPublishByDefault_WhateverTheGrantsOfTheTableAre(EntityGrants grants)
    {
        var given = new EntityOperationSource(typeof(Sample), grants).GetDefaultGrants();

        Assert.That(given.Select(row => row.Permission), Does.Not.Contain("Sample.Publish"));
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
            Assert.That(EntityPermissions.IsChecked(typeof(AccessGrant), EntityOperations.Read), Is.False, "a type that was not registered");
        });

        EntityPermissions.Register(typeof(Sample), EntityGrants.UserData);
        Assert.That(EntityPermissions.IsChecked(typeof(Sample), EntityOperations.Create), Is.True);
    }

    [Test]
    public void AGrantOfALevel_ConvertsFromTheLevel_AndNamesItsRole()
    {
        AccessGrant fromLevel = Access.Public;
        var ofRole = AccessGrant.OfRole("Doctors");

        Assert.Multiple(() =>
        {
            Assert.That(fromLevel, Is.EqualTo(new AccessGrant(Access.Public)));
            Assert.That(fromLevel.Role, Is.Null);
            Assert.That(ofRole, Is.EqualTo(new AccessGrant(Access.Role, "Doctors")));
        });
    }
}
