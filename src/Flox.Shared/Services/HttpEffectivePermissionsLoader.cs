using System.Net.Http.Json;
using Flox.Core.Authorization;
using Flox.Core.Models;

namespace Flox.Shared.Services;

public sealed class HttpEffectivePermissionsLoader
(
    IHttpClientFactory httpClientFactory
) : IEffectivePermissionsLoader
{
    public async Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(Constants.APP_HTTP_CLIENT_ID);
        var result = await client.GetFromJsonAsync<EffectivePermissions>("/api/permissions/effective", cancellationToken);
        return result?.Operations ?? [];
    }
}
