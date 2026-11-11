using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// 1-mode community detection builds its working graph in one of several ways: a single binary layer without
/// self-ties uses shared unit weights (directly from the packed arrays in parallel when the layer is sorted, or
/// by the general two-pass build otherwise), and falls back to real weights when that layer still yields a
/// repeated neighbour (a directed layer with ties in both directions). All must give exactly what the general
/// weighted build gives; here that build is forced by adding an empty second layer.
/// </summary>
[Collection("MaxThreadsLimit")] // sets maxthreads
public class CommunityDetectionWorkingGraphTests : IDisposable
{
    public void Dispose() => UserSettings.MaxDegreeOfParallelism = -1;

    private static (double Q, int[] C) Run(string method, Network net, string[] layers, string attr)
    {
        Misc.SetRandomSeed(11);
        var r = method switch
        {
            "louvain" => Analyses.CommunityDetectionLouvain(net, layers, attr),
            "leiden" => Analyses.CommunityDetectionLeiden(net, layers, attr),
            "lp" => Analyses.CommunityDetectionLabelPropagation(net, layers, attr),
            "lpam" => Analyses.CommunityDetectionLPAm(net, layers, attr),
            _ => Analyses.CommunityDetectionPLM(net, layers, attr),
        };
        Assert.True(r.Success, r.Message);
        var c = net.Nodeset.NodeIdArray.Select(id =>
        {
            var a = net.Nodeset.GetNodeAttribute(id, attr);
            return (int)a.Value.Value.GetValue(a.Value.Type)!;
        }).ToArray();
        return ((double)r.Value!["Modularity"], c);
    }

    public static IEnumerable<object[]> Cases() =>
        from m in new[] { "louvain", "leiden", "lp", "lpam", "plm" }
        from kind in new[] { "packed", "unpacked", "directed" }
        select new object[] { m, kind };

    [Theory]
    [MemberData(nameof(Cases))]
    public void SingleBinaryLayer_SameAsWeightedBuild(string method, string kind)
    {
        UserSettings.MaxDegreeOfParallelism = 1; // plm is only reproducible single-threaded
        Misc.SetRandomSeed(4);
        var ns = new Nodeset("ns");
        for (uint i = 0; i < 600; i++) ns.AddNode(i * 2 + 1);
        var net = new Network("net", ns);
        bool directed = kind == "directed";
        net.AddLayerOneMode("l", directed ? EdgeDirectionality.Directed : EdgeDirectionality.Undirected, EdgeType.Binary, false);
        if (!directed)
            Assert.True(Generators.GenerateBarabasiAlbertLayer(net, "l", 4).Success);
        else
        {
            // clustered random arcs (blocks of 60 nodes), plus some reciprocated ones below
            var rng = new Random(4);
            var all = ns.NodeIdArray;
            for (int e = 0; e < 3000; e++)
            {
                int a = rng.Next(all.Length), b = rng.Next(4) == 0 ? rng.Next(all.Length) : (a / 60) * 60 + rng.Next(60);
                if (a != b && b < all.Length) net.AddEdge("l", all[a], all[b]);
            }
        }
        if (directed)
        {
            // some ties in both directions: such pairs weigh 2 in the symmetrized working graph
            var ids = ns.NodeIdArray;
            for (int i = 0; i < ids.Length; i += 3)
                foreach (uint v in ((ILayerOneMode)net.Layers["l"]).GetNodeAlters(ids[i], EdgeTraversal.Out).Take(2).ToArray())
                    net.AddEdge("l", v, ids[i]);
        }
        if (kind != "unpacked") Assert.True(net.Pack("l").Success);
        net.AddLayerOneMode("empty", EdgeDirectionality.Undirected, EdgeType.Binary, false);

        var single = Run(method, net, ["l"], "a");
        var weighted = Run(method, net, ["l", "empty"], "b");
        Assert.Equal(weighted.Q, single.Q);
        Assert.Equal(weighted.C, single.C);
    }
}
