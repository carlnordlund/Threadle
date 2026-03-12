using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Tests;

/// <summary>
/// Tests for layer packing/unpacking and ILayer interface consistency.
///
/// Covers three areas:
///   1. Pack/unpack mechanics: IsStatic flag, write operations blocked on packed layers.
///   2. Query parity: all ILayer read methods return identical results whether the
///      underlying layer is dynamic (LayerOneMode / LayerTwoMode) or packed
///      (LayerOneModeStatic / LayerTwoModeStatic).
///   3. The "paired layer" pattern: two identical layers are added to the same
///      network, one is packed, and every query result is compared side-by-side.
/// </summary>
public class LayerTests
{
    private static Nodeset MakeNodeset()
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 5; i++) ns.AddNode(i);
        return ns;
    }

    // ── One-mode: packing mechanics ────────────────────────────────────────────

    [Fact]
    public void Pack_OneModeLayer_IsStaticTrue()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);

        net.Pack("friends");

        Assert.True(net.Layers["friends"].IsStatic);
    }

    [Fact]
    public void Pack_OneModeLayer_AddEdgeFails()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.Pack("friends");

        var result = net.AddEdge("friends", 1, 2);

        Assert.False(result.Success);
        Assert.Equal("LayerIsPacked", result.Code);
    }

    [Fact]
    public void Pack_OneModeLayer_RemoveEdgeFails()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.Pack("friends");

        var result = net.RemoveEdge("friends", 1, 2);

        Assert.False(result.Success);
        Assert.Equal("LayerIsPacked", result.Code);
    }

    [Fact]
    public void Unpack_OneModeLayer_IsStaticFalse_CanAddEdge()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.Pack("friends");
        net.Unpack("friends");

        Assert.False(net.Layers["friends"].IsStatic);
        Assert.True(net.AddEdge("friends", 1, 2).Success);
    }

    // ── One-mode: packed vs dynamic query parity ───────────────────────────────

    [Fact]
    public void OneModeLayer_PackedAndDynamic_CheckEdgeExists_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("dynamic", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("packed", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        foreach (var (a, b) in new[] { (1u, 2u), (2u, 3u), (4u, 5u) })
        {
            net.AddEdge("dynamic", a, b);
            net.AddEdge("packed", a, b);
        }
        net.Pack("packed");

        ILayer dyn = net.Layers["dynamic"];
        ILayer pak = net.Layers["packed"];

        uint[] nodes = [1, 2, 3, 4, 5];
        foreach (var n1 in nodes)
            foreach (var n2 in nodes)
                Assert.Equal(dyn.CheckEdgeExists(n1, n2), pak.CheckEdgeExists(n1, n2));
    }

    [Fact]
    public void OneModeLayer_PackedAndDynamic_GetEdgeValue_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("dynamic", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddLayerOneMode("packed", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        foreach (var (a, b, v) in new[] { (1u, 2u, 3.5f), (2u, 3u, 1.0f), (4u, 5u, 0.25f) })
        {
            net.AddEdge("dynamic", a, b, value: v);
            net.AddEdge("packed", a, b, value: v);
        }
        net.Pack("packed");

        ILayer dyn = net.Layers["dynamic"];
        ILayer pak = net.Layers["packed"];

        uint[] nodes = [1, 2, 3, 4, 5];
        foreach (var n1 in nodes)
            foreach (var n2 in nodes)
                Assert.Equal(dyn.GetEdgeValue(n1, n2), pak.GetEdgeValue(n1, n2));
    }

    [Fact]
    public void OneModeLayer_PackedAndDynamic_GetNodeAlters_Undirected_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("dynamic", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("packed", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        foreach (var (a, b) in new[] { (1u, 2u), (1u, 3u), (2u, 4u), (3u, 5u) })
        {
            net.AddEdge("dynamic", a, b);
            net.AddEdge("packed", a, b);
        }
        net.Pack("packed");

        ILayer dyn = net.Layers["dynamic"];
        ILayer pak = net.Layers["packed"];

        foreach (uint nodeId in new uint[] { 1, 2, 3, 4, 5 })
        {
            var dynAlters = dyn.GetNodeAlters(nodeId, EdgeTraversal.Both).Order().ToArray();
            var pakAlters = pak.GetNodeAlters(nodeId, EdgeTraversal.Both).Order().ToArray();
            Assert.Equal(dynAlters, pakAlters);
        }
    }

    [Fact]
    public void OneModeLayer_PackedAndDynamic_GetNodeAlters_Directed_InOutBoth_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("dynamic", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddLayerOneMode("packed", EdgeDirectionality.Directed, EdgeType.Binary, false);
        // Node 1 sends to 2 and 4; node 3 sends to 1
        foreach (var (a, b) in new[] { (1u, 2u), (1u, 4u), (3u, 1u) })
        {
            net.AddEdge("dynamic", a, b);
            net.AddEdge("packed", a, b);
        }
        net.Pack("packed");

        ILayer dyn = net.Layers["dynamic"];
        ILayer pak = net.Layers["packed"];

        foreach (var traversal in new[] { EdgeTraversal.Out, EdgeTraversal.In, EdgeTraversal.Both })
        {
            foreach (uint nodeId in new uint[] { 1, 2, 3, 4, 5 })
            {
                var dynAlters = dyn.GetNodeAlters(nodeId, traversal).Order().ToArray();
                var pakAlters = pak.GetNodeAlters(nodeId, traversal).Order().ToArray();
                Assert.Equal(dynAlters, pakAlters);
            }
        }
    }

    // ── Two-mode: packing mechanics ────────────────────────────────────────────

    [Fact]
    public void Pack_TwoModeLayer_IsStaticTrue()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2 });

        net.Pack("clubs");

        Assert.True(net.Layers["clubs"].IsStatic);
    }

    [Fact]
    public void Pack_TwoModeLayer_AddHyperedgeFails()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("clubs");
        net.Pack("clubs");

        var result = net.AddHyperedge("clubs", "new");

        Assert.False(result.Success);
        Assert.Equal("LayerIsStatic", result.Code);
    }

    [Fact]
    public void Pack_TwoModeLayer_AddAffiliationFails()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2 });
        net.Pack("clubs");

        var result = net.AddAffiliation("clubs", "c1", 3, addMissingNode: false, addMissingHyperedge: false);

        Assert.False(result.Success);
        Assert.Equal("LayerIsStatic", result.Code);
    }

    [Fact]
    public void Unpack_TwoModeLayer_IsStaticFalse_CanAddHyperedge()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("clubs");
        net.Pack("clubs");
        net.Unpack("clubs");

        Assert.False(net.Layers["clubs"].IsStatic);
        Assert.True(net.AddHyperedge("clubs", "newclub").Success);
    }

    // ── Two-mode: packed vs dynamic query parity ───────────────────────────────

    [Fact]
    public void TwoModeLayer_PackedAndDynamic_CheckEdgeExists_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("dynamic");
        net.AddLayerTwoMode("packed");
        // nodes 1,2,3 share club1; nodes 2,4 share club2 → 2 bridges both groups
        foreach (var layerName in new[] { "dynamic", "packed" })
        {
            net.AddHyperedge(layerName, "club1", new uint[] { 1, 2, 3 });
            net.AddHyperedge(layerName, "club2", new uint[] { 2, 4 });
        }
        net.Pack("packed");

        ILayer dyn = net.Layers["dynamic"];
        ILayer pak = net.Layers["packed"];

        uint[] nodes = [1, 2, 3, 4, 5];
        foreach (var n1 in nodes)
            foreach (var n2 in nodes)
                Assert.Equal(dyn.CheckEdgeExists(n1, n2), pak.CheckEdgeExists(n1, n2));
    }

    [Fact]
    public void TwoModeLayer_PackedAndDynamic_GetEdgeValue_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("dynamic");
        net.AddLayerTwoMode("packed");
        // nodes 2 and 3 share both clubs (value=2); nodes 1 and 4 share none (value=0)
        foreach (var layerName in new[] { "dynamic", "packed" })
        {
            net.AddHyperedge(layerName, "club1", new uint[] { 1, 2, 3 });
            net.AddHyperedge(layerName, "club2", new uint[] { 2, 3, 4 });
        }
        net.Pack("packed");

        ILayer dyn = net.Layers["dynamic"];
        ILayer pak = net.Layers["packed"];

        uint[] nodes = [1, 2, 3, 4, 5];
        foreach (var n1 in nodes)
            foreach (var n2 in nodes)
                Assert.Equal(dyn.GetEdgeValue(n1, n2), pak.GetEdgeValue(n1, n2));
    }

    [Fact]
    public void TwoModeLayer_PackedAndDynamic_GetNodeAlters_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("dynamic");
        net.AddLayerTwoMode("packed");
        foreach (var layerName in new[] { "dynamic", "packed" })
        {
            net.AddHyperedge(layerName, "club1", new uint[] { 1, 2, 3 });
            net.AddHyperedge(layerName, "club2", new uint[] { 2, 4 });
        }
        net.Pack("packed");

        ILayer dyn = net.Layers["dynamic"];
        ILayer pak = net.Layers["packed"];

        foreach (uint nodeId in new uint[] { 1, 2, 3, 4, 5 })
        {
            var dynAlters = dyn.GetNodeAlters(nodeId, EdgeTraversal.Both).Order().ToArray();
            var pakAlters = pak.GetNodeAlters(nodeId, EdgeTraversal.Both).Order().ToArray();
            Assert.Equal(dynAlters, pakAlters);
        }
    }

    // ── Pack all layers at once ────────────────────────────────────────────────

    [Fact]
    public void Pack_AllLayers_AllAreStatic()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("follows", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddLayerTwoMode("clubs");
        net.AddEdge("friends", 1, 2);
        net.AddEdge("follows", 1, 3);
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2 });

        net.Pack(null);  // pack all

        Assert.True(net.Layers["friends"].IsStatic);
        Assert.True(net.Layers["follows"].IsStatic);
        Assert.True(net.Layers["clubs"].IsStatic);
    }

    [Fact]
    public void Pack_AllLayers_QueriesStillWork()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerTwoMode("clubs");
        net.AddEdge("friends", 1, 2);
        net.AddHyperedge("clubs", "c1", new uint[] { 3, 4, 5 });

        net.Pack(null);

        Assert.True(net.CheckEdgeExists("friends", 1, 2).Value);
        Assert.False(net.CheckEdgeExists("friends", 1, 3).Value);

        var hyperedges = net.GetNodeHyperedges("clubs", 3);
        Assert.True(hyperedges.Success);
        Assert.Contains("c1", hyperedges.Value);

        var clubNodes = net.GetHyperedgeNodes("clubs", "c1");
        Assert.Contains(4u, clubNodes.Value);
    }
}
