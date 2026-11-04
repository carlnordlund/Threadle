using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// Checks betweennesscentrality() and edgebetweennesscentrality() against an independent, textbook
/// Brandes implementation written here on a multigraph built only from the public API. The multigraph
/// encodes Threadle's tie semantics: a 1-mode tie counts once per layer it is in (on a directed layer
/// under 'both' traversal, once even if it exists in both directions - GetNodeAlters' de-duplicated
/// union); a 2-mode tie counts once per hyperedge the two nodes share. Shortest paths are counted with
/// that multiplicity. Covers 1-mode undirected/directed (out/in/both)/valued/multilayer, 2-mode packed
/// and unpacked, mixed 1-mode + 2-mode, and sampled sources.
/// </summary>
public class BetweennessReferenceTests
{
    // ── Reference ────────────────────────────────────────────────────────────

    /// <summary>For each node index, the list of (neighbour index, multiplicity) along the traversal direction.</summary>
    private static List<(int V, int Mult)>[] MultiAdjacency(Network net, string[] layers, EdgeTraversal traversal, uint[] ids, Dictionary<uint, int> idx)
    {
        int n = ids.Length;
        var counts = new Dictionary<int, int>[n];
        for (int i = 0; i < n; i++) counts[i] = new Dictionary<int, int>();
        foreach (string name in layers)
        {
            var layer = net.Layers[name];
            if (layer is ILayerTwoMode two)
            {
                foreach (var (_, members) in two.GetAllHyperedgeData())
                {
                    var ms = members.Where(idx.ContainsKey).Select(m => idx[m]).ToArray();
                    foreach (int a in ms)
                        foreach (int b in ms)
                            if (a != b)
                                counts[a][b] = counts[a].GetValueOrDefault(b) + 1;
                }
            }
            else
            {
                var one = (ILayerOneMode)layer;
                for (int i = 0; i < n; i++)
                    foreach (uint v in one.GetNodeAlters(ids[i], traversal))
                        if (idx.TryGetValue(v, out int vi) && vi != i)
                            counts[i][vi] = counts[i].GetValueOrDefault(vi) + 1;
            }
        }
        return counts.Select(c => c.Select(kv => (kv.Key, kv.Value)).ToList()).ToArray();
    }

    /// <summary>Textbook Brandes on a multigraph: raw node betweenness and raw edge credits (v, w) per source.</summary>
    private static (double[] Node, Dictionary<(int, int), double> Edge) Brandes(List<(int V, int Mult)>[] adj, IEnumerable<int> sources)
    {
        int n = adj.Length;
        var node = new double[n];
        var edge = new Dictionary<(int, int), double>();
        foreach (int s in sources)
        {
            var dist = Enumerable.Repeat(-1, n).ToArray();
            var sigma = new double[n];
            var order = new List<int>();
            dist[s] = 0; sigma[s] = 1;
            var q = new Queue<int>(); q.Enqueue(s);
            while (q.Count > 0)
            {
                int u = q.Dequeue(); order.Add(u);
                foreach (var (v, mult) in adj[u])
                {
                    if (dist[v] < 0) { dist[v] = dist[u] + 1; q.Enqueue(v); }
                    if (dist[v] == dist[u] + 1) sigma[v] += sigma[u] * mult;
                }
            }
            var delta = new double[n];
            // predecessors: v with an edge v->w (multiplicity) and dist[v] == dist[w]-1
            var preds = new List<(int V, int Mult)>[n];
            for (int i = 0; i < n; i++) preds[i] = [];
            foreach (int u in order)
                foreach (var (v, mult) in adj[u])
                    if (dist[v] == dist[u] + 1) preds[v].Add((u, mult));
            for (int i = order.Count - 1; i >= 0; i--)
            {
                int w = order[i];
                foreach (var (v, mult) in preds[w])
                {
                    double c = mult * sigma[v] / sigma[w] * (1 + delta[w]);
                    delta[v] += c;
                    edge[(v, w)] = edge.GetValueOrDefault((v, w)) + c;
                }
                if (w != s) node[w] += delta[w];
            }
        }
        return (node, edge);
    }

    private static double Attr(Network net, uint id, string attr)
    {
        var r = net.Nodeset.GetNodeAttribute(id, attr);
        Assert.True(r.Success, r.Message);
        return Convert.ToDouble(r.Value.Value.GetValue(r.Value.Type));
    }

