using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Flox.Licensing;

namespace Flox.Tests.Licensing;

// What a product does with a license: the signature, the site, the dates, the file, the summary for a client, the free edition
public sealed class LicenseVerifierTests
{
    private const string Audience = "https://fhir.klinikum.de";
    private static readonly DateTimeOffset s_now = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public void A_signed_license_is_read_with_its_key_and_only_for_its_site()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var token = LicenseJwt.Sign(Draft() with { Features = ["validator"], Limits = new Dictionary<string, long> { ["users"] = 200 } }, key);
        LicensePublicKey[] keys = [new("test-1", key.ExportSubjectPublicKeyInfoPem())];

        var status = LicenseVerifier.Verify(token, Audience, keys, s_now);
        var otherSite = LicenseVerifier.Verify(token, "https://fhir.other.de", keys, s_now);
        var otherKey = LicenseVerifier.Verify(token, Audience, [new("test-1", OtherPublicPem())], s_now);

        Assert.That(status.State, Is.EqualTo(LicenseState.Valid), status.Error);
        var license = status.License!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(license.Id, Is.EqualTo("LIC-2026-0001"));
            Assert.That(license.Customer, Is.EqualTo("klinikum"));
            Assert.That(license.Audience, Is.EqualTo(new[] { Audience }));
            Assert.That(license.Kind, Is.EqualTo(LicenseKind.Subscription));
            Assert.That(license.ValidUntil, Is.EqualTo(new DateOnly(2027, 11, 10)));
            Assert.That(license.SupportUntil, Is.EqualTo(new DateOnly(2027, 11, 10)));
            Assert.That(license.KeyId, Is.EqualTo("test-1"));
            Assert.That(status.HasFeature("validator"), Is.True);
            Assert.That(status.HasFeature("synthetic-data"), Is.False);
            Assert.That(license.Limit("users"), Is.EqualTo(200));
            Assert.That(license.Ends, Is.EqualTo(new DateTimeOffset(2027, 11, 11, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(otherSite.State, Is.EqualTo(LicenseState.Invalid));
            Assert.That(otherKey.State, Is.EqualTo(LicenseState.Invalid));
            Assert.That(LicenseVerifier.Verify(" ", Audience, keys, s_now).State, Is.EqualTo(LicenseState.None));
        }
    }

    [TestCase("2026-11-10", "NotYetValid")]
    [TestCase("2027-10-11", "Valid")]
    [TestCase("2027-10-13", "Expiring")]
    [TestCase("2027-11-11", "Grace")]
    [TestCase("2027-12-11", "Expired")]
    public void The_state_follows_the_dates(string day, string expected)
    {
        var now = new DateTimeOffset(DateOnly.Parse(day, CultureInfo.InvariantCulture).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        Assert.That(LicenseVerifier.StateAt(Draft(), now), Is.EqualTo(Enum.Parse<LicenseState>(expected)));
    }

    [Test]
    public void A_license_file_is_read_and_a_missing_one_is_no_license()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var dir = Directory.CreateTempSubdirectory("license-").FullName;
        try
        {
            var path = Path.Combine(dir, "license.jwt");
            File.WriteAllText(path, LicenseJwt.Sign(Draft(), key) + Environment.NewLine);
            LicensePublicKey[] keys = [new("test-1", key.ExportSubjectPublicKeyInfoPem())];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(LicenseVerifier.VerifyFile(path, Audience, keys, s_now).License?.Id, Is.EqualTo("LIC-2026-0001"));
                Assert.That(LicenseVerifier.VerifyFile(path + ".missing", Audience, keys, s_now).State, Is.EqualTo(LicenseState.None));
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public void The_summary_for_a_client_has_the_edition_and_features_in_force_and_nothing_of_the_customer()
    {
        var license = Draft() with { Features = ["validator"], Limits = new Dictionary<string, long> { ["users"] = 200 } };
        var active = LicenseSummary.Of(new LicenseStatus(LicenseState.Expiring, license, null));
        var expired = LicenseSummary.Of(new LicenseStatus(LicenseState.Expired, license, null));
        var json = JsonSerializer.Serialize(active, JsonSerializerOptions.Web);
        var read = JsonSerializer.Deserialize<LicenseSummary>(json, JsonSerializerOptions.Web)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(active.Edition, Is.EqualTo("Enterprise"));
            Assert.That(active.HasFeature("validator"), Is.True);
            Assert.That(expired.Edition, Is.EqualTo(License.FreeEdition));
            Assert.That(expired.HasFeature("validator"), Is.False);
            Assert.That(expired.State, Is.EqualTo(LicenseState.Expired));
            Assert.That(expired.ValidUntil, Is.EqualTo(new DateOnly(2027, 11, 10)));
            Assert.That(json, Does.Contain("\"state\":\"Expiring\"").And.Not.Contain("klinikum").And.Not.Contain("users"));
            Assert.That(read with { Features = active.Features }, Is.EqualTo(active));
            Assert.That(read.Features, Is.EqualTo(active.Features));
        }
    }

    [Test]
    public void The_free_license_grants_nothing_and_a_signed_free_one_is_not_accepted()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var free = LicenseJwt.Sign(Draft() with { Kind = LicenseKind.Free, Features = ["validator"] }, key);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(License.Free.IsFree, Is.True);
            Assert.That(License.Free.HasFeature("validator"), Is.False);
            Assert.That(License.Free.Limit("users"), Is.Null);
            Assert.That(LicenseVerifier.Verify(free, Audience, [new("test-1", key.ExportSubjectPublicKeyInfoPem())], s_now).State,
                Is.EqualTo(LicenseState.Invalid));
        }
    }

    private static string OtherPublicPem()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return key.ExportSubjectPublicKeyInfoPem();
    }

    private static License Draft()
    {
        return new License
        {
            Id = "LIC-2026-0001",
            Customer = "klinikum",
            Name = "Klinikum GmbH",
            Audience = [Audience],
            Edition = "Enterprise",
            Kind = LicenseKind.Subscription,
            IssuedAt = new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero),
            ValidFrom = new DateOnly(2026, 11, 11),
            ValidUntil = new DateOnly(2027, 11, 10),
            SupportUntil = new DateOnly(2027, 11, 10),
            KeyId = "test-1",
        };
    }
}
