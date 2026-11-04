using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// Checks shortestpath()/shortestpaths() (bidirectional BFS, which stops at the first meeting node and as
/// soon as either side runs out of nodes) against a plain single-source BFS built from the public API:
/// every distance must match, including -1 for unreachable pairs (isolated nodes, separate components,
/// one-way reachability on directed layers), and every returned path must be a real path of exactly that
/// length from source to target. Covers 1-mode undirected/directed (packed and unpacked), multilayer,
/// 2-mode (packed and unpacked) and mixed 1-mode + 2-mode, with and without returned paths.
/// </summary>
[Collection("MaxThreadsLimit")] // sets maxthreads: run apart from all other tests
public class ShortestPathReferenceTests : IDisposable
{
    public void Dispose() => UserSettings.MaxDegreeOfParallelism = -1;

    /// <summary>Out-neighbours (or members of shared hyperedges) per node, over all given layers.</summary>
    private static Dictionary<uint, HashSet<uint>> Adjacency(Network net, string[] layers)
    {
        var adj = net.Nodeset.NodeIdArray.ToDictionary(id => id, _ => new HashSet<uint>());
        foreach (string name in layers)
        {
            if (net.Layers[name] is ILayerTwoMode two)
            {
                foreach (var (_, members) in two.GetAllHyperedgeData())
                    foreach (uint a in members)
                        foreach (uint b in members)
                            if (a != b) adj[a].Add(b);
            }
            else
            {
                var one = (ILayerOneMode)net.Layers[name];
                foreach (uint id in net.Nodeset.NodeIdArray)
                    foreach (uint v in one.GetNodeAlters(id, EdgeTraversal.Out))
                        adj[id].Add(v);
            }
        }
        return adj;
    }

    private static Dictionary<uint, int> Bfs(Dictionary<uint, HashSet<uint>> adj, uint source)
    {
        var dist = new Dictionary<uint, int> { [source] = 0 };
        var queue = new Queue<uint>([source]);
        while (queue.Count > 0)
        {
            uint u = queue.Dequeue();
            foreach (uint v in adj[u])
                if (dist.TryAdd(v, dist[u] + 1))
                    queue.Enqueue(v);
        }
        return dist;
    }

    private static void Check(Network net, string[] layers, int seed)
    {
        var adj = Adjacency(net, layers);
        var ids = net.Nodeset.NodeIdArray;
        var rng = new Random(seed);
        var pairs = new List<(uint, uint)>();
        for (int i = 0; i < 150; i++)
            pairs.Add((ids[rng.Next(ids.Length)], ids[rng.Next(ids.Length)]));
        pairs.Add((ids[0], ids[0]));

        foreach (bool returnPath in new[] { false, true })
        {
            var r = Analyses.ShortestPaths(net, layers, pairs, returnPath);
            Assert.True(r.Success, r.Message);
            for (int i = 0; i < pairs.Count; i++)
            {
                var (s, t) = pairs[i];
                int expected = Bfs(adj, s).GetValueOrDefault(t, -1);
                var res = r.Value![i].Result;
                Assert.Equal(expected, res.Distance);
                if (returnPath && expected >= 0)
                {
                    var path = res.Path!;
                    Assert.Equal(expected + 1, path.Length);
                    Assert.Equal(s, path[0]);
                    Assert.Equal(t, path[^1]);
                    for (int k = 0; k + 1 < path.Length; k++)
                        Assert.Contains(path[k + 1], adj[path[k]]);
                }
                else
                    Assert.True(returnPath ? expected < 0 && res.Path == null : res.Path == null);

                if (i < 20)
                {
                    var single = Analyses.ShortestPath(net, layers, s, t, returnPath);
                    Assert.True(single.Success, single.Message);
                    Assert.Equal(expected, single.Value!.Distance);
                    if (returnPath) Assert.Equal(res.Path, single.Value.Path);
                }
            }
        }
    }

    /// <summary>n nodes, of which every tenth gets no ties at all (isolated), with non-consecutive ids.</summary>
    private static (Network Net, uint[] Active) Make(int n, int seed)
    {
        Misc.SetRandomSeed(seed);
        var ns = new Nodeset("ns");
        for (uint i = 0; i < n; i++) ns.AddNode(i * 3 + 1);
        var ids = ns.NodeIdArray;
        return (new Network("net", ns), ids.Where((_, i) => i % 10 != 0).ToArray());
    }

