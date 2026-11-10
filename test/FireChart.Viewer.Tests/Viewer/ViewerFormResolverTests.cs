using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Viewer;

// The resolver of forms: designed beats generated, then the lower order; a failing provider is skipped and its
// answer asked again; Changed forgets
public class ViewerFormResolverTests
{
    private static readonly SearchFormKey s_patient = new("r4", "Patient");

    private sealed class FakeProvider(int order, FormSource source, Func<SearchFormKey, FormCandidate<SearchFormDefinition>?> find) : IViewerFormProvider<SearchFormKey, SearchFormDefinition>
    {
        public int Calls { get; private set; }

        public bool Fails { get; set; }

        public int Order => order;

        public FormSource Source => source;

        public event EventHandler? Changed;

        public void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public Task<FormCandidate<SearchFormDefinition>?> FindAsync(SearchFormKey key, CancellationToken cancellationToken)
        {
            Calls++;
            return Fails ? throw new HttpRequestException("offline") : Task.FromResult(find(key));
        }
    }

    private static FormCandidate<SearchFormDefinition> Form(string name, bool generated)
    {
        return new FormCandidate<SearchFormDefinition>(new SearchFormDefinition { Name = name, IsGenerated = generated }, generated, name);
    }

    private static Task<SearchFormDefinition> Plain(SearchFormKey key, CancellationToken cancellationToken)
    {
        return Task.FromResult(new SearchFormDefinition { Name = "plain" });
    }

    [Test]
    public async Task ADesignedForm_BeatsAGeneratedOneOfAHigherProvider_AndAmongDesignedTheLowerOrderWins()
    {
        var server = new FakeProvider(FormProviderOrder.Server, FormSource.Server, _ => Form("server-generated", true));
        var config = new FakeProvider(FormProviderOrder.Config, FormSource.Config, _ => Form("config", false));
        var vendor = new FakeProvider(FormProviderOrder.Vendor, FormSource.Vendor, _ => Form("vendor", false));
        using var resolver = new ViewerFormResolver<SearchFormKey, SearchFormDefinition>([vendor, server, config], Plain);

        var form = await resolver.ResolveAsync(s_patient, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(form.Definition.Name, Is.EqualTo("config"));
            Assert.That(form.Source, Is.EqualTo(FormSource.Config));
            Assert.That(vendor.Calls, Is.Zero, "a designed form ends the search");
        });
    }

    [Test]
    public async Task OnlyGeneratedForms_TheLowerOrderWins_AndWithoutAnyTheFallbackIsUsed()
    {
        var server = new FakeProvider(FormProviderOrder.Server, FormSource.Server, key => key.ResourceType == "Patient" ? Form("server-generated", true) : null);
        var vendor = new FakeProvider(FormProviderOrder.Vendor, FormSource.Vendor, key => key.ResourceType == "Patient" ? Form("vendor-generated", true) : null);
        using var resolver = new ViewerFormResolver<SearchFormKey, SearchFormDefinition>([vendor, server], Plain);

        var patient = await resolver.ResolveAsync(s_patient, CancellationToken.None);
        var other = await resolver.ResolveAsync(new SearchFormKey("r4", "Basic"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(patient, Is.EqualTo(new ResolvedForm<SearchFormDefinition>(patient.Definition, FormSource.Server, true)));
            Assert.That(patient.Definition.Name, Is.EqualTo("server-generated"));
            Assert.That(other.Source, Is.EqualTo(FormSource.Fallback));
            Assert.That(other.Definition.Name, Is.EqualTo("plain"));
        });
    }

    [Test]
    public async Task AFailingProvider_IsSkipped_AndAskedAgain_WhileAnAnswerIsKeptUntilChanged()
    {
        var server = new FakeProvider(FormProviderOrder.Server, FormSource.Server, _ => Form("server", false)) { Fails = true };
        var vendor = new FakeProvider(FormProviderOrder.Vendor, FormSource.Vendor, _ => Form("vendor", false));
        using var resolver = new ViewerFormResolver<SearchFormKey, SearchFormDefinition>([server, vendor], Plain);

        var offline = await resolver.ResolveAsync(s_patient, CancellationToken.None);
        server.Fails = false;
        var online = await resolver.ResolveAsync(s_patient, CancellationToken.None);
        var kept = await resolver.ResolveAsync(s_patient, CancellationToken.None);
        server.RaiseChanged();
        await resolver.ResolveAsync(s_patient, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(offline.Source, Is.EqualTo(FormSource.Vendor), "the server is offline: the vendor form");
            Assert.That(online.Source, Is.EqualTo(FormSource.Server), "a choice made while a provider failed is not kept");
            Assert.That(kept, Is.SameAs(online));
            Assert.That(server.Calls, Is.EqualTo(3), "Changed forgets the kept answer");
        });
    }

    [Test]
    public void TheCaller_CancellingIsNotSwallowed()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var provider = new FakeProvider(FormProviderOrder.Server, FormSource.Server, _ => throw new OperationCanceledException(cancelled.Token));
        using var resolver = new ViewerFormResolver<SearchFormKey, SearchFormDefinition>([provider], Plain);

        Assert.That(async () => await resolver.ResolveAsync(s_patient, cancelled.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task TheVendorSummaryProvider_HasTheDefaultForm_AndNoOtherCode()
    {
        using var resolver = new ViewerFormResolver<SummaryFormKey, ClinicalSummaryFormDefinition>([new VendorSummaryFormProvider(TestLibrary.Assets)], (_, _) => Task.FromResult(TestLibrary.SummaryDefault));

        var found = await resolver.ResolveAsync(new SummaryFormKey(ClinicalSummaryFormDefinition.DefaultCode), CancellationToken.None);
        var missing = await resolver.ResolveAsync(new SummaryFormKey("no-such-form"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(found.Source, Is.EqualTo(FormSource.Vendor));
            Assert.That(missing.Source, Is.EqualTo(FormSource.Fallback));
            Assert.That(missing.Definition.Code, Is.EqualTo(ClinicalSummaryFormDefinition.DefaultCode));
        });
    }
}
