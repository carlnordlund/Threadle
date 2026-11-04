using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// Tests for communitydetection(type=plm): parallel Louvain (PLM). Its results can legitimately differ
/// from sequential Louvain's and between runs, so these check what must hold regardless: clear planted
/// communities are recovered exactly, every node gets a community, the reported modularity is close to
/// Louvain's on random graphs, single-threaded runs are reproducible, and 1-mode, multilayer and 2-mode
/// (packed and unpacked) inputs all work, sequentially and with several threads.
/// </summary>
[Collection("MaxThreadsLimit")] // changes the global thread setting: run apart from all other tests
public class CommunityDetectionPlmTests : IDisposable
{
    public void Dispose() => UserSettings.MaxDegreeOfParallelism = -1;

    private static Network MakeNetwork(int nodeCount)
    {
        var ns = new Nodeset("ns");
        for (uint i = 0; i < nodeCount; i++)
            ns.AddNode(i);
        return new Network("net", ns);
    }

    private static int Community(Network net, uint node, string attr)
    {
        var r = net.Nodeset.GetNodeAttribute(node, attr);
        Assert.True(r.Success, r.Message);
        return (int)r.Value.Value.GetValue(r.Value.Type)!;
    }

    /// <summary>
    /// Asserts that the attribute partitions the nodes exactly into the given groups (community ids are
    /// arbitrary, so this compares "same group" relations).
    /// </summary>
    private static void AssertPartition(Network net, string attr, List<uint[]> groups)
    {
        var seen = new HashSet<int>();
        foreach (var g in groups)
        {
            int c = Community(net, g[0], attr);
            foreach (uint node in g)
                Assert.Equal(c, Community(net, node, attr));
            Assert.True(seen.Add(c), "two planted groups share a community");
        }
    }

    /// <summary>k cliques of the given size, chained by single edges between consecutive cliques.</summary>
    private static (Network Net, List<uint[]> Groups) MakeCliqueChain(int k, int size)
    {
        var net = MakeNetwork(k * size);
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        var groups = new List<uint[]>();
        for (int g = 0; g < k; g++)
        {
            uint b = (uint)(g * size);
            groups.Add(Enumerable.Range(0, size).Select(i => b + (uint)i).ToArray());
            for (uint a = 0; a < size; a++)
                for (uint c = a + 1; c < size; c++)
                    net.AddEdge("l", b + a, b + c);
            if (g > 0)
                net.AddEdge("l", b - 1, b);
        }
        return (net, groups);
    }

    /// <summary>
    /// 2-mode analogue: k groups, each with several hyperedges covering only its own members, plus one
    /// bridging hyperedge of two nodes between consecutive groups.
    /// </summary>
    private static (Network Net, List<uint[]> Groups) MakeTwoModeGroups(int k, int size, bool packed)
    {
        var net = MakeNetwork(k * size);
        net.AddLayerTwoMode("a");
        var groups = new List<uint[]>();
        for (int g = 0; g < k; g++)
        {
            uint b = (uint)(g * size);
            var members = Enumerable.Range(0, size).Select(i => b + (uint)i).ToArray();
            groups.Add(members);
            for (int h = 0; h < 4; h++)
                net.AddHyperedge("a", $"g{g}_h{h}", members.Where((_, i) => i % 4 != h).ToArray());
            if (g > 0)
                net.AddHyperedge("a", $"bridge{g}", [b - 1, b]);
        }
        if (packed)
            Assert.True(net.Pack("a").Success);
        return (net, groups);
    }

    public static IEnumerable<object[]> ThreadCounts() => [[1], [2], [4]];

