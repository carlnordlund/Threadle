using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// The traversal kernels translate node ids to compact indices with a direct-address array offset by the
/// smallest id when the ids span a compact range, and with a dictionary otherwise. The same graph with small
/// ids, with a large contiguous block of ids (e.g. person ids starting at 1,640,000,000: the offset array) and
/// with scattered ids (the dictionary) must give the same results everywhere.
/// </summary>
[Collection("MaxThreadsLimit")] // sets maxthreads
public class NodeIdLookupTests : IDisposable
{
    public void Dispose() => UserSettings.MaxDegreeOfParallelism = -1;

    private static Network Make(Func<uint, uint> id, bool packed)
    {
        var ns = new Nodeset("ns");
        for (uint i = 0; i < 400; i++) ns.AddNode(id(i));
        var net = new Network("net", ns);
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        var rng = new Random(9);
        for (int e = 0; e < 1500; e++)
        {
            uint a = (uint)rng.Next(400), b = rng.Next(3) == 0 ? (uint)rng.Next(400) : (a / 40) * 40 + (uint)rng.Next(40);
            if (a != b) net.AddEdge("l", id(a), id(b));
        }
        if (packed) Assert.True(net.Pack("l").Success);
        return net;
    }

    private static double[] Attr(Network net, string attr) => net.Nodeset.NodeIdArray.OrderBy(x => x).Select(id =>
    {
        var a = net.Nodeset.GetNodeAttribute(id, attr);
        return a.Success ? Convert.ToDouble(a.Value.Value.GetValue(a.Value.Type)) : double.NaN;
    }).ToArray();

    private static (double[] Comp, double[] Bw, double[] Pr, double[] Comm, int Diam, int[] Dists) Run(Network net)
    {
        Assert.True(Analyses.ConnectedComponents(net, "l", "cc").Success);
        Assert.True(Analyses.BetweennessCentrality(net, ["l"], "bw").Success);
        Assert.True(Analyses.PageRank(net, ["l"], "pr").Success);
        Misc.SetRandomSeed(3);
        Assert.True(Analyses.CommunityDetectionLouvain(net, ["l"], "comm").Success);
        int diam = (int)Analyses.Diameter(net, "l", diameterOnly: true).Value!["Diameter"];
        var ids = net.Nodeset.NodeIdArray.OrderBy(x => x).ToArray();
        var pairs = Enumerable.Range(0, 300).Select(k => (ids[(k * 37) % ids.Length], ids[(k * 101 + 7) % ids.Length])).ToList();
        var sp = Analyses.ShortestPaths(net, ["l"], pairs).Value!.Select(x => x.Result.Distance).ToArray();
        return (Attr(net, "cc"), Attr(net, "bw"), Attr(net, "pr"), Attr(net, "comm"), diam, sp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SmallLargeAndScatteredIds_GiveSameResults(bool packed)
    {
        UserSettings.MaxDegreeOfParallelism = 1;
        var small = Run(Make(i => i, packed));
        var block = Run(Make(i => 1_640_000_000u + i, packed));
        var scattered = Run(Make(i => i * 9_999_991u + 13u, packed)); // spans ~4e9: dictionary path
        foreach (var other in new[] { block, scattered })
        {
            Assert.Equal(small.Comp, other.Comp);
            Assert.Equal(small.Bw, other.Bw);
            Assert.Equal(small.Pr, other.Pr);
            Assert.Equal(small.Diam, other.Diam);
            Assert.Equal(small.Dists, other.Dists);
            Assert.Equal(small.Comm.Distinct().Count(), other.Comm.Distinct().Count());
        }
    }
}