    private static void AssertClose(double expected, double actual)
    {
        // stored as 32-bit floats
        Assert.True(Math.Abs(expected - actual) <= 1e-6 * Math.Max(1.0, Math.Abs(expected)), $"expected {expected}, got {actual}");
    }

    private static void CheckNodeBetweenness(Network net, string[] layers, EdgeTraversal traversal, int sampleSize = 0)
    {
        uint[] ids = net.Nodeset.NodeIdArray;
        var idx = new Dictionary<uint, int>();
        for (int i = 0; i < ids.Length; i++) idx[ids[i]] = i;

        // Same sample as the command draws (SampleNodes uses Misc.Random): reseed before each
        Misc.SetRandomSeed(77);
        Assert.True(Analyses.BetweennessCentrality(net, layers, "bw", sampleSize, traversal).Success);
        int n = ids.Length;
        IEnumerable<int> sources = Enumerable.Range(0, n);
        if (sampleSize > 0 && sampleSize < n)
        {
            // Reproduce CentralityFunctions.SampleNodes' partial Fisher-Yates with the same seed
            Misc.SetRandomSeed(77);
            var pool = (uint[])ids.Clone();
            var rnd = typeof(Misc).GetProperty("Random", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            var random = (Random)rnd.GetValue(null)!;
            for (int i = 0; i < sampleSize; i++)
            {
                int j = random.Next(i, pool.Length);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            sources = pool.Take(sampleSize).Select(id => idx[id]);
        }

        var (raw, _) = Brandes(MultiAdjacency(net, layers, traversal, ids, idx), sources);
        double norm = (n - 1.0) * (n - 2.0);
        double scale = (sampleSize > 0 && sampleSize < n) ? (double)n / sampleSize : 1.0;
        for (int i = 0; i < n; i++)
            AssertClose(raw[i] * scale / norm, Attr(net, ids[i], "bw"));
    }

    private static Network Make(int n, int seed)
    {
        Misc.SetRandomSeed(seed);
        var ns = new Nodeset("ns");
        for (uint i = 0; i < n; i++) ns.AddNode(i);
        return new Network("net", ns);
    }

    public static IEnumerable<object[]> Seeds() => [[1], [2], [3]];

    // ── Node betweenness ─────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Seeds))]
    public void OneMode_Undirected_PackedAndUnpacked(int seed)
    {
        foreach (bool packed in new[] { false, true })
        {
            var net = Make(120, seed);
            net.AddLayerOneMode("u", EdgeDirectionality.Undirected, EdgeType.Binary, false);
            Generators.GenerateErdosRenyiLayer(net, "u", 0.03);
            if (packed) net.Pack(null);
            CheckNodeBetweenness(net, ["u"], EdgeTraversal.Out);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void OneMode_Directed_OutInBoth(int seed)
    {
        foreach (bool packed in new[] { false, true })
        {
            var net = Make(100, seed);
            net.AddLayerOneMode("d", EdgeDirectionality.Directed, EdgeType.Binary, false);
            Generators.GenerateErdosRenyiLayer(net, "d", 0.03);
            for (uint k = 0; k < 100; k += 7) { net.AddEdge("d", k, (k + 1) % 100); net.AddEdge("d", (k + 1) % 100, k); }
            if (packed) net.Pack(null);
            foreach (var t in new[] { EdgeTraversal.Out, EdgeTraversal.In, EdgeTraversal.Both })
                CheckNodeBetweenness(net, ["d"], t);
        }
    }

    [Fact]
    public void OneMode_MultilayerWithSharedEdges_CountsEachLayer()
    {
        foreach (bool packed in new[] { false, true })
        {
            var net = Make(90, 4);
            net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Valued, true);
            net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Binary, false);
            Generators.GenerateErdosRenyiLayer(net, "b", 0.04);
            for (uint k = 0; k < 90; k += 3) net.AddEdge("a", k, (k + 5) % 90, value: 2.5f);
            for (uint k = 0; k < 90; k += 11) net.AddEdge("a", k, k, value: 1f);
            for (uint k = 0; k < 90; k += 9) net.AddEdge("b", k, (k + 5) % 90); // some edges in both layers
            if (packed) net.Pack(null);
            CheckNodeBetweenness(net, ["a", "b"], EdgeTraversal.Out);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TwoMode_PackedAndUnpacked(int seed)
    {
        foreach (bool packed in new[] { false, true })
        {
            var net = Make(150, seed);
            net.AddLayerTwoMode("x");
            Generators.GenerateRandomTwoModeLayer(net, "x", 60, 2);
            net.AddHyperedge("x", "big", Enumerable.Range(0, 25).Select(i => (uint)(i * 5)).ToArray());
            if (packed) net.Pack(null);
            CheckNodeBetweenness(net, ["x"], EdgeTraversal.Out);
        }
    }

    [Fact]
    public void TwoMode_PairsSharingSeveralHyperedges_CountMultiplicity()
    {
        // 1-2 share three hyperedges, 2-3 share one; 1-4-3 is another two-step path: node 2 carries
        // three times as many shortest 1->3 paths as node 4
        foreach (bool packed in new[] { false, true })
        {
            var net = Make(5, 1);
            net.AddLayerTwoMode("x");
            net.AddHyperedge("x", "a", [1, 2]);
            net.AddHyperedge("x", "b", [1, 2]);
            net.AddHyperedge("x", "c", [1, 2]);
            net.AddHyperedge("x", "d", [2, 3]);
            net.AddHyperedge("x", "e", [1, 4]);
            net.AddHyperedge("x", "f", [4, 3]);
            if (packed) net.Pack(null);
            CheckNodeBetweenness(net, ["x"], EdgeTraversal.Out);
            Assert.True(Attr(net, 2, "bw") > Attr(net, 4, "bw"));
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Mixed_OneModeAndTwoMode(int seed)
    {
        var net = Make(140, seed);
        net.AddLayerOneMode("d", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddLayerTwoMode("x");
        Generators.GenerateErdosRenyiLayer(net, "d", 0.01);
        Generators.GenerateRandomTwoModeLayer(net, "x", 50, 1);
        net.Pack(null);
        foreach (var t in new[] { EdgeTraversal.Out, EdgeTraversal.In, EdgeTraversal.Both })
            CheckNodeBetweenness(net, ["d", "x"], t);
    }

    [Fact]
    public void Sampled_OneModeAndTwoMode()
    {
        var one = Make(150, 8);
        one.AddLayerOneMode("u", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        Generators.GenerateErdosRenyiLayer(one, "u", 0.03);
        CheckNodeBetweenness(one, ["u"], EdgeTraversal.Out, sampleSize: 40);

        var two = Make(150, 8);
        two.AddLayerTwoMode("x");
        Generators.GenerateRandomTwoModeLayer(two, "x", 60, 2);
        two.Pack(null);
        CheckNodeBetweenness(two, ["x"], EdgeTraversal.Out, sampleSize: 40);
    }

    // ── Edge betweenness ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(EdgeDirectionality.Undirected, false)]
    [InlineData(EdgeDirectionality.Undirected, true)]
    [InlineData(EdgeDirectionality.Directed, false)]
    [InlineData(EdgeDirectionality.Directed, true)]
    public void EdgeBetweenness_MatchesReference(EdgeDirectionality dir, bool packed)
    {
        var net = Make(100, 5);
        net.AddLayerOneMode("l", dir, EdgeType.Binary, false);
        Generators.GenerateErdosRenyiLayer(net, "l", 0.04);
        if (packed) net.Pack(null);
        Assert.True(Analyses.EdgeBetweennessCentrality(net, "l", "eb", 0, normalize: false).Success);

        uint[] ids = net.Nodeset.NodeIdArray;
        var idx = new Dictionary<uint, int>();
        for (int i = 0; i < ids.Length; i++) idx[ids[i]] = i;
        var (_, edges) = Brandes(MultiAdjacency(net, ["l"], EdgeTraversal.Out, ids, idx), Enumerable.Range(0, ids.Length));
        var expected = new Dictionary<(uint, uint), double>();
        foreach (var ((v, w), c) in edges)
        {
            uint a = ids[v], b = ids[w];
            var key = (dir == EdgeDirectionality.Directed || a < b) ? (a, b) : (b, a);
            expected[key] = expected.GetValueOrDefault(key) + c;
        }
        var eb = (ILayerOneMode)net.Layers["eb"];
        foreach (var ((a, b), value) in expected)
            AssertClose(value, eb.GetEdgeValue(a, b));
    }
}