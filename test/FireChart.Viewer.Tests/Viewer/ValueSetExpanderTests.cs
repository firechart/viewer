using System.Net;
using System.Text;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Search;
using Hl7.Fhir.Specification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FireChart.Viewer.Tests.Viewer;

// The codes of a value set come from the FHIR server of the search (ValueSet/$expand); a server that does not expand it leaves the field a text box
public class ValueSetExpanderTests
{
    private sealed record Server(string BaseUrl) : IViewerServer
    {
        public string Key => BaseUrl;

        public string Name => "Test";

        public AuthenticationType AuthenticationType => AuthenticationType.None;

        public FhirRelease? FhirRelease => Hl7.Fhir.Specification.FhirRelease.R4;

        public int PageSize => 25;
    }

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<Uri> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/fhir+json") });
        }
    }

    private static ValueSetExpander Expander(FakeHandler handler, TimeProvider? clock = null)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(FhirServerHttp.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return new ValueSetExpander(services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>(), new ServerCredentialStore(), NullLogger<ValueSetExpander>.Instance, clock);
    }

    // A clock the test moves
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }

    private const string Status = """
                                  {
                                    "resourceType": "ValueSet",
                                    "expansion": {
                                      "contains": [
                                        { "system": "http://hl7.org/fhir/observation-status", "code": "final", "display": "Final" },
                                        { "system": "http://hl7.org/fhir/observation-status", "code": "amended", "display": "Amended",
                                          "contains": [ { "system": "http://hl7.org/fhir/observation-status", "code": "corrected", "display": "Corrected" } ] },
                                        { "abstract": true, "display": "A heading" },
                                        { "system": "http://hl7.org/fhir/observation-status", "code": "final", "display": "Final" }
                                      ]
                                    }
                                  }
                                  """;

    [Test]
    public void An_expansion_is_flat_without_headings_and_twice_given_codes_and_complete_when_it_is_under_the_limit()
    {
        var expansion = ValueSetExpansion.Parse(Status, 500);

        Assert.Multiple(() =>
        {
            Assert.That(expansion.Options.Select(o => o.Key), Is.EqualTo(new[]
            {
                "http://hl7.org/fhir/observation-status|final", "http://hl7.org/fhir/observation-status|amended", "http://hl7.org/fhir/observation-status|corrected"
            }));
            Assert.That(expansion.Complete, Is.True);
            Assert.That(expansion.Matching("CORR").Single().Code, Is.EqualTo("corrected"), "the display, whatever the case");
            Assert.That(ValueSetExpansion.Parse(Status, 2).Complete, Is.False, "cut at the limit");
            Assert.That(ValueSetExpansion.Parse("""{ "resourceType": "ValueSet", "expansion": { "total": 9000, "contains": [ { "code": "a" } ] } }""", 500).Complete, Is.False);
        });
    }

    [Test]
    public async Task The_server_is_asked_once_for_a_value_set_and_a_filter_is_applied_here_when_it_gave_the_whole_set()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, Status);
        var expander = Expander(handler);
        var server = new Server("https://fhir.example.test/r4/");

        var whole = await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/observation-status", null, null);
        var again = await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/observation-status", null, "fin");

        Assert.Multiple(() =>
        {
            Assert.That(whole!.Options, Has.Count.EqualTo(3));
            Assert.That(again!.Options.Select(o => o.Code), Is.EqualTo(new[] { "final" }));
            Assert.That(handler.Requested.Single().AbsoluteUri,
                        Does.StartWith("https://fhir.example.test/r4/ValueSet/$expand?url=http%3A%2F%2Fhl7.org%2Ffhir%2FValueSet%2Fobservation-status&count=500"));
        });
    }

    [Test]
    public async Task A_cut_value_set_is_searched_on_the_server_with_the_text_typed()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{ "resourceType": "ValueSet", "expansion": { "total": 90000, "contains": [ { "system": "http://loinc.org", "code": "8867-4", "display": "Heart rate" } ] } }""");
        var expander = Expander(handler);
        var server = new Server("https://fhir.example.test/r4");

        await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/observation-codes", null, null);
        var found = await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/observation-codes", null, "heart");

        Assert.That(handler.Requested, Has.Count.EqualTo(2));
        Assert.That(handler.Requested[1].Query, Does.Contain("&filter=heart&"));
        Assert.That(found!.Options.Single().Key, Is.EqualTo("http://loinc.org|8867-4"));
    }

    [Test]
    public async Task A_server_that_refuses_to_expand_gives_no_codes_and_is_not_asked_again()
    {
        var handler = new FakeHandler(HttpStatusCode.BadRequest,
            """{ "resourceType": "OperationOutcome", "issue": [ { "severity": "error", "diagnostics": "Unable to expand ValueSet because CodeSystem could not be found: http://loinc.org" } ] }""");
        var expander = Expander(handler);
        var server = new Server("https://fhir.example.test/r4");

        var first = await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/observation-codes", null, null);
        var second = await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/observation-codes", null, "heart");

        Assert.That(first, Is.Null);
        Assert.That(second, Is.Null);
        Assert.That(handler.Requested, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task A_failing_server_gives_no_codes_and_is_asked_again_only_after_a_while()
    {
        var handler = new FakeHandler(HttpStatusCode.ServiceUnavailable, "down");
        var clock = new Clock();
        var expander = Expander(handler, clock);
        var server = new Server("https://fhir.example.test/r4");

        await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/administrative-gender", null, null);
        var soon = await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/administrative-gender", null, null);
        var asked = handler.Requested.Count;
        clock.Now += TimeSpan.FromMinutes(6);
        await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/administrative-gender", null, null);

        Assert.Multiple(() =>
        {
            Assert.That(soon, Is.Null);
            Assert.That(asked, Is.EqualTo(1), "a server error is believed for a while");
            Assert.That(handler.Requested, Has.Count.EqualTo(2), "then the server is asked again");
        });
    }

    // A token that ran out is not "the server does not expand value sets": after the sign-in the field has its codes again
    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    public async Task A_server_that_refuses_the_caller_or_is_busy_is_asked_again_after_a_while(HttpStatusCode status)
    {
        var handler = new FakeHandler(status, "no");
        var clock = new Clock();
        var expander = Expander(handler, clock);
        var server = new Server("https://fhir.example.test/r4");

        var refused = await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/administrative-gender", null, null);
        clock.Now += TimeSpan.FromMinutes(6);
        await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/administrative-gender", null, null);

        Assert.That(refused, Is.Null);
        Assert.That(handler.Requested, Has.Count.EqualTo(2));
    }
}
