using System.Net.Http.Json;
using Flox.Core.Authorization;
using Flox.Core.Models;

namespace Flox.Shared.Services;

/// <summary>Loads the operations the server grants to the current user from <c>/api/permissions/effective</c>.</summary>
public sealed class HttpEffectivePermissionsLoader
(
    IHttpClientFactory httpClientFactory
) : IEffectivePermissionsLoader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(Constants.APP_HTTP_CLIENT_ID);
        var result = await client.GetFromJsonAsync<EffectivePermissions>("/api/permissions/effective", cancellationToken);
        return result?.Operations ?? [];
    }
}
