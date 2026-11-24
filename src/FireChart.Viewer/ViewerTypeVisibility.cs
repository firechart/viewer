using FireChart.Viewer.Core.Fhir;
using Flox.Core;
using Flox.Core.Data;
using Flox.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer;

// The resource types hidden on the first page of a server, on the device of the host (ResourceTypeVisibility)
internal sealed class ViewerTypeVisibility(IGuidEntityStorage<ResourceTypeVisibility> storage)
{
    public static ViewerTypeVisibility? Of(IServiceProvider services)
    {
        return services.GetService<IGuidEntityStorage<ResourceTypeVisibility>>() is { } storage ? new ViewerTypeVisibility(storage) : null;
    }

    public async Task<HashSet<string>> HiddenAsync(IViewerServer server)
    {
        var document = await storage.GetByIdAsync(ResourceTypeVisibility.KeyOf(server.BaseUrl));
        return document is null ? new HashSet<string>(StringComparer.Ordinal) : [.. document.Types.Where(entry => entry.Hidden).Select(entry => entry.ResourceType)];
    }

    // Every type of the server with its flag; another tab that saved meanwhile loses to this one
    public async Task SaveAsync(IViewerServer server, IEnumerable<string> types, IReadOnlySet<string> hidden)
    {
        var key = ResourceTypeVisibility.KeyOf(server.BaseUrl);
        var entries = types.Select(type => new ResourceTypeVisibilityEntry { ResourceType = type, Hidden = hidden.Contains(type) }).ToList();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (await storage.GetByIdAsync(key) is { } document)
                {
                    document.ServerKey = server.Key;
                    document.Types = entries;
                    await storage.UpdateAsync(key, document);
                }
                else
                {
                    await storage.InsertAsync(new ResourceTypeVisibility
                    {
                        Uid = key,
                        ServerAddress = UserBookmark.NormalizeAddress(server.BaseUrl)!,
                        ServerKey = server.Key,
                        Types = entries
                    });
                }

                return;
            }
            catch (Exception ex) when (attempt == 0 && ex is DuplicateException or ConcurrentUpdateException or NotFoundException)
            {
                // written by another tab between the read and the write: once more over its document
            }
        }
    }
}
