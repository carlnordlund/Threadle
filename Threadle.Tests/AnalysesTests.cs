using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Tests;

/// <summary>
/// Tests for the public Analyses API: Density, ShortestPath, DegreeCentralities, ConnectedComponents,
/// and GetAttributeSummary.
/// </summary>
public class AnalysesTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a network with the given number of nodes (ids 1..n) and no layers.
    /// </summary>
    private static Network MakeNetwork(int nodeCount)
    {
        var nodeset = new Nodeset("ns");
        for (uint i = 1; i <= nodeCount; i++)
            nodeset.AddNode(i);
        return new Network("net", nodeset);
    }

    /// <summary>
    /// Adds an undirected binary layer to a network and returns the network+layerName.
    /// </summary>
    private static Network AddUndirectedLayer(Network net, string layerName, bool selfties = false)
    {
        net.AddLayerOneMode(layerName, EdgeDirectionality.Undirected, EdgeType.Binary, selfties);
        return net;
    }

    /// <summary>
    /// Adds a directed binary layer to a network and returns the network.
    /// </summary>
    private static Network AddDirectedLayer(Network net, string layerName)
    {
        net.AddLayerOneMode(layerName, EdgeDirectionality.Directed, EdgeType.Binary, false);
        return net;
    }

    // ── Density ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Density_EmptyLayer_ReturnsZero()
    {
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");
        // No edges added
        var result = Analyses.Density(net, "layer");
        Assert.True(result.Success);
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Density_CompleteUndirectedGraph_ReturnsOne()
    {
        // 4 nodes, all 6 pairs connected → density = 1.0
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 1, 4);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 2, 4);
        net.AddEdge("layer", 3, 4);
        var result = Analyses.Density(net, "layer");
        Assert.True(result.Success);
        Assert.Equal(1.0, result.Value, precision: 6);
    }

    [Fact]
    public void Density_CompleteDirectedGraph_ReturnsOne()
    {
        // 3 nodes, all 6 directed pairs connected → density = 1.0
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 1);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 3, 1);
        net.AddEdge("layer", 3, 2);
        var result = Analyses.Density(net, "layer");
        Assert.True(result.Success);
        Assert.Equal(1.0, result.Value, precision: 6);
    }

    [Fact]
    public void Density_PartialUndirectedGraph_ReturnsCorrectValue()
    {
        // 4 nodes, 3 of 6 possible edges → density = 0.5
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 1, 4);
        var result = Analyses.Density(net, "layer");
        Assert.True(result.Success);
        Assert.Equal(0.5, result.Value, precision: 6);
    }

    [Fact]
    public void Density_NonExistentLayer_Fails()
    {
        var net = MakeNetwork(3);
        var result = Analyses.Density(net, "ghost");
        Assert.False(result.Success);
    }

    // ── ShortestPath ─────────────────────────────────────────────────────────────

    [Fact]
    public void ShortestPath_SameNode_ReturnsZero()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        var result = Analyses.ShortestPath(net, "layer", 1, 1);
        Assert.True(result.Success);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void ShortestPath_DirectlyConnected_ReturnsOne()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        var result = Analyses.ShortestPath(net, "layer", 1, 2);
        Assert.True(result.Success);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void ShortestPath_IndirectPath_ReturnsCorrectDistance()
    {
        // 1–2–3: shortest path from 1 to 3 is 2
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        var result = Analyses.ShortestPath(net, "layer", 1, 3);
        Assert.True(result.Success);
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void ShortestPath_NoPath_ReturnsNegativeOne()
    {
        // Nodes 1, 2, 3 with edge 1–2 only; 3 is isolated
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        var result = Analyses.ShortestPath(net, "layer", 1, 3);
        Assert.True(result.Success);
        Assert.Equal(-1, result.Value);
    }

    [Fact]
    public void ShortestPath_DirectedLayer_RespectsDirection()
    {
        // 1→2→3 exists but 3 cannot reach 1 through outbound traversal
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        var forwardResult = Analyses.ShortestPath(net, "layer", 1, 3);
        var reverseResult = Analyses.ShortestPath(net, "layer", 3, 1);
        Assert.Equal(2, forwardResult.Value);
        Assert.Equal(-1, reverseResult.Value);
    }

    [Fact]
    public void ShortestPath_NonExistentNode_Fails()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        var result = Analyses.ShortestPath(net, "layer", 1, 999);
        Assert.False(result.Success);
    }

    [Fact]
    public void ShortestPath_AllLayers_FindsPathAcrossLayers()
    {
        // 1–2 in layer A, 2–3 in layer B; shortest path 1→3 across all layers = 2
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "A");
        AddUndirectedLayer(net, "B");
        net.AddEdge("A", 1, 2);
        net.AddEdge("B", 2, 3);
        // Pass null/empty layerName to use all layers
        var result = Analyses.ShortestPath(net, null, 1, 3);
        Assert.True(result.Success);
        Assert.Equal(2, result.Value);
    }

    // ── DegreeCentralities ───────────────────────────────────────────────────────

    [Fact]
    public void DegreeCentralities_UndirectedLayer_StoresCorrectDegrees()
    {
        // Node 1 connects to both 2 and 3: degree 2
        // Node 2 connects to 1 only: degree 1
        // Node 3 connects to 1 only: degree 1
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        var result = Analyses.DegreeCentralities(net, "layer", "deg");
        Assert.True(result.Success);
        var attrResult = net.Nodeset.GetNodeAttribute(1, "deg");
        Assert.True(attrResult.Success);
        var (val, type) = attrResult.Value;
        int degree1 = (int)val.GetValue(type)!;
        Assert.Equal(2, degree1);
    }

    [Fact]
    public void DegreeCentralities_DirectedLayer_OutdegreeCorrect()
    {
        // 1→2 and 1→3: outdegree of node 1 is 2
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        var result = Analyses.DegreeCentralities(net, "layer", "outdeg", EdgeTraversal.Out);
        Assert.True(result.Success);
        var attrResult = net.Nodeset.GetNodeAttribute(1, "outdeg");
        Assert.True(attrResult.Success);
        var (val, type) = attrResult.Value;
        int outdegree = (int)val.GetValue(type)!;
        Assert.Equal(2, outdegree);
    }

    [Fact]
    public void DegreeCentralities_IsolatedNode_DegreeIsZero()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        // Node 3 is isolated
        Analyses.DegreeCentralities(net, "layer", "deg");
        var attrResult = net.Nodeset.GetNodeAttribute(3, "deg");
        Assert.True(attrResult.Success);
        var (val, type) = attrResult.Value;
        int degree = (int)val.GetValue(type)!;
        Assert.Equal(0, degree);
    }

    [Fact]
    public void DegreeCentralities_NonExistentLayer_Fails()
    {
        var net = MakeNetwork(3);
        var result = Analyses.DegreeCentralities(net, "ghost", "deg");
        Assert.False(result.Success);
    }

    // ── ConnectedComponents ───────────────────────────────────────────────────────

    [Fact]
    public void ConnectedComponents_FullyConnected_ReturnsOneComponent()
    {
        // 1–2–3 fully connected chain
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        var result = Analyses.ConnectedComponents(net, "layer");
        Assert.True(result.Success);
        Assert.Equal(1, (int)result.Value!["NbrComponents"]);
    }

    [Fact]
    public void ConnectedComponents_TwoComponents_ReturnsTwoComponents()
    {
        // Nodes 1–2 connected; node 3 isolated
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        var result = Analyses.ConnectedComponents(net, "layer");
        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrComponents"]);
    }

    [Fact]
    public void ConnectedComponents_AllIsolated_ReturnsNComponents()
    {
        // 4 isolated nodes → 4 components
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");
        var result = Analyses.ConnectedComponents(net, "layer");
        Assert.True(result.Success);
        Assert.Equal(4, (int)result.Value!["NbrComponents"]);
    }

    [Fact]
    public void ConnectedComponents_StoresAttributeInNodeset()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        Analyses.ConnectedComponents(net, "layer", "comp");
        // Node 1 and 2 should have the same component id
        var attr1 = net.Nodeset.GetNodeAttribute(1, "comp");
        var attr2 = net.Nodeset.GetNodeAttribute(2, "comp");
        Assert.True(attr1.Success);
        Assert.True(attr2.Success);
        var comp1 = (int)attr1.Value.Value.GetValue(attr1.Value.Type)!;
        var comp2 = (int)attr2.Value.Value.GetValue(attr2.Value.Type)!;
        Assert.Equal(comp1, comp2);
    }

    // ── GetAttributeSummary ───────────────────────────────────────────────────────

    [Fact]
    public void GetAttributeSummary_UnknownAttribute_Fails()
    {
        var nodeset = new Nodeset("ns", 3);
        var result = Analyses.GetAttributeSummary(nodeset, "nonexistent");
        Assert.False(result.Success);
    }

    [Fact]
    public void GetAttributeSummary_IntAttribute_ContainsMeanAndMedian()
    {
        var nodeset = new Nodeset("ns", 0);
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.AddNode(3);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "10");
        nodeset.SetNodeAttribute(2, "age", "20");
        nodeset.SetNodeAttribute(3, "age", "30");

        var result = Analyses.GetAttributeSummary(nodeset, "age");
        Assert.True(result.Success);
        var stats = (Dictionary<string, object>)result.Value!["Statistics"];
        Assert.True(stats.ContainsKey("Mean"));
        Assert.True(stats.ContainsKey("Median"));
        Assert.Equal(20.0, (double)stats["Mean"], precision: 4);
    }

    [Fact]
    public void GetAttributeSummary_BoolAttribute_ContainsTrueFalseCounts()
    {
        var nodeset = new Nodeset("ns", 0);
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.AddNode(3);
        nodeset.DefineNodeAttribute("active", "bool");
        nodeset.SetNodeAttribute(1, "active", "true");
        nodeset.SetNodeAttribute(2, "active", "true");
        nodeset.SetNodeAttribute(3, "active", "false");

        var result = Analyses.GetAttributeSummary(nodeset, "active");
        Assert.True(result.Success);
        var stats = (Dictionary<string, object>)result.Value!["Statistics"];
        Assert.Equal(2, (int)stats["Count_True"]);
        Assert.Equal(1, (int)stats["Count_False"]);
    }

    [Fact]
    public void GetAttributeSummary_ReportsCorrectMissingCount()
    {
        var nodeset = new Nodeset("ns", 0);
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.AddNode(3);
        nodeset.DefineNodeAttribute("score", "int");
        nodeset.SetNodeAttribute(1, "score", "100");
        // Nodes 2 and 3 have no score set

        var result = Analyses.GetAttributeSummary(nodeset, "score");
        Assert.True(result.Success);
        var stats = (Dictionary<string, object>)result.Value!["Statistics"];
        Assert.Equal(2, (int)stats["Missing"]);
    }
}
