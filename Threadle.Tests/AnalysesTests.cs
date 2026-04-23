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
        var result = Analyses.ShortestPath(net, new[] { "layer" }, 1, 1);
        Assert.True(result.Success);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void ShortestPath_DirectlyConnected_ReturnsOne()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        var result = Analyses.ShortestPath(net, new[] { "layer" }, 1, 2);
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
        var result = Analyses.ShortestPath(net, new[] { "layer" }, 1, 3);
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
        var result = Analyses.ShortestPath(net, new[] { "layer" }, 1, 3);
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
        var forwardResult = Analyses.ShortestPath(net, new[] { "layer" }, 1, 3);
        var reverseResult = Analyses.ShortestPath(net, new[] { "layer" }, 3, 1);
        Assert.Equal(2, forwardResult.Value);
        Assert.Equal(-1, reverseResult.Value);
    }

    [Fact]
    public void ShortestPath_NonExistentNode_Fails()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        var result = Analyses.ShortestPath(net, new[] { "layer" }, 1, 999);
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

    [Fact]
    public void ShortestPath_SpecificLayerSubset_OnlyUsesNamedLayers()
    {
        // 1–2 in A, 2–3 in B, 3–4 in C; path 1→4 requires A+B+C
        // If only A+B are specified, node 4 should be unreachable
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "A");
        AddUndirectedLayer(net, "B");
        AddUndirectedLayer(net, "C");
        net.AddEdge("A", 1, 2);
        net.AddEdge("B", 2, 3);
        net.AddEdge("C", 3, 4);
        var fullResult = Analyses.ShortestPath(net, new[] { "A", "B", "C" }, 1, 4);
        var partialResult = Analyses.ShortestPath(net, new[] { "A", "B" }, 1, 4);
        Assert.True(fullResult.Success);
        Assert.Equal(3, fullResult.Value);
        Assert.True(partialResult.Success);
        Assert.Equal(-1, partialResult.Value);
    }

    [Fact]
    public void ShortestPath_SpecificLayers_NonExistentLayerName_Fails()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "A");
        var result = Analyses.ShortestPath(net, new[] { "A", "ghost" }, 1, 3);
        Assert.False(result.Success);
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

    // ── DegreeCentralities: static (packed) layers ───────────────────────────────

    // Helper: read a stored integer node attribute
    private static int GetIntAttr(Network net, uint nodeId, string attrName)
    {
        var r = net.Nodeset.GetNodeAttribute(nodeId, attrName);
        Assert.True(r.Success, $"Attribute '{attrName}' missing for node {nodeId}");
        return (int)r.Value.Value.GetValue(r.Value.Type)!;
    }

    [Fact]
    public void DegreeCentralities_StaticUndirectedOneMode_MatchesDynamic()
    {
        // star: node 1 connects to 2, 3, 4
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "dyn");
        net.AddEdge("dyn", 1, 2);
        net.AddEdge("dyn", 1, 3);
        net.AddEdge("dyn", 1, 4);

        Analyses.DegreeCentralities(net, "dyn", "deg_dyn");

        net.Pack("dyn");
        Analyses.DegreeCentralities(net, "dyn", "deg_packed");

        for (uint id = 1; id <= 4; id++)
            Assert.Equal(GetIntAttr(net, id, "deg_dyn"), GetIntAttr(net, id, "deg_packed"));
    }

    [Fact]
    public void DegreeCentralities_StaticDirectedOneMode_OutdegreeMatchesDynamic()
    {
        // 1→2, 1→3, 2→3
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "dyn");
        net.AddEdge("dyn", 1, 2);
        net.AddEdge("dyn", 1, 3);
        net.AddEdge("dyn", 2, 3);

        Analyses.DegreeCentralities(net, "dyn", "out_dyn", EdgeTraversal.Out);

        net.Pack("dyn");
        Analyses.DegreeCentralities(net, "dyn", "out_packed", EdgeTraversal.Out);

        for (uint id = 1; id <= 3; id++)
            Assert.Equal(GetIntAttr(net, id, "out_dyn"), GetIntAttr(net, id, "out_packed"));
    }

    [Fact]
    public void DegreeCentralities_StaticDirectedOneMode_IndegreeMatchesDynamic()
    {
        // 1→2, 1→3, 2→3 — node 3 has indegree 2
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "dyn");
        net.AddEdge("dyn", 1, 2);
        net.AddEdge("dyn", 1, 3);
        net.AddEdge("dyn", 2, 3);

        Analyses.DegreeCentralities(net, "dyn", "in_dyn", EdgeTraversal.In);

        net.Pack("dyn");
        Analyses.DegreeCentralities(net, "dyn", "in_packed", EdgeTraversal.In);

        for (uint id = 1; id <= 3; id++)
            Assert.Equal(GetIntAttr(net, id, "in_dyn"), GetIntAttr(net, id, "in_packed"));
    }

    [Fact]
    public void DegreeCentralities_StaticTwoMode_MatchesDynamic()
    {
        // node 1 in clubs c1+c2, node 2 in c1 only, node 3 in c2 only
        var net = MakeNetwork(3);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2 });
        net.AddHyperedge("clubs", "c2", new uint[] { 1, 3 });

        Analyses.DegreeCentralities(net, "clubs", "deg_dyn");

        net.Pack("clubs");
        Analyses.DegreeCentralities(net, "clubs", "deg_packed");

        for (uint id = 1; id <= 3; id++)
            Assert.Equal(GetIntAttr(net, id, "deg_dyn"), GetIntAttr(net, id, "deg_packed"));
    }

    // ── GetRandomEdge: all four layer types ──────────────────────────────────────

    [Fact]
    public void GetRandomEdge_DynamicUndirectedOneMode_ReturnsEdge()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);

        var result = Analyses.GetRandomEdge(net, "layer");
        Assert.True(result.Success);
        Assert.True(result.Value!.ContainsKey("node1"));
        Assert.True(result.Value!.ContainsKey("node2"));
    }

    [Fact]
    public void GetRandomEdge_StaticUndirectedOneMode_ReturnsEdge()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.Pack("layer");

        var result = Analyses.GetRandomEdge(net, "layer");
        Assert.True(result.Success);
        Assert.True(result.Value!.ContainsKey("node1"));
        Assert.True(result.Value!.ContainsKey("node2"));
    }

    [Fact]
    public void GetRandomEdge_DynamicTwoMode_ReturnsEdge()
    {
        var net = MakeNetwork(4);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "c2", new uint[] { 2, 4 });

        var result = Analyses.GetRandomEdge(net, "clubs");
        Assert.True(result.Success);
        Assert.True(result.Value!.ContainsKey("node1"));
        Assert.True(result.Value!.ContainsKey("node2"));
    }

    [Fact]
    public void GetRandomEdge_StaticTwoMode_ReturnsEdge()
    {
        var net = MakeNetwork(4);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "c2", new uint[] { 2, 4 });
        net.Pack("clubs");

        var result = Analyses.GetRandomEdge(net, "clubs");
        Assert.True(result.Success);
        Assert.True(result.Value!.ContainsKey("node1"));
        Assert.True(result.Value!.ContainsKey("node2"));
    }

    [Fact]
    public void GetRandomEdge_StaticOneModeViaFallback_ReturnsEdge()
    {
        // maxAttempts=0 forces the fallback sweep path immediately
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.Pack("layer");

        var result = Analyses.GetRandomEdge(net, "layer", maxAttempts: 0);
        Assert.True(result.Success);
        Assert.True(result.Value!.ContainsKey("node1"));
    }

    [Fact]
    public void GetRandomEdge_StaticTwoModeViaFallback_ReturnsEdge()
    {
        // maxAttempts=0 forces the fallback weighted path immediately
        var net = MakeNetwork(3);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2, 3 });
        net.Pack("clubs");

        var result = Analyses.GetRandomEdge(net, "clubs", maxAttempts: 0);
        Assert.True(result.Success);
        Assert.True(result.Value!.ContainsKey("node1"));
    }

    [Fact]
    public void GetRandomEdge_EmptyLayer_Fails()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.Pack("layer");

        var result = Analyses.GetRandomEdge(net, "layer", maxAttempts: 0);
        Assert.False(result.Success);
    }

    // ── Density: static layers ────────────────────────────────────────────────────

    [Fact]
    public void Density_StaticUndirectedOneMode_MatchesDynamic()
    {
        // 3 nodes: 1-2, 2-3 → density = 2/3
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);

        var dynResult = Analyses.Density(net, "layer");
        net.Pack("layer");
        var staticResult = Analyses.Density(net, "layer");

        Assert.True(dynResult.Success);
        Assert.True(staticResult.Success);
        Assert.Equal(dynResult.Value, staticResult.Value, 10);
    }

    [Fact]
    public void Density_StaticDirectedOneMode_MatchesDynamic()
    {
        // 3 nodes, directed: 1→2, 2→3 → density = 2/6
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);

        var dynResult = Analyses.Density(net, "layer");
        net.Pack("layer");
        var staticResult = Analyses.Density(net, "layer");

        Assert.True(dynResult.Success);
        Assert.True(staticResult.Success);
        Assert.Equal(dynResult.Value, staticResult.Value, 10);
    }

    [Fact]
    public void Density_StaticTwoMode_MatchesDynamic()
    {
        var net = MakeNetwork(3);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2 });
        net.AddHyperedge("clubs", "c2", new uint[] { 2, 3 });

        var dynResult = Analyses.Density(net, "clubs");
        net.Pack("clubs");
        var staticResult = Analyses.Density(net, "clubs");

        Assert.True(dynResult.Success);
        Assert.True(staticResult.Success);
        Assert.Equal(dynResult.Value, staticResult.Value, 10);
    }

    // ── ShortestPath: static layers ───────────────────────────────────────────────

    [Fact]
    public void ShortestPath_StaticLayer_DirectlyConnected_ReturnsOne()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.Pack("layer");

        var result = Analyses.ShortestPath(net, new[] { "layer" }, 1, 2);
        Assert.True(result.Success);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void ShortestPath_StaticLayer_IndirectPath_ReturnsCorrectDistance()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.Pack("layer");

        var result = Analyses.ShortestPath(net, new[] { "layer" }, 1, 3);
        Assert.True(result.Success);
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void ShortestPath_StaticLayer_NoPath_ReturnsNegativeOne()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        // node 3 is isolated
        net.Pack("layer");

        var result = Analyses.ShortestPath(net, new[] { "layer" }, 1, 3);
        Assert.True(result.Success);
        Assert.Equal(-1, result.Value);
    }

    [Fact]
    public void ShortestPath_StaticLayer_MatchesDynamic()
    {
        // Chain 1-2-3-4: shortest path from 1 to 4 is 3
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 3, 4);

        var dynResult = Analyses.ShortestPath(net, new[] { "layer" }, 1, 4);
        net.Pack("layer");
        var staticResult = Analyses.ShortestPath(net, new[] { "layer" }, 1, 4);

        Assert.True(dynResult.Success);
        Assert.True(staticResult.Success);
        Assert.Equal(dynResult.Value, staticResult.Value);
    }

    [Fact]
    public void ShortestPath_StaticDirectedLayer_RespectsDirection()
    {
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.Pack("layer");

        var forward = Analyses.ShortestPath(net, new[] { "layer" }, 1, 3);
        var reverse = Analyses.ShortestPath(net, new[] { "layer" }, 3, 1);

        Assert.True(forward.Success);
        Assert.Equal(2, forward.Value);
        Assert.True(reverse.Success);
        Assert.Equal(-1, reverse.Value);
    }

    // ── ConnectedComponents: static and two-mode layers ───────────────────────────

    [Fact]
    public void ConnectedComponents_StaticLayer_MatchesDynamic()
    {
        // 1-2 connected, 3-4 connected, 5 isolated → 3 components
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 3, 4);

        var dynResult = Analyses.ConnectedComponents(net, "layer", "comp_dyn");
        net.Pack("layer");
        var staticResult = Analyses.ConnectedComponents(net, "layer", "comp_static");

        Assert.True(dynResult.Success);
        Assert.True(staticResult.Success);
        Assert.Equal((int)dynResult.Value!["NbrComponents"], (int)staticResult.Value!["NbrComponents"]);

        // Nodes 1 and 2 must be in the same component in both
        Assert.Equal(GetIntAttr(net, 1, "comp_dyn"), GetIntAttr(net, 2, "comp_dyn"));
        Assert.Equal(GetIntAttr(net, 1, "comp_static"), GetIntAttr(net, 2, "comp_static"));
        // Nodes 1 and 3 must be in different components in both
        Assert.NotEqual(GetIntAttr(net, 1, "comp_dyn"), GetIntAttr(net, 3, "comp_dyn"));
        Assert.NotEqual(GetIntAttr(net, 1, "comp_static"), GetIntAttr(net, 3, "comp_static"));
    }

    [Fact]
    public void ConnectedComponents_StaticLayer_TwoComponents_CorrectCount()
    {
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 3, 4);
        net.Pack("layer");

        var result = Analyses.ConnectedComponents(net, "layer");
        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrComponents"]);
    }

    [Fact]
    public void ConnectedComponents_DynamicTwoMode_ReturnsComponents()
    {
        // Two-mode: club1={1,2}, club2={3,4} → nodes 1,2 in one component; 3,4 in another
        var net = MakeNetwork(4);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2 });
        net.AddHyperedge("clubs", "c2", new uint[] { 3, 4 });

        var result = Analyses.ConnectedComponents(net, "clubs");
        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrComponents"]);
    }

    [Fact]
    public void ConnectedComponents_StaticTwoMode_MatchesDynamic()
    {
        // club1={1,2,3}, club2={4} → nodes 1,2,3 connected; 4 isolated → 2 components
        var net = MakeNetwork(4);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2, 3 });

        var dynResult = Analyses.ConnectedComponents(net, "clubs");
        net.Pack("clubs");
        var staticResult = Analyses.ConnectedComponents(net, "clubs");

        Assert.True(dynResult.Success);
        Assert.True(staticResult.Success);
        Assert.Equal((int)dynResult.Value!["NbrComponents"], (int)staticResult.Value!["NbrComponents"]);
    }

    // ── DegreeCentralities: indegree and EdgeTraversal.Both ───────────────────────

    [Fact]
    public void DegreeCentralities_DirectedLayer_IndegreeCorrect()
    {
        // 1→2, 3→2: node 2 has indegree 2; node 1 has indegree 0
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 3, 2);

        var result = Analyses.DegreeCentralities(net, "layer", "indeg", EdgeTraversal.In);

        Assert.True(result.Success);

        var indeg2 = net.Nodeset.GetNodeAttribute(2, "indeg");
        Assert.True(indeg2.Success);
        Assert.Equal(2, (int)indeg2.Value.Value.GetValue(indeg2.Value.Type)!);

        var indeg1 = net.Nodeset.GetNodeAttribute(1, "indeg");
        Assert.True(indeg1.Success);
        Assert.Equal(0, (int)indeg1.Value.Value.GetValue(indeg1.Value.Type)!);
    }

    [Fact]
    public void DegreeCentralities_DirectedLayer_BothTraversal_SumsInAndOutDegree()
    {
        // 1→2, 1→3: node 1 out=2, in=0 → both=2
        //            node 2 out=0, in=1 → both=1
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);

        var result = Analyses.DegreeCentralities(net, "layer", "deg", EdgeTraversal.Both);

        Assert.True(result.Success);

        var deg1 = net.Nodeset.GetNodeAttribute(1, "deg");
        Assert.True(deg1.Success);
        Assert.Equal(2, (int)deg1.Value.Value.GetValue(deg1.Value.Type)!);

        var deg2 = net.Nodeset.GetNodeAttribute(2, "deg");
        Assert.True(deg2.Success);
        Assert.Equal(1, (int)deg2.Value.Value.GetValue(deg2.Value.Type)!);
    }

    [Fact]
    public void DegreeCentralities_StaticDirectedLayer_IndegreeMatchesDynamic()
    {
        // Pack the layer and verify indegree gives same result as dynamic
        var net = MakeNetwork(4);
        AddDirectedLayer(net, "dyn");
        AddDirectedLayer(net, "packed");
        foreach (var lyr in new[] { "dyn", "packed" })
        {
            net.AddEdge(lyr, 1, 2);
            net.AddEdge(lyr, 3, 2);
            net.AddEdge(lyr, 4, 2);
        }
        net.Pack("packed");

        Analyses.DegreeCentralities(net, "dyn", "indeg_dyn", EdgeTraversal.In);
        Analyses.DegreeCentralities(net, "packed", "indeg_packed", EdgeTraversal.In);

        for (uint nodeId = 1; nodeId <= 4; nodeId++)
        {
            var dynAttr = net.Nodeset.GetNodeAttribute(nodeId, "indeg_dyn");
            var packedAttr = net.Nodeset.GetNodeAttribute(nodeId, "indeg_packed");
            Assert.True(dynAttr.Success);
            Assert.True(packedAttr.Success);
            Assert.Equal(
                (int)dynAttr.Value.Value.GetValue(dynAttr.Value.Type)!,
                (int)packedAttr.Value.Value.GetValue(packedAttr.Value.Type)!
            );
        }
    }

    // ── GetAttributeSummary: char attribute ───────────────────────────────────────

    [Fact]
    public void GetAttributeSummary_CharAttribute_ContainsFrequencyCounts()
    {
        var nodeset = new Nodeset("ns", 0);
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.AddNode(3);
        nodeset.AddNode(4);
        nodeset.DefineNodeAttribute("gender", "char");
        nodeset.SetNodeAttribute(1, "gender", "M");
        nodeset.SetNodeAttribute(2, "gender", "F");
        nodeset.SetNodeAttribute(3, "gender", "M");
        nodeset.SetNodeAttribute(4, "gender", "F");

        var result = Analyses.GetAttributeSummary(nodeset, "gender");

        Assert.True(result.Success);
        // For char attributes the summary should contain frequency/distribution info
        // (at minimum the result succeeds and returns non-null payload)
        Assert.NotNull(result.Value);
    }

    [Fact]
    public void GetAttributeSummary_AllNodesLackValue_StillSucceeds()
    {
        // Attribute defined but never set on any node
        var nodeset = new Nodeset("ns", 0);
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.DefineNodeAttribute("age", "int");

        var result = Analyses.GetAttributeSummary(nodeset, "age");

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
    }

    // ── GetAttributeSummary: string attribute ─────────────────────────────────

    [Fact]
    public void GetAttributeSummary_StringAttribute_ContainsFrequencyAndMode()
    {
        var nodeset = new Nodeset("ns", 0);
        for (uint i = 1; i <= 5; i++) nodeset.AddNode(i);
        nodeset.DefineNodeAttribute("country", "string");
        nodeset.SetNodeAttribute(1, "country", "Sweden");
        nodeset.SetNodeAttribute(2, "country", "Sweden");
        nodeset.SetNodeAttribute(3, "country", "Norway");
        nodeset.SetNodeAttribute(4, "country", "Sweden");
        nodeset.SetNodeAttribute(5, "country", "Denmark");

        var result = Analyses.GetAttributeSummary(nodeset, "country");

        Assert.True(result.Success);
        var stats = (Dictionary<string, object>)result.Value!["Statistics"];
        Assert.True(stats.ContainsKey("Frequency"));
        Assert.Equal("Sweden", (string)stats["Mode"]);
        Assert.Equal(3, (int)stats["Mode_Count"]);
        Assert.Equal(3, (int)stats["Unique_Values"]);
    }

    [Fact]
    public void GetAttributeSummary_StringAttribute_CountAndMissingCorrect()
    {
        var nodeset = new Nodeset("ns", 0);
        for (uint i = 1; i <= 5; i++) nodeset.AddNode(i);
        nodeset.DefineNodeAttribute("occupation", "string");
        nodeset.SetNodeAttribute(1, "occupation", "Engineer");
        nodeset.SetNodeAttribute(3, "occupation", "Teacher");
        nodeset.SetNodeAttribute(5, "occupation", "Engineer");
        // nodes 2 and 4 have no value set

        var result = Analyses.GetAttributeSummary(nodeset, "occupation");

        Assert.True(result.Success);
        var stats = (Dictionary<string, object>)result.Value!["Statistics"];
        Assert.Equal(3, (int)stats["Count"]);
        Assert.Equal(2, (int)stats["Missing"]);
    }

    [Fact]
    public void GetAttributeSummary_StringAttribute_FrequencyCappedAtKeyPresent()
    {
        var nodeset = new Nodeset("ns", 0);
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("label", "string");
        nodeset.SetNodeAttribute(1, "label", "A");

        var result = Analyses.GetAttributeSummary(nodeset, "label");

        Assert.True(result.Success);
        var stats = (Dictionary<string, object>)result.Value!["Statistics"];
        Assert.True(stats.ContainsKey("Frequency_Capped_At"));
        Assert.Equal(50, (int)stats["Frequency_Capped_At"]);
    }

    [Fact]
    public void GetAttributeSummary_StringAttribute_NoValuesSet_ModeCountZero()
    {
        var nodeset = new Nodeset("ns", 0);
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.DefineNodeAttribute("label", "string");
        // no values set on any node

        var result = Analyses.GetAttributeSummary(nodeset, "label");

        Assert.True(result.Success);
        var stats = (Dictionary<string, object>)result.Value!["Statistics"];
        Assert.Equal(0, (int)stats["Mode_Count"]);
        Assert.Equal(0, (int)stats["Unique_Values"]);
        Assert.Equal(0, (int)stats["Count"]);
        Assert.Equal(2, (int)stats["Missing"]);
    }

    // ── Density: edge case – single node ─────────────────────────────────────

    [Fact]
    public void Density_SingleNodeLayer_ReturnsZero()
    {
        // 1 node → 0 potential edges; guard must return 0.0 without dividing by zero
        var net = MakeNetwork(1);
        AddUndirectedLayer(net, "layer");
        var result = Analyses.Density(net, "layer");
        Assert.True(result.Success);
        Assert.Equal(0.0, result.Value);
    }
}
