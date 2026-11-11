using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;

namespace Threadle.Tests;

/// <summary>
/// subnet() on 2-mode layers: hyperedges with no members among the subset's nodes are dropped by default
/// (keepemptyhyperedges=true keeps them); hyperedges with one or more remaining members, including
/// single-member ones, are always kept; layers keep their source name. duplicate() keeps empty
/// hyperedges (a copy is identical).
/// </summary>
public class SubnetHyperedgeTests
{
    private static Network Make(bool packed)
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 10; i++) ns.AddNode(i);
        var net = new Network("net", ns);
        net.AddLayerTwoMode("h");
        net.AddHyperedge("h", "a", [1, 2, 3]);   // keeps 1, 2
        net.AddHyperedge("h", "b", [3, 4]);      // keeps nothing
        net.AddHyperedge("h", "c", [2, 9]);      // keeps 2 (single member)
        net.AddHyperedge("h", "d", [5, 6]);      // keeps nothing
        net.AddHyperedge("h", "e", []);          // empty already
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("l", 1, 2);
        net.AddEdge("l", 2, 4);
        if (packed) Assert.True(net.Pack(null).Success);
        return net;
    }

    private static Nodeset Subset(Network net)
    {
        var sub = new Nodeset("sub");
        foreach (uint id in new uint[] { 1, 2 }) sub.AddNode(id);
        return sub;
    }

    private static ILayerTwoMode Layer(Network net) => (ILayerTwoMode)net.Layers["h"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Subnet_DropsHyperedgesWithoutMembers_KeepsSingleMember(bool packed)
    {
        var net = Make(packed);
        var r = NetworkProcessor.Subnet(net, Subset(net));
        Assert.True(r.Success, r.Message);
        var layer = Layer(r.Value!);
        Assert.Equal(2u, (uint)layer.NbrHyperedges);
        var names = layer.GetAllHyperedgeData().Select(x => x.Item1).OrderBy(x => x).ToArray();
        Assert.Equal(["a", "c"], names);
        Assert.Equal(3u, (uint)layer.NbrAffiliations);
        Assert.True(r.Value!.CheckEdgeExists("h", 1, 2).Value);
        Assert.Equal(2, layer.GetNodeAlters(2, EdgeTraversal.Both).Length + 1); // 2's co-member: 1
        Assert.Equal("h", r.Value!.Layers["h"].Name);
        Assert.Equal("l", r.Value!.Layers["l"].Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Subnet_KeepEmptyHyperedges_KeepsAll(bool packed)
    {
        var net = Make(packed);
        var r = NetworkProcessor.Subnet(net, Subset(net), keepEmptyHyperedges: true);
        Assert.True(r.Success, r.Message);
        Assert.Equal(5u, (uint)Layer(r.Value!).NbrHyperedges);
        Assert.Equal(3u, (uint)Layer(r.Value!).NbrAffiliations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Subnet_PackedNodeQueries_UseRenumberedHyperedges(bool packed)
    {
        // After dropping hyperedges, each node's hyperedge list must point at the right (renumbered) ones
        var net = Make(packed);
        var sub = NetworkProcessor.Subnet(net, Subset(net)).Value!;
        var layer = Layer(sub);
        var of2 = layer.GetAllHyperedgeData().Where(x => x.Item2.Contains(2u)).Select(x => x.Item1).OrderBy(x => x).ToArray();
        Assert.Equal(["a", "c"], of2);
        Assert.Equal([2u], layer.GetNodeAlters(1, EdgeTraversal.Both));
        Assert.Equal([1u], layer.GetNodeAlters(2, EdgeTraversal.Both));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateLayer_KeepsEmptyHyperedges(bool packed)
    {
        var net = Make(packed);
        Assert.True(net.DuplicateLayer("h", "h2").Success);
        Assert.Equal(5u, (uint)((ILayerTwoMode)net.Layers["h2"]).NbrHyperedges);
    }
}