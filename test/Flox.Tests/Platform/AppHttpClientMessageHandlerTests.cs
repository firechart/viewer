using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Flox.Core;
using Flox.Core.Services;
using Flox.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace Flox.Tests.Platform;

// What the handler does when the server refuses the access token it sent (401): one renewal that does not wait for the expiry and one repeat of the
// request with the token it gives; no loop, nothing for a request whose token the handler did not put there
[TestFixture]
public class AppHttpClientMessageHandlerTests
{
    private sealed record Seen(string? Token, string? Body);

    private sealed class Authentication : IAuthenticationService
    {
        public string? Token { get; set; }

        public string? Renewed { get; set; }

        public List<string> Rejected { get; } = [];

        public event AuthenticationStateChangedHandler? AuthenticationStateChanged
        {
            add { }
            remove { }
        }

        public Task<string?> GetAccessTokenAsync()
        {
            return Task.FromResult(Token);
        }

        public Task<string?> RefreshAccessTokenAsync(string rejectedToken)
        {
            Rejected.Add(rejectedToken);
            return Task.FromResult(Renewed);
        }

        public Task<ClaimsPrincipal> GetCurrentUserAsync()
        {
            throw new NotSupportedException();
        }

        public Task GoToProfileAsync()
        {
            throw new NotSupportedException();
        }

        public Task LoginAsync(string? returnUrl = null)
        {
            throw new NotSupportedException();
        }

        public Task LogoutAsync()
        {
            throw new NotSupportedException();
        }

        public void SetCurrentUser(ClaimsPrincipal? value)
        {
            throw new NotSupportedException();
        }
    }

    // Answers 401 to every token but the ones it accepts
    private sealed class Server : HttpMessageHandler
    {
        public HashSet<string> Accepts { get; } = [];

        public List<Seen> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = request.Headers.Authorization?.Parameter;
            Requests.Add(new Seen(token, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(token is not null && Accepts.Contains(token) ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
        }
    }

    private static HttpMessageInvoker Pipeline(Authentication authentication, Server server)
    {
        return new HttpMessageInvoker(new AppHttpClientMessageHandler(authentication)
        {
            InnerHandler = server
        });
    }

    private static async Task SendAndDisposeAsync(HttpMessageInvoker pipeline, HttpRequestMessage request)
    {
        using var response = await pipeline.SendAsync(request, CancellationToken.None);
    }

    [Test]
    public async Task ARefusedToken_IsRenewedAtOnce_AndTheRequestGoesAgainWithTheNewOne()
    {
        var authentication = new Authentication { Token = "old", Renewed = "new" };
        var server = new Server { Accepts = { "new" } };
        using var pipeline = Pipeline(authentication, server);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/odata/x")
        {
            Content = new StringContent("{\"a\":1}", Encoding.UTF8, "application/json")
        };

        using var response = await pipeline.SendAsync(request, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(authentication.Rejected, Is.EqualTo(new[] { "old" }), "the renewal was asked for the refused token, once");
            Assert.That(server.Requests.Select(row => row.Token), Is.EqualTo(new[] { "old", "new" }));
            Assert.That(server.Requests.Select(row => row.Body), Is.EqualTo(new[] { "{\"a\":1}", "{\"a\":1}" }), "the body goes again");
        });
    }

    [Test]
    public async Task ASecondRefusal_IsTheEnd_NotALoop()
    {
        var authentication = new Authentication { Token = "old", Renewed = "new" };
        var server = new Server();
        using var pipeline = Pipeline(authentication, server);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/odata/x");

        await Assert.ThrowsAsync<UnauthenticatedException>(() => SendAndDisposeAsync(pipeline, request));

        Assert.Multiple(() =>
        {
            Assert.That(authentication.Rejected, Has.Count.EqualTo(1), "one renewal");
            Assert.That(server.Requests, Has.Count.EqualTo(2), "one repeat");
        });
    }

    [TestCase(null)]
    [TestCase("old")]
    public async Task WithoutAnotherToken_TheRefusalIsTheEnd(string? renewed)
    {
        var authentication = new Authentication { Token = "old", Renewed = renewed };
        var server = new Server();
        using var pipeline = Pipeline(authentication, server);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/odata/x");

        await Assert.ThrowsAsync<UnauthenticatedException>(() => SendAndDisposeAsync(pipeline, request));

        Assert.That(server.Requests, Has.Count.EqualTo(1), "nothing to send again: the same token, or none");
    }

    [Test]
    public async Task ATokenTheCallerSet_OrNoToken_IsNotRenewed()
    {
        var authentication = new Authentication { Renewed = "new" };
        var server = new Server();
        using var pipeline = Pipeline(authentication, server);
        using var withoutToken = new HttpRequestMessage(HttpMethod.Get, "http://localhost/odata/x");
        using var ofTheCaller = new HttpRequestMessage(HttpMethod.Get, "http://localhost/other")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", "theirs") }
        };
        authentication.Token = null;

        await Assert.ThrowsAsync<UnauthenticatedException>(() => SendAndDisposeAsync(pipeline, withoutToken));
        authentication.Token = "old";
        await Assert.ThrowsAsync<UnauthenticatedException>(() => SendAndDisposeAsync(pipeline, ofTheCaller));

        Assert.Multiple(() =>
        {
            Assert.That(authentication.Rejected, Is.Empty, "the handler renews only the token it put there");
            Assert.That(server.Requests.Select(row => row.Token), Is.EqualTo(new[] { null, "theirs" }));
        });
    }
}
