using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Tests;

/// <summary>
/// Tests for the Network class: layers, edges, and node-alter queries.
/// </summary>
public class NetworkTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>Creates a small network with nodes 1, 2, 3 and no layers.</summary>
    private static Network MakeNetwork()
    {
        var nodeset = new Nodeset("ns", 0);
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.AddNode(3);
        return new Network("net", nodeset);
    }

    /// <summary>Creates a network with an undirected binary layer already added.</summary>
    private static (Network net, string layerName) MakeNetworkWithUndirectedLayer()
    {
        var net = MakeNetwork();
        const string layer = "friends";
        net.AddLayerOneMode(layer, EdgeDirectionality.Undirected, EdgeType.Binary, false);
        return (net, layer);
    }

    /// <summary>Creates a network with a directed binary layer already added.</summary>
    private static (Network net, string layerName) MakeNetworkWithDirectedLayer()
    {
        var net = MakeNetwork();
        const string layer = "follows";
        net.AddLayerOneMode(layer, EdgeDirectionality.Directed, EdgeType.Binary, false);
        return (net, layer);
    }

    // ── AddLayerOneMode ──────────────────────────────────────────────────────────

    [Fact]
    public void AddLayerOneMode_NewName_Succeeds()
    {
        var net = MakeNetwork();
        var result = net.AddLayerOneMode("layer1", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        Assert.True(result.Success);
    }

    [Fact]
    public void AddLayerOneMode_NewName_AppearsInLayers()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("layer1", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        Assert.True(net.Layers.ContainsKey("layer1"));
    }

    [Fact]
    public void AddLayerOneMode_DuplicateName_Fails()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("layer1", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        var result = net.AddLayerOneMode("layer1", EdgeDirectionality.Directed, EdgeType.Binary, false);
        Assert.False(result.Success);
    }

    [Fact]
    public void AddLayerOneMode_EmptyName_Fails()
    {
        var net = MakeNetwork();
        var result = net.AddLayerOneMode("   ", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        Assert.False(result.Success);
    }

    // ── AddLayerTwoMode ──────────────────────────────────────────────────────────

    [Fact]
    public void AddLayerTwoMode_NewName_Succeeds()
    {
        var net = MakeNetwork();
        var result = net.AddLayerTwoMode("clubs");
        Assert.True(result.Success);
    }

    [Fact]
    public void AddLayerTwoMode_DuplicateName_Fails()
    {
        var net = MakeNetwork();
        net.AddLayerTwoMode("clubs");
        var result = net.AddLayerTwoMode("clubs");
        Assert.False(result.Success);
    }

    // ── RemoveLayer ──────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveLayer_ExistingLayer_Succeeds()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        var result = net.RemoveLayer(layer);
        Assert.True(result.Success);
    }

    [Fact]
    public void RemoveLayer_ExistingLayer_DisappearsFromLayers()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        net.RemoveLayer(layer);
        Assert.False(net.Layers.ContainsKey(layer));
    }

    [Fact]
    public void RemoveLayer_NonExistentLayer_Fails()
    {
        var net = MakeNetwork();
        var result = net.RemoveLayer("ghost");
        Assert.False(result.Success);
    }

    // ── ClearLayer ───────────────────────────────────────────────────────────────

    [Fact]
    public void ClearLayer_RemovesAllEdges()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        net.AddEdge(layer, 1, 2);
        net.AddEdge(layer, 2, 3);
        net.ClearLayer(layer);
        var edgesResult = net.GetAllEdges(layer);
        Assert.True(edgesResult.Success);
        Assert.Empty(edgesResult.Value!);
    }

    // ── GetNextAvailableLayerName ────────────────────────────────────────────────

    [Fact]
    public void GetNextAvailableLayerName_NoConflict_ReturnsBaseName()
    {
        var net = MakeNetwork();
        Assert.Equal("layer", net.GetNextAvailableLayerName("layer"));
    }

    [Fact]
    public void GetNextAvailableLayerName_Conflict_ReturnsSuffixed()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        Assert.Equal("layer-1", net.GetNextAvailableLayerName("layer"));
    }

    [Fact]
    public void GetNextAvailableLayerName_MultipleConflicts_IncrementsCorrectly()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("layer-1", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        Assert.Equal("layer-2", net.GetNextAvailableLayerName("layer"));
    }

    // ── AddEdge ──────────────────────────────────────────────────────────────────

    [Fact]
    public void AddEdge_ValidNodes_Succeeds()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        var result = net.AddEdge(layer, 1, 2);
        Assert.True(result.Success);
    }

    [Fact]
    public void AddEdge_MissingNode_Fails()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        var result = net.AddEdge(layer, 1, 999);  // node 999 doesn't exist
        Assert.False(result.Success);
    }

    [Fact]
    public void AddEdge_DuplicateEdge_Fails()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        net.AddEdge(layer, 1, 2);
        var result = net.AddEdge(layer, 1, 2);
        Assert.False(result.Success);
    }

    [Fact]
    public void AddEdge_Selftie_WhenNotAllowed_Fails()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, selfties: false);
        var result = net.AddEdge("layer", 1, 1);
        Assert.False(result.Success);
    }

    [Fact]
    public void AddEdge_Selftie_WhenAllowed_Succeeds()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, selfties: true);
        var result = net.AddEdge("layer", 1, 1);
        Assert.True(result.Success);
    }

    [Fact]
    public void AddEdge_WithAddMissingNodes_AddsNodeToNodeset()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("layer", 1, 99, addMissingNodes: true);
        Assert.Contains(99u, net.Nodeset.NodeIdArray);
    }

    [Fact]
    public void AddEdge_ToNonExistentLayer_Fails()
    {
        var net = MakeNetwork();
        var result = net.AddEdge("ghost", 1, 2);
        Assert.False(result.Success);
    }

    // ── RemoveEdge ───────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveEdge_ExistingEdge_Succeeds()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        net.AddEdge(layer, 1, 2);
        var result = net.RemoveEdge(layer, 1, 2);
        Assert.True(result.Success);
    }

    [Fact]
    public void RemoveEdge_ExistingEdge_EdgeNoLongerExists()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        net.AddEdge(layer, 1, 2);
        net.RemoveEdge(layer, 1, 2);
        var check = net.CheckEdgeExists(layer, 1, 2);
        Assert.True(check.Success);
        Assert.False(check.Value);
    }

    [Fact]
    public void RemoveEdge_NonExistentEdge_Fails()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        var result = net.RemoveEdge(layer, 1, 2);
        Assert.False(result.Success);
    }

    // ── CheckEdgeExists ──────────────────────────────────────────────────────────

    [Fact]
    public void CheckEdgeExists_AfterAdd_ReturnsTrue()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        net.AddEdge(layer, 1, 2);
        var result = net.CheckEdgeExists(layer, 1, 2);
        Assert.True(result.Success);
        Assert.True(result.Value);
    }

    [Fact]
    public void CheckEdgeExists_NoEdge_ReturnsFalse()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        var result = net.CheckEdgeExists(layer, 1, 2);
        Assert.True(result.Success);
        Assert.False(result.Value);
    }

    [Fact]
    public void CheckEdgeExists_UndirectedLayer_BothDirectionsReturn()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        net.AddEdge(layer, 1, 2);
        // For undirected, edge 2→1 should also be found
        var result = net.CheckEdgeExists(layer, 2, 1);
        Assert.True(result.Success);
        Assert.True(result.Value);
    }

    // ── GetEdge ──────────────────────────────────────────────────────────────────

    [Fact]
    public void GetEdge_ExistingBinaryEdge_ReturnsOne()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        net.AddEdge(layer, 1, 2);
        var result = net.GetEdge(layer, 1, 2);
        Assert.True(result.Success);
        Assert.Equal(1f, result.Value);
    }

    [Fact]
    public void GetEdge_NonExistentEdge_ReturnsZero()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        var result = net.GetEdge(layer, 1, 2);
        Assert.True(result.Success);
        Assert.Equal(0f, result.Value);
    }

    [Fact]
    public void GetEdge_ValuedLayer_ReturnsCorrectValue()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("valued", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("valued", 1, 2, value: 3.5f);
        var result = net.GetEdge("valued", 1, 2);
        Assert.True(result.Success);
        Assert.Equal(3.5f, result.Value, precision: 4);
    }

    // ── NbrEdges ─────────────────────────────────────────────────────────────────

    [Fact]
    public void NbrEdges_UndirectedLayer_CountsEdgesOnce()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        net.AddEdge(layer, 1, 2);
        net.AddEdge(layer, 2, 3);
        var layerObj = (LayerOneMode)net.Layers[layer];
        Assert.Equal(2u, layerObj.NbrEdges);
    }

    [Fact]
    public void NbrEdges_DirectedLayer_CountsEachDirectionSeparately()
    {
        var (net, layer) = MakeNetworkWithDirectedLayer();
        net.AddEdge(layer, 1, 2);
        net.AddEdge(layer, 2, 1);  // reverse direction: separate edge
        var layerObj = (LayerOneMode)net.Layers[layer];
        Assert.Equal(2u, layerObj.NbrEdges);
    }

    // ── GetNodeAlters ────────────────────────────────────────────────────────────

    [Fact]
    public void GetNodeAlters_UndirectedLayer_ReturnsAlter()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        net.AddEdge(layer, 1, 2);
        var result = net.GetNodeAlters(layer, 1);
        Assert.True(result.Success);
        Assert.Contains(2u, result.Value!);
    }

    [Fact]
    public void GetNodeAlters_UndirectedLayer_SymmetricInBothDirections()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        net.AddEdge(layer, 1, 2);
        var resultFrom1 = net.GetNodeAlters(layer, 1);
        var resultFrom2 = net.GetNodeAlters(layer, 2);
        Assert.Contains(2u, resultFrom1.Value!);
        Assert.Contains(1u, resultFrom2.Value!);
    }

    [Fact]
    public void GetNodeAlters_DirectedLayer_OnlyOutbound()
    {
        var (net, layer) = MakeNetworkWithDirectedLayer();
        net.AddEdge(layer, 1, 2);  // 1→2, not 2→1
        var result = net.GetNodeAlters(layer, 2, EdgeTraversal.Out);
        Assert.True(result.Success);
        Assert.DoesNotContain(1u, result.Value!);
    }

    [Fact]
    public void GetNodeAlters_NodeWithNoEdges_ReturnsEmptyArray()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        var result = net.GetNodeAlters(layer, 3);  // node 3, no edges
        Assert.True(result.Success);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void GetNodeAlters_NonExistentNode_Fails()
    {
        var (net, layer) = MakeNetworkWithUndirectedLayer();
        var result = net.GetNodeAlters(layer, 999);
        Assert.False(result.Success);
    }

    // ── Hyperedge (2-mode) ───────────────────────────────────────────────────────

    [Fact]
    public void AddHyperedge_NewHyperedge_Succeeds()
    {
        var net = MakeNetwork();
        net.AddLayerTwoMode("clubs");
        var result = net.AddHyperedge("clubs", "chess-club", [1u, 2u]);
        Assert.True(result.Success);
    }

    [Fact]
    public void GetHyperedgeNodes_ReturnsCorrectNodes()
    {
        var net = MakeNetwork();
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "chess-club", [1u, 2u]);
        var result = net.GetHyperedgeNodes("clubs", "chess-club");
        Assert.True(result.Success);
        Assert.Contains(1u, result.Value!);
        Assert.Contains(2u, result.Value!);
    }

    [Fact]
    public void RemoveHyperedge_ExistingHyperedge_Succeeds()
    {
        var net = MakeNetwork();
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "chess-club", [1u, 2u]);
        var result = net.RemoveHyperedge("clubs", "chess-club");
        Assert.True(result.Success);
    }
}
