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

    // ── One-mode: RemoveNodeEdges ──────────────────────────────────────────────

    [Fact]
    public void OneModeLayer_RemoveNodeEdges_Packed_EdgeNoLongerExists()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.AddEdge("friends", 1, 3);
        net.AddEdge("friends", 2, 4);
        net.Pack("friends");

        ILayer pak = net.Layers["friends"];
        pak.RemoveNodeEdges(1);

        Assert.False(pak.CheckEdgeExists(1, 2));
        Assert.False(pak.CheckEdgeExists(1, 3));
        Assert.True(pak.CheckEdgeExists(2, 4)); // edge not involving node 1 is unaffected
    }

    [Fact]
    public void OneModeLayer_PackedAndDynamic_AfterRemoveNodeEdges_CheckEdge_Identical()
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
        dyn.RemoveNodeEdges(2);
        pak.RemoveNodeEdges(2);

        uint[] nodes = [1, 2, 3, 4, 5];
        foreach (var n1 in nodes)
            foreach (var n2 in nodes)
                Assert.Equal(dyn.CheckEdgeExists(n1, n2), pak.CheckEdgeExists(n1, n2));
    }

    [Fact]
    public void TwoModeLayer_RemoveNodeEdges_Packed_NodeGoneFromHyperedge()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "club1", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "club2", new uint[] { 2, 4 });
        net.Pack("clubs");

        ILayer pak = net.Layers["clubs"];
        pak.RemoveNodeEdges(2);

        ILayerTwoMode pak2 = (ILayerTwoMode)pak;
        Assert.Empty(pak2.GetNodeHyperedgeNames(2));
        Assert.DoesNotContain(2u, pak2.GetHyperedgeNodeIds("club1"));
        Assert.Contains(1u, pak2.GetHyperedgeNodeIds("club1"));
        Assert.Contains(3u, pak2.GetHyperedgeNodeIds("club1"));
        Assert.DoesNotContain(2u, pak2.GetHyperedgeNodeIds("club2"));
        Assert.Contains(4u, pak2.GetHyperedgeNodeIds("club2"));
    }

    // ── One-mode: ClearLayer ───────────────────────────────────────────────────

    [Fact]
    public void OneModeLayer_ClearLayer_Packed_NoEdgesRemain()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.AddEdge("friends", 2, 3);
        net.AddEdge("friends", 4, 5);
        net.Pack("friends");

        ILayer pak = net.Layers["friends"];
        pak.ClearLayer();

        uint[] nodes = [1, 2, 3, 4, 5];
        foreach (var n1 in nodes)
            foreach (var n2 in nodes)
                Assert.False(pak.CheckEdgeExists(n1, n2));
        Assert.Empty(pak.GetNodeAlters(1, EdgeTraversal.Both));
    }

    [Fact]
    public void OneModeLayer_PackedAndDynamic_AfterClearLayer_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("dynamic", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("packed", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        foreach (var (a, b) in new[] { (1u, 2u), (2u, 3u), (3u, 4u), (4u, 5u) })
        {
            net.AddEdge("dynamic", a, b);
            net.AddEdge("packed", a, b);
        }
        net.Pack("packed");

        ILayer dyn = net.Layers["dynamic"];
        ILayer pak = net.Layers["packed"];
        dyn.ClearLayer();
        pak.ClearLayer();

        uint[] nodes = [1, 2, 3, 4, 5];
        foreach (var n1 in nodes)
            foreach (var n2 in nodes)
                Assert.Equal(dyn.CheckEdgeExists(n1, n2), pak.CheckEdgeExists(n1, n2));
        foreach (uint nodeId in nodes)
            Assert.Equal(
                dyn.GetNodeAlters(nodeId, EdgeTraversal.Both).Order().ToArray(),
                pak.GetNodeAlters(nodeId, EdgeTraversal.Both).Order().ToArray());
    }

    [Fact]
    public void TwoModeLayer_ClearLayer_Packed_NoAffiliationsRemain()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "club1", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "club2", new uint[] { 2, 4 });
        net.Pack("clubs");

        ILayer pak = net.Layers["clubs"];
        pak.ClearLayer();

        ILayerTwoMode pak2 = (ILayerTwoMode)pak;
        Assert.Empty(pak2.GetNodeHyperedgeNames(1));
        Assert.Empty(pak2.GetNodeHyperedgeNames(2));
        Assert.Empty(pak2.GetHyperedgeNodeIds("club1"));
        Assert.Empty(pak2.GetHyperedgeNodeIds("club2"));
    }

    // ── One-mode: CreateFilteredCopy ───────────────────────────────────────────

    [Fact]
    public void OneModeLayer_Dynamic_CreateFilteredCopy_OnlyKeepsEdgesInNodeset()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.AddEdge("friends", 2, 3);
        net.AddEdge("friends", 3, 4);
        net.AddEdge("friends", 4, 5);

        var subNs = new Nodeset("sub");
        foreach (uint i in new uint[] { 1, 2, 3 }) subNs.AddNode(i);

        ILayer filtered = net.Layers["friends"].CreateFilteredCopy(subNs);

        Assert.True(filtered.CheckEdgeExists(1, 2));
        Assert.True(filtered.CheckEdgeExists(2, 3));
        Assert.False(filtered.CheckEdgeExists(3, 4)); // node 4 not in sub-nodeset
        Assert.False(filtered.CheckEdgeExists(4, 5)); // nodes 4 and 5 not in sub-nodeset
        Assert.EndsWith("_filtered", filtered.Name);
    }

    [Fact]
    public void OneModeLayer_Packed_CreateFilteredCopy_OnlyKeepsEdgesInNodeset()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.AddEdge("friends", 2, 3);
        net.AddEdge("friends", 3, 4);
        net.AddEdge("friends", 4, 5);
        net.Pack("friends");

        var subNs = new Nodeset("sub");
        foreach (uint i in new uint[] { 1, 2, 3 }) subNs.AddNode(i);

        ILayer filtered = net.Layers["friends"].CreateFilteredCopy(subNs);

        Assert.True(filtered.CheckEdgeExists(1, 2));
        Assert.True(filtered.CheckEdgeExists(2, 3));
        Assert.False(filtered.CheckEdgeExists(3, 4)); // node 4 not in sub-nodeset
        Assert.False(filtered.CheckEdgeExists(4, 5)); // nodes 4 and 5 not in sub-nodeset
        Assert.EndsWith("_filtered", filtered.Name);
    }

    [Fact]
    public void OneModeLayer_PackedAndDynamic_CreateFilteredCopy_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("dynamic", EdgeDirectionality.Directed, EdgeType.Valued, false);
        net.AddLayerOneMode("packed", EdgeDirectionality.Directed, EdgeType.Valued, false);
        foreach (var (a, b, v) in new[] { (1u, 2u, 1.5f), (2u, 1u, 3.0f), (1u, 3u, 0.75f), (3u, 4u, 2.0f) })
        {
            net.AddEdge("dynamic", a, b, value: v);
            net.AddEdge("packed", a, b, value: v);
        }
        net.Pack("packed");

        var subNs = new Nodeset("sub");
        foreach (uint i in new uint[] { 1, 2, 3 }) subNs.AddNode(i);

        ILayer dynFiltered = net.Layers["dynamic"].CreateFilteredCopy(subNs);
        ILayer pakFiltered = net.Layers["packed"].CreateFilteredCopy(subNs);

        uint[] nodes = [1, 2, 3, 4, 5];
        foreach (var n1 in nodes)
            foreach (var n2 in nodes)
            {
                Assert.Equal(dynFiltered.CheckEdgeExists(n1, n2), pakFiltered.CheckEdgeExists(n1, n2));
                Assert.Equal(dynFiltered.GetEdgeValue(n1, n2), pakFiltered.GetEdgeValue(n1, n2));
            }
    }

    [Fact]
    public void TwoModeLayer_PackedAndDynamic_CreateFilteredCopy_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("dynamic");
        net.AddLayerTwoMode("packed");
        foreach (var layerName in new[] { "dynamic", "packed" })
        {
            net.AddHyperedge(layerName, "club1", new uint[] { 1, 2, 3 });
            net.AddHyperedge(layerName, "club2", new uint[] { 2, 4, 5 });
        }
        net.Pack("packed");

        var subNs = new Nodeset("sub");
        foreach (uint i in new uint[] { 1, 2, 3 }) subNs.AddNode(i);

        ILayerTwoMode dynFiltered = (ILayerTwoMode)net.Layers["dynamic"].CreateFilteredCopy(subNs);
        ILayerTwoMode pakFiltered = (ILayerTwoMode)net.Layers["packed"].CreateFilteredCopy(subNs);

        // club1 is fully within {1,2,3}: identical between dynamic and packed filtered copies
        Assert.Equal(
            dynFiltered.GetHyperedgeNodeIds("club1").Order().ToArray(),
            pakFiltered.GetHyperedgeNodeIds("club1").Order().ToArray());

        // club2: only node 2 survives the filter (nodes 4 and 5 excluded)
        Assert.Equal(new uint[] { 2 }, dynFiltered.GetHyperedgeNodeIds("club2").Order().ToArray());
        Assert.Equal(new uint[] { 2 }, pakFiltered.GetHyperedgeNodeIds("club2").Order().ToArray());

        // node 4 was filtered out: no affiliations in either copy
        Assert.Empty(dynFiltered.GetNodeHyperedgeNames(4));
        Assert.Empty(pakFiltered.GetNodeHyperedgeNames(4));
    }

    // ── One-mode: directed valued parity ──────────────────────────────────────

    [Fact]
    public void OneModeLayer_Directed_Valued_PackedAndDynamic_GetEdgeValue_Asymmetric_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("dynamic", EdgeDirectionality.Directed, EdgeType.Valued, false);
        net.AddLayerOneMode("packed", EdgeDirectionality.Directed, EdgeType.Valued, false);
        // Deliberately asymmetric: (1→2)=1.5 but (2→1)=3.0
        foreach (var (a, b, v) in new[] { (1u, 2u, 1.5f), (2u, 1u, 3.0f), (1u, 3u, 0.75f) })
        {
            net.AddEdge("dynamic", a, b, value: v);
            net.AddEdge("packed", a, b, value: v);
        }
        net.Pack("packed");

        ILayer dyn = net.Layers["dynamic"];
        ILayer pak = net.Layers["packed"];

        // Directed edge values must be asymmetric
        Assert.NotEqual(dyn.GetEdgeValue(1, 2), dyn.GetEdgeValue(2, 1));
        Assert.NotEqual(pak.GetEdgeValue(1, 2), pak.GetEdgeValue(2, 1));

        // Dynamic and packed must agree on every ordered pair
        uint[] nodes = [1, 2, 3, 4, 5];
        foreach (var n1 in nodes)
            foreach (var n2 in nodes)
                Assert.Equal(dyn.GetEdgeValue(n1, n2), pak.GetEdgeValue(n1, n2));
    }

    [Fact]
    public void OneModeLayer_Directed_Valued_PackedAndDynamic_GetNodeAlters_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("dynamic", EdgeDirectionality.Directed, EdgeType.Valued, false);
        net.AddLayerOneMode("packed", EdgeDirectionality.Directed, EdgeType.Valued, false);
        foreach (var (a, b, v) in new[] { (1u, 2u, 1.5f), (2u, 1u, 3.0f), (1u, 3u, 0.75f) })
        {
            net.AddEdge("dynamic", a, b, value: v);
            net.AddEdge("packed", a, b, value: v);
        }
        net.Pack("packed");

        ILayer dyn = net.Layers["dynamic"];
        ILayer pak = net.Layers["packed"];

        foreach (var traversal in new[] { EdgeTraversal.Out, EdgeTraversal.In, EdgeTraversal.Both })
            foreach (uint nodeId in new uint[] { 1, 2, 3, 4, 5 })
            {
                var dynAlters = dyn.GetNodeAlters(nodeId, traversal).Order().ToArray();
                var pakAlters = pak.GetNodeAlters(nodeId, traversal).Order().ToArray();
                Assert.Equal(dynAlters, pakAlters);
            }
    }

    // ── Self-ties and two-mode hyperedge queries ───────────────────────────────

    [Fact]
    public void OneModeLayer_Selftie_Enabled_Packed_CheckEdgeExists_True()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, true); // selfties=true
        net.AddEdge("friends", 1, 1); // self-tie
        net.AddEdge("friends", 1, 2);
        net.Pack("friends");

        ILayer pak = net.Layers["friends"];

        Assert.True(pak.CheckEdgeExists(1, 1));
        Assert.Equal(1f, pak.GetEdgeValue(1, 1));
        Assert.True(pak.CheckEdgeExists(1, 2));
    }

    [Fact]
    public void TwoModeLayer_PackedAndDynamic_GetHyperedgeNodeIds_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("dynamic");
        net.AddLayerTwoMode("packed");
        foreach (var layerName in new[] { "dynamic", "packed" })
        {
            net.AddHyperedge(layerName, "club1", new uint[] { 1, 2, 3 });
            net.AddHyperedge(layerName, "club2", new uint[] { 2, 4 });
            net.AddHyperedge(layerName, "club3", new uint[] { 3, 4, 5 });
        }
        net.Pack("packed");

        ILayerTwoMode dyn2 = (ILayerTwoMode)net.Layers["dynamic"];
        ILayerTwoMode pak2 = (ILayerTwoMode)net.Layers["packed"];

        foreach (var clubName in new[] { "club1", "club2", "club3" })
        {
            var dynNodes = dyn2.GetHyperedgeNodeIds(clubName).Order().ToArray();
            var pakNodes = pak2.GetHyperedgeNodeIds(clubName).Order().ToArray();
            Assert.Equal(dynNodes, pakNodes);
        }
    }

    [Fact]
    public void TwoModeLayer_PackedAndDynamic_GetNodeHyperedgeNames_Identical()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("dynamic");
        net.AddLayerTwoMode("packed");
        foreach (var layerName in new[] { "dynamic", "packed" })
        {
            net.AddHyperedge(layerName, "club1", new uint[] { 1, 2, 3 });
            net.AddHyperedge(layerName, "club2", new uint[] { 2, 4 });
            net.AddHyperedge(layerName, "club3", new uint[] { 3, 4, 5 });
        }
        net.Pack("packed");

        ILayerTwoMode dyn2 = (ILayerTwoMode)net.Layers["dynamic"];
        ILayerTwoMode pak2 = (ILayerTwoMode)net.Layers["packed"];

        foreach (uint nodeId in new uint[] { 1, 2, 3, 4, 5 })
        {
            var dynHyperedges = dyn2.GetNodeHyperedgeNames(nodeId).Order().ToArray();
            var pakHyperedges = pak2.GetNodeHyperedgeNames(nodeId).Order().ToArray();
            Assert.Equal(dynHyperedges, pakHyperedges);
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
