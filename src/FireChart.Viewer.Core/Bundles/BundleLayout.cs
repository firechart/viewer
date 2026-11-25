using System.Globalization;

namespace FireChart.Viewer.Core.Bundles;

// A node of the drawing: its box, the top left corner at X, Y
internal sealed record PlacedNode(BundleNode Node, double X, double Y);

// A reference of the drawing: an SVG path from the node that refers to the node referred to
internal sealed record PlacedEdge(BundleEdge Edge, string Path);

internal sealed record BundleDrawing(IReadOnlyList<PlacedNode> Nodes, IReadOnlyList<PlacedEdge> Edges, double Width, double Height)
{
    public static readonly BundleDrawing Empty = new([], [], 0, 0);
}

// A layered drawing of the graph: what refers to nothing (a Patient) is on top, what refers to it below, each layer at least one below what it refers
// to (the longest path); a cycle is cut where the search met it. A layer longer than a row wraps into several rows, so a Bundle of a patient and fifty
// observations stays readable. In a layer the nodes stand in the order of their neighbours above and below (barycenters), the types together
internal static class BundleLayout
{
    public const double NodeWidth = 184;
    public const double NodeHeight = 52;
    private const double Gap = 24;
    private const double RowGap = 20;
    private const double LayerGap = 64;
    private const double Padding = 16;
    private const int Sweeps = 4;

    public static BundleDrawing Draw(BundleGraph graph, int minPerRow = 6)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (graph.Nodes.Count == 0)
        {
            return BundleDrawing.Empty;
        }