    [Theory]
    [MemberData(nameof(ThreadCounts))]
    public void OneMode_CliqueChain_RecoversCliques(int threads)
    {
        UserSettings.MaxDegreeOfParallelism = threads;
        var (net, groups) = MakeCliqueChain(8, 12);
        var r = Analyses.CommunityDetectionPLM(net, ["l"], "plm");
        Assert.True(r.Success, r.Message);
        Assert.Equal(8, (int)r.Value!["NbrCommunities"]);
        AssertPartition(net, "plm", groups);

        var louvain = Analyses.CommunityDetectionLouvain(net, ["l"], "lou");
        Assert.Equal((double)louvain.Value!["Modularity"], (double)r.Value!["Modularity"], 10);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    public void TwoMode_PlantedGroups_RecoversGroups(int threads, bool packed)
    {
        UserSettings.MaxDegreeOfParallelism = threads;
        var (net, groups) = MakeTwoModeGroups(6, 10, packed);
        var r = Analyses.CommunityDetectionPLM(net, ["a"], "plm");
        Assert.True(r.Success, r.Message);
        Assert.Equal(6, (int)r.Value!["NbrCommunities"]);
        AssertPartition(net, "plm", groups);
    }

    [Theory]
    [MemberData(nameof(ThreadCounts))]
    public void OneMode_RandomGraphs_ModularityCloseToLouvain(int threads)
    {
        UserSettings.MaxDegreeOfParallelism = threads;
        for (int seed = 0; seed < 3; seed++)
        {
            Misc.SetRandomSeed(100 + seed);
            var net = MakeNetwork(600);
            net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
            Assert.True(Generators.GenerateWattsStrogatzLayer(net, "l", 6, 0.1).Success);
            double plm = (double)Analyses.CommunityDetectionPLM(net, ["l"], "plm").Value!["Modularity"];
            double lou = (double)Analyses.CommunityDetectionLouvain(net, ["l"], "lou").Value!["Modularity"];
            Assert.True(plm > 0.9 * lou, $"PLM modularity {plm} too far below Louvain's {lou}");
        }
    }

    [Theory]
    [MemberData(nameof(ThreadCounts))]
    public void TwoMode_RandomLayer_ModularityCloseToLouvain(int threads)
    {
        UserSettings.MaxDegreeOfParallelism = threads;
        Misc.SetRandomSeed(7);
        var net = MakeNetwork(800);
        net.AddLayerTwoMode("a");
        Assert.True(Generators.GenerateRandomTwoModeLayer(net, "a", 300, 2).Success);
        Assert.True(net.Pack("a").Success);
        double plm = (double)Analyses.CommunityDetectionPLM(net, ["a"], "plm").Value!["Modularity"];
        double lou = (double)Analyses.CommunityDetectionLouvain(net, ["a"], "lou").Value!["Modularity"];
        Assert.True(plm > 0.9 * lou, $"PLM modularity {plm} too far below Louvain's {lou}");
    }

    [Fact]
    public void EveryNodeGetsACommunity_AndSizesAddUp()
    {
        UserSettings.MaxDegreeOfParallelism = 4;
        Misc.SetRandomSeed(3);
        var net = MakeNetwork(500);
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        Assert.True(Generators.GenerateErdosRenyiLayer(net, "l", 0.01).Success);
        var r = Analyses.CommunityDetectionPLM(net, ["l"], "plm");
        Assert.True(r.Success, r.Message);
        var sizes = ((IEnumerable<int>)r.Value!["CommunitySizes"]).ToList();
        Assert.Equal(500, sizes.Sum());
        Assert.Equal((int)r.Value!["NbrCommunities"], sizes.Count);
        var ids = net.Nodeset.NodeIdArray.Select(id => Community(net, id, "plm")).Distinct().Count();
        Assert.Equal(sizes.Count, ids);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleThread_SameSeed_IsReproducible(bool twoMode)
    {
        UserSettings.MaxDegreeOfParallelism = 1;
        Network Make()
        {
            Misc.SetRandomSeed(11);
            var net = MakeNetwork(400);
            if (twoMode)
            {
                net.AddLayerTwoMode("x");
                Assert.True(Generators.GenerateRandomTwoModeLayer(net, "x", 150, 2).Success);
            }
            else
            {
                net.AddLayerOneMode("x", EdgeDirectionality.Undirected, EdgeType.Binary, false);
                Assert.True(Generators.GenerateErdosRenyiLayer(net, "x", 0.015).Success);
            }
            return net;
        }
        var n1 = Make();
        Misc.SetRandomSeed(5);
        var r1 = Analyses.CommunityDetectionPLM(n1, ["x"], "plm");
        var n2 = Make();
        Misc.SetRandomSeed(5);
        var r2 = Analyses.CommunityDetectionPLM(n2, ["x"], "plm");
        Assert.Equal((double)r1.Value!["Modularity"], (double)r2.Value!["Modularity"]);
        foreach (uint id in n1.Nodeset.NodeIdArray)
            Assert.Equal(Community(n1, id, "plm"), Community(n2, id, "plm"));
    }

    [Fact]
    public void Multilayer_And_Resolution_And_Restarts()
    {
        UserSettings.MaxDegreeOfParallelism = 4;
        var (net, groups) = MakeCliqueChain(5, 8);
        net.AddLayerOneMode("extra", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("extra", 0, 1);
        var r = Analyses.CommunityDetectionPLM(net, ["l", "extra"], "plm", resolution: 1.0, numRestarts: 3);
        Assert.True(r.Success, r.Message);
        Assert.Equal(3, (int)r.Value!["NumRestarts"]);
        AssertPartition(net, "plm", groups);

        // A very low resolution favours fewer, larger communities
        var low = Analyses.CommunityDetectionPLM(net, ["l"], "plm_low", resolution: 0.01);
        Assert.True((int)low.Value!["NbrCommunities"] < 5);
    }

    [Fact]
    public void TwoModeLayer_CombinedWithOtherLayer_IsRejected()
    {
        var (net, _) = MakeTwoModeGroups(2, 5, false);
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        var r = Analyses.CommunityDetectionPLM(net, ["a", "l"], "plm");
        Assert.False(r.Success);
    }

    [Fact]
    public void EmptyLayer_GivesSingletons()
    {
        var net = MakeNetwork(5);
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        var r = Analyses.CommunityDetectionPLM(net, ["l"], "plm");
        Assert.True(r.Success, r.Message);
        Assert.Equal(5, (int)r.Value!["NbrCommunities"]);
    }
}