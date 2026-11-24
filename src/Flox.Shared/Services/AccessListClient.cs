using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Flox.Shared.Services;

/// <summary>
/// The access lists of the server (<c>api/acl/{entity}</c>): the list of a row, the model of its editor and the subjects an editor adds. The
/// refusals of the server are the exceptions of the data layer.
/// </summary>
public interface IAccessListClient
{
    /// <summary>Reads the access list of a row (ManageAccess on the row).</summary>
    Task<IReadOnlyList<AclEntry>> GetAsync(string entity, Guid key, CancellationToken cancellationToken = default);

    /// <summary>Replaces the access list of a row and returns the stored one.</summary>
    Task<IReadOnlyList<AclEntry>> SetAsync(string entity, Guid key, IReadOnlyCollection<AclEntry> acl, CancellationToken cancellationToken = default);

    /// <summary>Reads what an editor of the list of a row shows.</summary>
    Task<AccessEditor> GetEditorAsync(string entity, Guid key, CancellationToken cancellationToken = default);

    /// <summary>Finds roles and users whose name starts with the text.</summary>
    Task<IReadOnlyList<AccessSubjectInfo>> FindPrincipalsAsync(string entity, string? search, CancellationToken cancellationToken = default);

    /// <summary>Describes subjects an editor adds: their names and what the permission matrix gives them.</summary>
    Task<IReadOnlyList<AccessSubjectInfo>> DescribeAsync(string entity, IReadOnlyCollection<AclSubject> subjects, CancellationToken cancellationToken = default);
}

/// <summary>The access lists of the server over HTTP, with the HTTP client of the application.</summary>
/// <param name="http">The HTTP client of the application, which signs the requests in.</param>
public sealed class HttpAccessListClient(HttpClient http) : IAccessListClient
{
    private static readonly JsonSerializerOptions s_json = JsonSerializerOptions.Web;

    /// <summary>Takes the HTTP client of the application from the factory.</summary>
    public HttpAccessListClient(IHttpClientFactory factory) : this((factory ?? throw new ArgumentNullException(nameof(factory))).CreateClient(Constants.APP_HTTP_CLIENT_ID))
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AclEntry>> GetAsync(string entity, Guid key, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(RowAddress(entity, key), cancellationToken);
        return (await ReadAsync<EntityAccessList>(response, key, cancellationToken)).Entries;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AclEntry>> SetAsync(string entity, Guid key, IReadOnlyCollection<AclEntry> acl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acl);
        using var response = await http.PutAsJsonAsync(RowAddress(entity, key), new EntityAccessList([.. acl]), s_json, cancellationToken);
        return (await ReadAsync<EntityAccessList>(response, key, cancellationToken)).Entries;
    }

    /// <inheritdoc />
    public async Task<AccessEditor> GetEditorAsync(string entity, Guid key, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"{RowAddress(entity, key)}/editor", cancellationToken);
        return await ReadAsync<AccessEditor>(response, key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccessSubjectInfo>> FindPrincipalsAsync(string entity, string? search, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"{EntityAddress(entity)}/principals?search={Uri.EscapeDataString(search ?? string.Empty)}", cancellationToken);
        return await ReadAsync<List<AccessSubjectInfo>>(response, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccessSubjectInfo>> DescribeAsync(string entity, IReadOnlyCollection<AclSubject> subjects, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        using var response = await http.PostAsJsonAsync($"{EntityAddress(entity)}/subjects", subjects, s_json, cancellationToken);
        return await ReadAsync<List<AccessSubjectInfo>>(response, null, cancellationToken);
    }

    private static string EntityAddress(string entity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);
        return $"/api/acl/{Uri.EscapeDataString(entity)}";
    }

    private static string RowAddress(string entity, Guid key)
    {
        return $"{EntityAddress(entity)}/{key}";
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, Guid? key, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = await DetailOfAsync(response, cancellationToken);
            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new UnauthenticatedException(),
                HttpStatusCode.Forbidden => new ForbidDocumentException(detail),
                HttpStatusCode.NotFound when key.HasValue => new NotFoundException(key.Value),
                HttpStatusCode.NotFound => new NotSupportedException(detail),
                HttpStatusCode.BadRequest => new ArgumentException(detail),
                var status => new HttpRequestException($"The access list request failed: {(int)status} {detail}", null, status)
            };
        }

        return await response.Content.ReadFromJsonAsync<T>(s_json, cancellationToken) ?? throw new InvalidOperationException("The server gave no answer.");
    }

    // The detail of a problem the server answered, else its body
    private static async Task<string> DetailOfAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("detail", out var detail) && detail.GetString() is { } text ? text : body;
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