        var nodes = graph.Nodes;
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < nodes.Count; i++)
        {
            index[nodes[i].Key] = i;
        }

        var outgoing = nodes.Select(_ => new HashSet<int>()).ToArray();
        var linked = new bool[nodes.Count];
        foreach (var edge in graph.Edges)
        {
            var from = index[edge.From];
            var to = index[edge.To];
            outgoing[from].Add(to);
            linked[from] = linked[to] = true;
        }

        var acyclic = WithoutCycles(outgoing);
        var layers = Layers(acyclic, linked);
        var order = Order(nodes, acyclic, layers);
        return Place(nodes, graph.Edges, index, order, Math.Max(1, minPerRow));
    }

    // The references without those that close a cycle (found by a depth-first search from each node in turn)
    private static HashSet<int>[] WithoutCycles(HashSet<int>[] outgoing)
    {
        var result = outgoing.Select(set => new HashSet<int>(set)).ToArray();
        var state = new byte[outgoing.Length];
        for (var start = 0; start < outgoing.Length; start++)
        {
            if (state[start] != 0)
            {
                continue;
            }

            var stack = new Stack<IEnumerator<int>>();
            var path = new Stack<int>();
            state[start] = 1;
            path.Push(start);
            stack.Push(outgoing[start].ToList().GetEnumerator());
            while (stack.Count > 0)
            {
                var next = stack.Peek();
                if (next.MoveNext())
                {
                    var to = next.Current;
                    if (state[to] == 1)
                    {
                        result[path.Peek()].Remove(to);
                    }
                    else if (state[to] == 0)
                    {
                        state[to] = 1;
                        path.Push(to);
                        stack.Push(outgoing[to].ToList().GetEnumerator());
                    }
                }
                else
                {
                    stack.Pop().Dispose();
                    state[path.Pop()] = 2;
                }
            }
        }

        return result;
    }

    // The layer of each node: 0 for one that refers to nothing, else one more than the deepest it refers to; the nodes without any reference are the
    // last layer, apart
    private static int[] Layers(HashSet<int>[] acyclic, bool[] linked)
    {
        var count = acyclic.Length;
        var layer = Enumerable.Repeat(-1, count).ToArray();
        var pending = acyclic.Select(set => set.Count).ToArray();
        var incoming = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
        for (var from = 0; from < count; from++)
        {
            foreach (var to in acyclic[from])
            {
                incoming[to].Add(from);
            }
        }

        var ready = new Queue<int>(Enumerable.Range(0, count).Where(node => pending[node] == 0));
        while (ready.Count > 0)
        {
            var node = ready.Dequeue();
            layer[node] = acyclic[node].Count == 0 ? 0 : acyclic[node].Max(to => layer[to]) + 1;
            foreach (var from in incoming[node])
            {
                if (--pending[from] == 0)
                {
                    ready.Enqueue(from);
                }
            }
        }

        var last = Enumerable.Range(0, count).Where(node => linked[node]).Select(node => layer[node]).DefaultIfEmpty(-1).Max() + 1;
        for (var node = 0; node < count; node++)
        {
            if (!linked[node])
            {
                layer[node] = last;
            }
        }

        return layer;
    }

    // The nodes of each layer in their order: first by type, then by the mean place of their neighbours in the layers above (down) and below (up)
    private static List<List<int>> Order(IReadOnlyList<BundleNode> nodes, HashSet<int>[] acyclic, int[] layers)
    {
        var neighbours = Enumerable.Range(0, nodes.Count).Select(_ => new List<int>()).ToArray();
        for (var from = 0; from < acyclic.Length; from++)
        {
            foreach (var to in acyclic[from])
            {
                neighbours[from].Add(to);
                neighbours[to].Add(from);
            }
        }

        var order = Enumerable.Range(0, layers.Max() + 1)
                              .Select(l => Enumerable.Range(0, nodes.Count).Where(n => layers[n] == l)
                                                     .OrderBy(n => nodes[n].ResourceType, StringComparer.Ordinal).ThenBy(n => n).ToList())
                              .ToList();
        var place = new double[nodes.Count];
        void Remember(List<int> layer)
        {
            for (var i = 0; i < layer.Count; i++)
            {
                place[layer[i]] = (i + 0.5) / layer.Count;
            }
        }

        order.ForEach(Remember);
        for (var sweep = 0; sweep < Sweeps; sweep++)
        {
            var down = sweep % 2 == 0;
            var sequence = down ? Enumerable.Range(1, order.Count - 1) : Enumerable.Range(0, order.Count - 1).Reverse();
            foreach (var l in sequence)
            {
                var layer = order[l];
                var near = down ? (Func<int, bool>)(n => layers[n] < l) : n => layers[n] > l;
                var weight = layer.ToDictionary(n => n, n =>
                {
                    var around = neighbours[n].Where(near).ToList();
                    return around.Count == 0 ? place[n] : around.Average(m => place[m]);
                });
                order[l] = [.. layer.OrderBy(n => weight[n])];
                Remember(order[l]);
            }
        }

        return order;
    }

    private static BundleDrawing Place(IReadOnlyList<BundleNode> nodes, IReadOnlyList<BundleEdge> edges, Dictionary<string, int> index, List<List<int>> order,
                                       int minPerRow)
    {
        var x = new double[nodes.Count];
        var y = new double[nodes.Count];
        var top = 0.0;
        foreach (var layer in order.Where(layer => layer.Count > 0))
        {
            // A long layer wraps into rows about twice as wide as they are many: fifty observations are five rows of ten
            var perRow = Math.Max(minPerRow, (int)Math.Ceiling(Math.Sqrt(2.0 * layer.Count)));
            foreach (var row in layer.Chunk(perRow))
            {
                for (var i = 0; i < row.Length; i++)
                {
                    x[row[i]] = (i - (row.Length - 1) / 2.0) * (NodeWidth + Gap);
                    y[row[i]] = top;
                }

                top += NodeHeight + RowGap;
            }

            top += LayerGap - RowGap;
        }

        var left = x.Min() - NodeWidth / 2 - Padding;
        var placed = nodes.Select((node, i) => new PlacedNode(node, x[i] - NodeWidth / 2 - left, y[i] + Padding)).ToList();
        var paths = edges.Select(edge => new PlacedEdge(edge, PathOf(placed[index[edge.From]], placed[index[edge.To]]))).ToList();
        var width = placed.Max(node => node.X) + NodeWidth + Padding;
        var height = placed.Max(node => node.Y) + NodeHeight + Padding;
        return new BundleDrawing(placed, paths, width, height);
    }

    // A curve from the side of the node that refers toward the node referred to: up, down, or an arch over a row
    private static string PathOf(PlacedNode from, PlacedNode to)
    {
        var fromX = from.X + NodeWidth / 2;
        var toX = to.X + NodeWidth / 2;
        if (Math.Abs(from.Y - to.Y) < 1)
        {
            var arch = from.Y - LayerGap / 2;
            return Format($"M {fromX} {from.Y} C {fromX} {arch} {toX} {arch} {toX} {to.Y}");
        }

        var up = to.Y < from.Y;
        var fromY = up ? from.Y : from.Y + NodeHeight;
        var toY = up ? to.Y + NodeHeight : to.Y;
        var middle = (fromY + toY) / 2;
        return Format($"M {fromX} {fromY} C {fromX} {middle} {toX} {middle} {toX} {toY}");
    }

    private static string Format(FormattableString path)
    {
        return path.ToString(CultureInfo.InvariantCulture);
    }
}
