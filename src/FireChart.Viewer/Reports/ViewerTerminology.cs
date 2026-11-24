using System.Collections.Concurrent;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Reports;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer.Reports;

/// <summary>
/// The words of the codes a record gives without a display, for the texts of the components: a code is looked up on the terminology server of the
/// Viewer (<see cref="ViewerSession.TerminologyServer"/>) the first time a text needs it; the text shows the bare code meanwhile, and the pages draw
/// again when the words come (<see cref="Changed"/>). Nothing is asked without a terminology server; a failing server never breaks a page. Scoped:
/// the words found are kept for the scope, one batch per render.
/// </summary>
/// <param name="resolver">Asks the terminology server (<c>CodeSystem/$lookup</c>) and keeps its answers.</param>
/// <example>
/// <code>
/// terminology.Changed += (_, _) =&gt; InvokeAsync(StateHasChanged);
/// </code>
/// </example>
[FloxService(ServiceLifetime.Scoped)]
public sealed class ViewerTerminology(ITerminologyResolver resolver)
{
    private static readonly TimeSpan s_wait = TimeSpan.FromSeconds(6);

    private readonly ConcurrentDictionary<string, Server> _servers = new(StringComparer.Ordinal);

    private ITerminologyResolver Resolver => resolver;

    /// <summary>
    /// Raised when words of codes have come: a page that showed bare codes draws again.
    /// </summary>
    /// <example>
    /// <code>
    /// terminology.Changed += OnWords;
    /// </code>
    /// </example>
    public event EventHandler? Changed;

    // The words of the codes for the texts, from the terminology server; null without one
    internal IReportDisplays? For(string? server)
    {
        return server is null ? null : _servers.GetOrAdd(server, address => new Server(this, address));
    }

    private sealed class Server(ViewerTerminology owner, string address) : IReportDisplays
    {
        // null: asked, no word (yet)
        private readonly ConcurrentDictionary<CodeKey, string?> _known = new();
        private readonly ConcurrentQueue<CodeKey> _pending = new();
        private int _scheduled;

        public string? DisplayOf(string system, string code)
        {
            var key = new CodeKey(system, code);
            if (_known.TryGetValue(key, out var display))
            {
                return display;
            }

            // A page asks for at most TerminologyCodes.Limit codes a session: a long list does not flood the server
            if (_known.Count < TerminologyCodes.Limit && _known.TryAdd(key, null))
            {
                _pending.Enqueue(key);
                if (Interlocked.Exchange(ref _scheduled, 1) == 0)
                {
                    _ = LookUpAsync();
                }
            }

            return null;
        }

        // The codes of one render go in one batch
        private async Task LookUpAsync()
        {
            await Task.Yield();
            Interlocked.Exchange(ref _scheduled, 0);
            var codes = new List<CodeKey>();
            while (_pending.TryDequeue(out var code))
            {
                codes.Add(code);
            }

            if (codes.Count == 0)
            {
                return;
            }

            try
            {
                var map = await owner.Resolver.ResolveAsync(address, codes, s_wait);
                var found = false;
                foreach (var code in codes)
                {
                    if (map.Find(code.System, code.Code) is { } display)
                    {
                        _known[code] = display;
                        found = true;
                    }
                }

                if (found)
                {
                    owner.Changed?.Invoke(owner, EventArgs.Empty);
                }
            }
            catch (Exception)
            {
                // The codes stay bare
            }
        }
    }
}
