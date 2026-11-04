using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// Checks every command that runs the shared, scratch-based BFS (eccentricity, diameter in its exact,
/// sampled and iFUB modes, closeness, harmonic, and shortest-path distances between attribute
/// categories) against an independent reference BFS written here, which only uses the public
/// Network.GetNodeAlters query. Covers 1-mode (undirected, and directed under out/in/both traversal),
/// 2-mode (unpacked and packed, since only packed layers use the precomputed arrays), and a mix of
/// 1-mode and 2-mode layers, on random graphs that include several components.
/// </summary>
public class BFSEquivalenceTests
{
    // ── Reference implementation (public API only) ────────────────────────────

    /// <summary>Plain BFS over the union of the given layers' alters, via Network.GetNodeAlters.</summary>
    private static Dictionary<uint, int> ReferenceBfs(Network net, string[] layers, uint source, EdgeTraversal traversal, HashSet<uint>? restrictTo = null)
    {
        var dist = new Dictionary<uint, int> { [source] = 0 };
        var queue = new Queue<uint>();
        queue.Enqueue(source);
        while (queue.Count > 0)
        {
            uint u = queue.Dequeue();
            var alters = net.GetNodeAlters(layers, u, traversal, true);
            Assert.True(alters.Success, alters.Message);
            foreach (uint v in alters.Value!)
                if ((restrictTo == null || restrictTo.Contains(v)) && !dist.ContainsKey(v))
                {
                    dist[v] = dist[u] + 1;
                    queue.Enqueue(v);
                }
        }
        return dist;
    }

    /// <summary>Largest weakly connected component (direction ignored), as diameter() uses.</summary>
    private static HashSet<uint> ReferenceLargestComponent(Network net, string[] layers)
    {
        var seen = new HashSet<uint>();
        HashSet<uint> best = [];
        foreach (uint id in net.Nodeset.NodeIdArray)
        {
            if (seen.Contains(id)) continue;
            var comp = ReferenceBfs(net, layers, id, EdgeTraversal.Both).Keys.ToHashSet();
            seen.UnionWith(comp);
            if (comp.Count > best.Count) best = comp;
        }
        return best;
    }

    private static double FloatAttr(Network net, uint id, string attr)
    {
        var r = net.Nodeset.GetNodeAttribute(id, attr);
        Assert.True(r.Success, r.Message);
        return Convert.ToDouble(r.Value.Value.GetValue(r.Value.Type));
    }

    // ── Test networks ──────────────────────────────────────────────────────────

    public enum Kind
    {
        Undirected, Directed, TwoMode, TwoModePacked, Mixed,
        // Packed 1-mode layers take the BFS's direct-CSR path
        UndirectedPacked, DirectedPacked, MixedPacked,
        // Node ids spread over the whole uint range: no direct id lookup array, dictionary fallback
        UndirectedPackedSparseIds, DirectedPackedSparseIds
    }

    private static bool IsSparseIds(Kind kind) => kind is Kind.UndirectedPackedSparseIds or Kind.DirectedPackedSparseIds;