    private static void RandomEdges(Network net, string layer, uint[] nodes, int count, Random rng)
    {
        for (int i = 0; i < count; i++)
            net.AddEdge(layer, nodes[rng.Next(nodes.Length)], nodes[rng.Next(nodes.Length)]);
    }

    public static IEnumerable<object[]> Cases() =>
        from seed in new[] { 1, 2 }
        from packed in new[] { false, true }
        from threads in new[] { 1, 4 }
        select new object[] { seed, packed, threads };

    [Theory]
    [MemberData(nameof(Cases))]
    public void OneMode_Undirected(int seed, bool packed, int threads)
    {
        UserSettings.MaxDegreeOfParallelism = threads;
        var (net, active) = Make(400, seed);
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        RandomEdges(net, "l", active, 420, new Random(seed));
        if (packed) Assert.True(net.Pack("l").Success);
        Check(net, ["l"], seed);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void OneMode_Directed(int seed, bool packed, int threads)
    {
        UserSettings.MaxDegreeOfParallelism = threads;
        var (net, active) = Make(400, seed);
        net.AddLayerOneMode("l", EdgeDirectionality.Directed, EdgeType.Binary, true);
        RandomEdges(net, "l", active, 700, new Random(seed));
        if (packed) Assert.True(net.Pack("l").Success);
        Check(net, ["l"], seed);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Multilayer(int seed, bool packed, int threads)
    {
        UserSettings.MaxDegreeOfParallelism = threads;
        var (net, active) = Make(400, seed);
        var rng = new Random(seed);
        net.AddLayerOneMode("d", EdgeDirectionality.Directed, EdgeType.Valued, false);
        net.AddLayerOneMode("u", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        for (int i = 0; i < 300; i++)
            net.AddEdge("d", active[rng.Next(active.Length)], active[rng.Next(active.Length)], 2.5f);
        RandomEdges(net, "u", active, 200, rng);
        if (packed) Assert.True(net.Pack("d").Success);
        Check(net, ["d", "u"], seed);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TwoMode(int seed, bool packed, int threads)
    {
        UserSettings.MaxDegreeOfParallelism = threads;
        var (net, active) = Make(400, seed);
        net.AddLayerTwoMode("h");
        var rng = new Random(seed);
        for (int h = 0; h < 120; h++)
            net.AddHyperedge("h", $"h{h}", Enumerable.Range(0, 1 + rng.Next(4)).Select(_ => active[rng.Next(active.Length)]).Distinct().ToArray());
        if (packed) Assert.True(net.Pack("h").Success);
        Check(net, ["h"], seed);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Mixed_OneModeAndTwoMode(int seed, bool packed, int threads)
    {
        UserSettings.MaxDegreeOfParallelism = threads;
        var (net, active) = Make(400, seed);
        var rng = new Random(seed);
        net.AddLayerTwoMode("dyn");
        net.AddLayerTwoMode("stat");
        for (int h = 0; h < 50; h++)
        {
            net.AddHyperedge("dyn", $"a{h}", Enumerable.Range(0, 3).Select(_ => active[rng.Next(active.Length)]).Distinct().ToArray());
            net.AddHyperedge("stat", $"b{h}", Enumerable.Range(0, 3).Select(_ => active[rng.Next(active.Length)]).Distinct().ToArray());
        }
        Assert.True(net.Pack("stat").Success);
        net.AddLayerOneMode("d", EdgeDirectionality.Directed, EdgeType.Binary, false);
        RandomEdges(net, "d", active, 250, rng);
        if (packed) Assert.True(net.Pack("d").Success);
        Check(net, ["dyn", "stat", "d"], seed);
    }

    [Fact]
    public void IsolatedNode_InLargeComponent_IsUnreachable()
    {
        var (net, active) = Make(2000, 3);
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        for (int i = 0; i + 1 < active.Length; i++)
            net.AddEdge("l", active[i], active[i + 1]); // one long chain through all non-isolated nodes
        Assert.True(net.Pack("l").Success);
        uint isolated = net.Nodeset.NodeIdArray[0];
        Assert.Equal(-1, Analyses.ShortestPath(net, ["l"], active[0], isolated).Value!.Distance);
        Assert.Equal(-1, Analyses.ShortestPath(net, ["l"], isolated, active[^1]).Value!.Distance);
        Assert.Equal(active.Length - 1, Analyses.ShortestPath(net, ["l"], active[0], active[^1]).Value!.Distance);
    }
}