    /// <summary>
    /// Random network of the given kind; returns the layer names to analyze. Sparse enough to have
    /// several components, with one clearly largest.
    /// </summary>
    private static (Network Net, string[] Layers) MakeNetwork(Kind kind, int seed)
    {
        Misc.SetRandomSeed(seed);
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 90; i++)
            ns.AddNode(IsSparseIds(kind) ? i * 40_000_000u : i);
        var net = new Network("net", ns);
        switch (kind)
        {
            case Kind.UndirectedPacked:
            case Kind.UndirectedPackedSparseIds:
                net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
                Assert.True(Generators.GenerateErdosRenyiLayer(net, "l", 0.035).Success);
                Assert.True(net.Pack("l").Success);
                return (net, ["l"]);
            case Kind.DirectedPacked:
            case Kind.DirectedPackedSparseIds:
                net.AddLayerOneMode("l", EdgeDirectionality.Directed, EdgeType.Binary, false);
                Assert.True(Generators.GenerateErdosRenyiLayer(net, "l", 0.03).Success);
                Assert.True(net.Pack("l").Success);
                return (net, ["l"]);
            case Kind.MixedPacked:
                net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
                Assert.True(Generators.GenerateErdosRenyiLayer(net, "l", 0.012).Success);
                net.AddLayerTwoMode("a");
                Assert.True(Generators.GenerateRandomTwoModeLayer(net, "a", 60, 1).Success);
                Assert.True(net.Pack(null).Success);
                return (net, ["l", "a"]);
            case Kind.Undirected:
                net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
                Assert.True(Generators.GenerateErdosRenyiLayer(net, "l", 0.035).Success);
                return (net, ["l"]);
            case Kind.Directed:
                net.AddLayerOneMode("l", EdgeDirectionality.Directed, EdgeType.Binary, false);
                Assert.True(Generators.GenerateErdosRenyiLayer(net, "l", 0.03).Success);
                return (net, ["l"]);
            case Kind.TwoMode:
            case Kind.TwoModePacked:
                net.AddLayerTwoMode("a");
                Assert.True(Generators.GenerateRandomTwoModeLayer(net, "a", 45, 1).Success);
                if (kind == Kind.TwoModePacked)
                    Assert.True(net.Pack("a").Success);
                return (net, ["a"]);
            default:
                net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
                Assert.True(Generators.GenerateErdosRenyiLayer(net, "l", 0.012).Success);
                net.AddLayerTwoMode("a");
                Assert.True(Generators.GenerateRandomTwoModeLayer(net, "a", 60, 1).Success);
                Assert.True(net.Pack("a").Success);
                return (net, ["l", "a"]);
        }
    }

    public static IEnumerable<object[]> SingleLayerCases()
    {
        foreach (int seed in new[] { 11, 12, 13 })
        {
            yield return [Kind.Undirected, EdgeTraversal.Out, seed];
            yield return [Kind.Directed, EdgeTraversal.Out, seed];
            yield return [Kind.Directed, EdgeTraversal.In, seed];
            yield return [Kind.Directed, EdgeTraversal.Both, seed];
            yield return [Kind.TwoMode, EdgeTraversal.Out, seed];
            yield return [Kind.TwoModePacked, EdgeTraversal.Out, seed];
            yield return [Kind.UndirectedPacked, EdgeTraversal.Out, seed];
            yield return [Kind.DirectedPacked, EdgeTraversal.Out, seed];
            yield return [Kind.DirectedPacked, EdgeTraversal.In, seed];
            yield return [Kind.DirectedPacked, EdgeTraversal.Both, seed];
            yield return [Kind.UndirectedPackedSparseIds, EdgeTraversal.Out, seed];
            yield return [Kind.DirectedPackedSparseIds, EdgeTraversal.In, seed];
            yield return [Kind.DirectedPackedSparseIds, EdgeTraversal.Both, seed];
        }
    }

    public static IEnumerable<object[]> AnyLayerCases()
    {
        foreach (var c in SingleLayerCases())
            yield return c;
        foreach (int seed in new[] { 11, 12, 13 })
        {
            yield return [Kind.Mixed, EdgeTraversal.Out, seed];
            yield return [Kind.MixedPacked, EdgeTraversal.Out, seed];
        }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SingleLayerCases))]
    public void Eccentricity_MatchesReference(Kind kind, EdgeTraversal traversal, int seed)
    {
        var (net, layers) = MakeNetwork(kind, seed);
        Assert.True(Analyses.Eccentricity(net, layers[0], "ecc", traversal).Success);
        foreach (uint id in net.Nodeset.NodeIdArray)
        {
            int expected = ReferenceBfs(net, layers, id, traversal).Values.Max();
            Assert.Equal(expected, (int)FloatAttr(net, id, "ecc"));
        }
    }

    [Theory]
    [MemberData(nameof(SingleLayerCases))]
    public void Diameter_Exact_MatchesReference(Kind kind, EdgeTraversal traversal, int seed)
    {
        var (net, layers) = MakeNetwork(kind, seed);
        var comp = ReferenceLargestComponent(net, layers);
        long max = 0, sum = 0, pairs = 0;
        foreach (uint s in comp)
            foreach (var (t, d) in ReferenceBfs(net, layers, s, traversal, comp))
            {
                if (t == s) continue;
                max = Math.Max(max, d);
                sum += d;
                pairs++;
            }

        var r = Analyses.Diameter(net, layers[0], traversal: traversal);
        Assert.True(r.Success, r.Message);
        Assert.Equal(comp.Count, (int)r.Value!["ComponentSize"]);
        Assert.Equal((int)max, (int)r.Value!["Diameter"]);
        Assert.Equal(pairs, (long)r.Value!["PairsConsidered"]);
        Assert.Equal(pairs > 0 ? (double)sum / pairs : 0.0, (double)r.Value!["AvgDistance"], 10);

        // diameteronly: iFUB where symmetric, the exhaustive pass otherwise - same diameter either way
        var fast = Analyses.Diameter(net, layers[0], traversal: traversal, diameterOnly: true);
        Assert.True(fast.Success, fast.Message);
        Assert.Equal((int)max, (int)fast.Value!["Diameter"]);
    }

    [Theory]
    [MemberData(nameof(SingleLayerCases))]
    public void Diameter_SampledWithAllSources_MatchesReference(Kind kind, EdgeTraversal traversal, int seed)
    {
        // A sample at least as large as the component uses every node as a source, so the
        // estimate is deterministic: the mean, over sources, of each source's mean distance.
        var (net, layers) = MakeNetwork(kind, seed);
        var comp = ReferenceLargestComponent(net, layers);
        double total = 0;
        foreach (uint s in comp)
        {
            var dist = ReferenceBfs(net, layers, s, traversal, comp);
            int count = dist.Count - 1;
            total += count > 0 ? (double)dist.Values.Sum() / count : 0.0;
        }
        double expected = comp.Count > 0 ? total / comp.Count : 0.0;

        var r = Analyses.Diameter(net, layers[0], sampleSize: net.Nodeset.Count, numSweeps: 0, traversal: traversal);
        Assert.True(r.Success, r.Message);
        Assert.Equal(expected, (double)r.Value!["AvgDistance"], 10);
    }

    [Theory]
    [MemberData(nameof(AnyLayerCases))]
    public void ClosenessAndHarmonic_MatchReference(Kind kind, EdgeTraversal traversal, int seed)
    {
        var (net, layers) = MakeNetwork(kind, seed);
        Assert.True(Analyses.ClosenessCentrality(net, layers, "clo", traversal: traversal).Success);
        Assert.True(Analyses.HarmonicCentrality(net, layers, "har", normalize: true, traversal: traversal).Success);
        int n = net.Nodeset.Count;
        foreach (uint id in net.Nodeset.NodeIdArray)
        {
            var dist = ReferenceBfs(net, layers, id, traversal);
            int reach = dist.Count - 1;
            double dSum = dist.Values.Sum();
            double closeness = (dSum > 0 && reach > 0) ? (double)reach * reach / ((n - 1.0) * dSum) : 0.0;
            double harmonic = dist.Where(kv => kv.Key != id).Sum(kv => 1.0 / kv.Value) / (n - 1);
            Assert.Equal(closeness, FloatAttr(net, id, "clo"), 1e-6);
            Assert.Equal(harmonic, FloatAttr(net, id, "har"), 1e-6);
        }
    }

    [Theory]
    [MemberData(nameof(AnyLayerCases))]
    public void ShortestPathsBetweenAttributeCategories_MatchReference(Kind kind, EdgeTraversal traversal, int seed)
    {
        _ = traversal; // this command always follows outbound ties
        var (net, layers) = MakeNetwork(kind, seed);
        net.Nodeset.DefineNodeAttribute("grp", "int");
        foreach (uint id in net.Nodeset.NodeIdArray)
            net.Nodeset.SetNodeAttribute(id, "grp", (id % 3).ToString());

        var sums = new Dictionary<(uint, uint), (double Sum, int Count)>();
        foreach (uint s in net.Nodeset.NodeIdArray)
            foreach (var (t, d) in ReferenceBfs(net, layers, s, EdgeTraversal.Out))
            {
                if (t == s) continue;
                var key = (s % 3, t % 3);
                var cur = sums.GetValueOrDefault(key);
                sums[key] = (cur.Sum + d, cur.Count + 1);
            }

        var r = Distance.ShortestPathsNodeAttributeDistances(net, "grp", layers);
        Assert.True(r.Success, r.Message);
        var resultNet = (Network)r.Value!.MainStructure;
        var avg = (ILayerOneMode)resultNet.Layers["grp_sp_avg"];
        var cnt = (ILayerOneMode)resultNet.Layers["grp_sp_count"];
        // Category labels "0","1","2" sort to result node ids 0,1,2
        for (uint c1 = 0; c1 < 3; c1++)
            for (uint c2 = 0; c2 < 3; c2++)
            {
                var (sum, count) = sums.GetValueOrDefault((c1, c2));
                Assert.Equal((float)count, cnt.GetEdgeValue(c1, c2), 3);
                if (count > 0)
                    Assert.Equal(sum / count, avg.GetEdgeValue(c1, c2), 1e-5);
            }
    }
}