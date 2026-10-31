using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

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
    public void Density_UndirectedGraphWithSelfLoop_CountsSelfLoopAsOneEdge()
    {
        // 3 nodes, selfties allowed: possible edges = 3*(3+1)/2 = 6. One self-loop (1,1) and
        // one ordinary edge (1,2) → 2 actual edges → density = 2/6. Regression: NbrEdges used to
        // halve the self-loop away, undercounting the numerator.
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer", selfties: true);
        net.AddEdge("layer", 1, 1);
        net.AddEdge("layer", 1, 2);

        var result = Analyses.Density(net, "layer");
        Assert.True(result.Success);
        Assert.Equal(2.0 / 6.0, result.Value, precision: 6);
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
        Assert.Equal(0, result.Value!.Distance);
    }

    [Fact]
    public void ShortestPath_DirectlyConnected_ReturnsOne()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        var result = Analyses.ShortestPath(net, new[] { "layer" }, 1, 2);
        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.Distance);
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
        Assert.Equal(2, result.Value!.Distance);
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
        Assert.Equal(-1, result.Value!.Distance);
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
        Assert.Equal(2, forwardResult.Value!.Distance);
        Assert.Equal(-1, reverseResult.Value!.Distance);
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
        Assert.Equal(2, result.Value!.Distance);
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
        Assert.Equal(3, fullResult.Value!.Distance);
        Assert.True(partialResult.Success);
        Assert.Equal(-1, partialResult.Value!.Distance);
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

    // ── GetLayerStats ────────────────────────────────────────────────────────────
    // Shares its statistics machinery (Functions.CalculateValueStatistics) with
    // GetAttributeSummary's int/float branch, so the field set and math are the same.

    [Fact]
    public void GetLayerStats_ValuedUndirectedLayer_CountsEachEdgeOnceAndComputesExpectedStats()
    {
        // Undirected: 1-2 (value 2), 2-3 (value 4), 1-3 (value 6). Each edge must be counted
        // once (EdgeCount=3), not twice (GetAllEgoData surfaces it from both endpoints).
        var net = MakeNetwork(3);
        net.AddLayerOneMode("trust", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("trust", 1, 2, 2.0f);
        net.AddEdge("trust", 2, 3, 4.0f);
        net.AddEdge("trust", 1, 3, 6.0f);

        var result = Analyses.GetLayerStats(net, "trust");

        Assert.True(result.Success);
        Assert.Equal(3, (int)result.Value!["EdgeCount"]);
        Assert.Equal(4.0, (double)result.Value!["Mean"], precision: 5);
        Assert.Equal(4.0, (double)result.Value!["Median"], precision: 5);
        Assert.Equal(2.0f, (float)result.Value!["Min"]);
        Assert.Equal(6.0f, (float)result.Value!["Max"]);
    }

    [Fact]
    public void GetLayerStats_ValuedDirectedLayer_CountsEachArcIndependently()
    {
        // Directed: 1->2 (value 10) and 2->1 (value 20) are distinct arcs with distinct
        // values, so both must be counted (EdgeCount=2), not deduplicated like undirected edges.
        var net = MakeNetwork(2);
        net.AddLayerOneMode("flow", EdgeDirectionality.Directed, EdgeType.Valued, false);
        net.AddEdge("flow", 1, 2, 10.0f);
        net.AddEdge("flow", 2, 1, 20.0f);

        var result = Analyses.GetLayerStats(net, "flow");

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["EdgeCount"]);
        Assert.Equal(15.0, (double)result.Value!["Mean"], precision: 5);
    }

    [Fact]
    public void GetLayerStats_UndirectedLayerWithSelfLoop_IncludesSelfLoopValueOnce()
    {
        // 1-2 (value 2), plus a self-loop on node 1 (value 10). GetAllEgoData() never yields
        // self-loops for symmetric layers, so EdgeCount/Mean must include it explicitly.
        var net = MakeNetwork(3);
        net.AddLayerOneMode("trust", EdgeDirectionality.Undirected, EdgeType.Valued, true);
        net.AddEdge("trust", 1, 2, 2.0f);
        net.AddEdge("trust", 1, 1, 10.0f);

        var result = Analyses.GetLayerStats(net, "trust");

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["EdgeCount"]);
        Assert.Equal(6.0, (double)result.Value!["Mean"], precision: 5);
    }

    [Fact]
    public void GetLayerStats_BinaryLayer_Fails()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);

        var result = Analyses.GetLayerStats(net, "layer");

        Assert.False(result.Success);
    }

    [Fact]
    public void GetLayerStats_NonExistentLayer_Fails()
    {
        var net = MakeNetwork(3);
        var result = Analyses.GetLayerStats(net, "ghost");
        Assert.False(result.Success);
    }

    [Fact]
    public void GetLayerStats_TwoModeLayer_ComputesHyperedgeSizeStatsAndProjectionEstimate()
    {
        // Hyperedges of size 2, 3, 4 (nodes 1-4 reused across them). Hand-computed:
        // sizes = [2,3,4] -> Mean=3, Median=3, Min=2, Max=4.
        // Sigma k*(k-1)/2 = 1 + 3 + 6 = 10 -> EstimatedProjectedEdgesUpperBound = 10,
        // EstimatedProjectionMemoryBytes = 10 * 25 = 250.
        // NbrAffiliations = 2+3+4 = 9 (sum of hyperedge sizes).
        var net = MakeNetwork(4);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2 });
        net.AddHyperedge("clubs", "c2", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "c3", new uint[] { 1, 2, 3, 4 });

        var result = Analyses.GetLayerStats(net, "clubs");

        Assert.True(result.Success, result.Message);
        Assert.Equal(3, (int)result.Value!["NbrHyperedges"]);
        Assert.Equal(9L, (long)result.Value!["NbrAffiliations"]);
        Assert.Equal(3.0, (double)result.Value!["Mean"], precision: 5);
        Assert.Equal(3.0, (double)result.Value!["Median"], precision: 5);
        Assert.Equal(2.0f, (float)result.Value!["Min"]);
        Assert.Equal(4.0f, (float)result.Value!["Max"]);
        Assert.Equal(10.0, (double)result.Value!["EstimatedProjectedEdgesUpperBound"], precision: 5);
        Assert.Equal(250.0, (double)result.Value!["EstimatedProjectionMemoryBytes"], precision: 5);
    }

    [Fact]
    public void GetLayerStats_TwoModeLayer_MatchesActualProjectedEdgeCountAsUpperBound()
    {
        // The upper bound must never undercount the real materialized projection's edge count:
        // here, nodes {1,2,3} share both c1 and c2, so the true distinct-pair count (3, from the
        // 3-member union) is strictly less than the naive per-hyperedge sum (3 + 3 = 6) used by
        // the bound - confirming the bound is safe (>=) rather than exact.
        var net = MakeNetwork(4);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "c2", new uint[] { 1, 2, 3 });

        var statsResult = Analyses.GetLayerStats(net, "clubs");
        Assert.True(statsResult.Success, statsResult.Message);
        double upperBound = (double)statsResult.Value!["EstimatedProjectedEdgesUpperBound"];

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Count, "clubs_proj");
        Assert.True(projResult.Success, projResult.Message);
        var projLayer = (ILayerOneMode)net.Layers["clubs_proj"];

        Assert.Equal(6.0, upperBound, precision: 5); // 3 + 3, double-counting the shared pair
        Assert.Equal(3L, (long)projLayer.NbrEdges); // actual distinct pairs: (1,2), (1,3), (2,3)
        Assert.True(upperBound >= projLayer.NbrEdges);
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

    // ── Density: 2-mode exact-on-request (sampleSize=0; default stays 200, not exact) ──

    [Fact]
    public void Density_TwoMode_ExactOnRequest_MatchesHandComputedValue()
    {
        // Hand-computed expectation: distinct-alter degrees are 1->{2}=1, 2->{1,3}=2, 3->{2}=1,
        // summing to 4. Exact density = 4 / (3*2) = 2/3. Default sampleSize is 200 (sampled),
        // not exact - sampleSize must be passed explicitly here to get the exact path.
        var net = MakeNetwork(3);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2 });
        net.AddHyperedge("clubs", "c2", new uint[] { 2, 3 });

        var result = Analyses.Density(net, "clubs", sampleSize: 0);

        Assert.True(result.Success);
        Assert.Equal(4.0 / 6.0, result.Value, 10);
    }

    [Fact]
    public void Density_TwoMode_SampleSizeAtOrAboveNodeCount_MatchesExact()
    {
        var net = MakeNetwork(3);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2 });
        net.AddHyperedge("clubs", "c2", new uint[] { 2, 3 });

        var exactResult = Analyses.Density(net, "clubs", sampleSize: 0);
        var largeSampleResult = Analyses.Density(net, "clubs", sampleSize: 1000);

        Assert.True(exactResult.Success);
        Assert.True(largeSampleResult.Success);
        Assert.Equal(exactResult.Value, largeSampleResult.Value, 10);
    }

    [Fact]
    public void Density_TwoMode_DefaultSampleSize_IsTwoHundredNotExact()
    {
        // Confirms the default genuinely stays 200 (sampled), not 0/exact - the one deliberate
        // exception to every other samplesize-bearing command's "0 means exact by default"
        // convention, since this layer's exact computation costs O(sum of hyperedge sizes
        // squared), not O(sum of hyperedge sizes). On this tiny 3-node network sampleSize=200
        // already exceeds the node count, so the default still happens to compute the exact
        // value here (the "sampleSize >= nbrNodes" branch) - this test is about confirming the
        // default's numeric value, not about distinguishing sampled-vs-exact behavior on a
        // network this small.
        var net = MakeNetwork(3);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2 });
        net.AddHyperedge("clubs", "c2", new uint[] { 2, 3 });

        var defaultResult = Analyses.Density(net, "clubs");
        var explicitTwoHundredResult = Analyses.Density(net, "clubs", sampleSize: 200);

        Assert.True(defaultResult.Success);
        Assert.True(explicitTwoHundredResult.Success);
        Assert.Equal(explicitTwoHundredResult.Value, defaultResult.Value, 10);
    }

    [Fact]
    public void Density_TwoMode_SampleSizeBelowNodeCount_StillSucceeds()
    {
        // Not asserting a specific value here (sampling is random) - just confirming the
        // sampled path still runs and produces a plausible density after the default changed.
        var net = MakeNetwork(3);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2 });
        net.AddHyperedge("clubs", "c2", new uint[] { 2, 3 });

        var result = Analyses.Density(net, "clubs", sampleSize: 1);

        Assert.True(result.Success);
        Assert.True(result.Value >= 0.0 && result.Value <= 1.0);
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
        Assert.Equal(1, result.Value!.Distance);
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
        Assert.Equal(2, result.Value!.Distance);
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
        Assert.Equal(-1, result.Value!.Distance);
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
        Assert.Equal(2, forward.Value!.Distance);
        Assert.True(reverse.Success);
        Assert.Equal(-1, reverse.Value!.Distance);
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

    // ── TriadicCensus ──────────────────────────────────────────────────────────

    [Fact]
    public void TriadicCensus_NoEdges_AllEmptyTriad()
    {
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");

        var result = Analyses.TriadicCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(1L, (long)result.Value!["003"]);
        Assert.Equal(1L, (long)result.Value!["Total"]);
        Assert.Equal(0L, (long)result.Value!["012"]);
    }

    [Fact]
    public void TriadicCensus_SingleAsymmetricEdge_Is012()
    {
        // 1->2, node 3 isolated
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);

        var result = Analyses.TriadicCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(1L, (long)result.Value!["012"]);
        Assert.Equal(0L, (long)result.Value!["003"]);
    }

    [Fact]
    public void TriadicCensus_SingleMutualEdge_Is102()
    {
        // 1<->2, node 3 isolated
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 1);

        var result = Analyses.TriadicCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(1L, (long)result.Value!["102"]);
    }

    [Fact]
    public void TriadicCensus_TransitiveTriad_Is030T()
    {
        // 1->2, 1->3, 2->3: a feed-forward loop (source=1, middle=2, sink=3)
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);

        var result = Analyses.TriadicCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(1L, (long)result.Value!["030T"]);
        Assert.Equal(0L, (long)result.Value!["030C"]);
    }

    [Fact]
    public void TriadicCensus_CyclicTriad_Is030C()
    {
        // 1->2->3->1: a directed 3-cycle
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 3, 1);

        var result = Analyses.TriadicCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(1L, (long)result.Value!["030C"]);
        Assert.Equal(0L, (long)result.Value!["030T"]);
    }

    [Fact]
    public void TriadicCensus_MutualPairPlusInboundArc_Is111D()
    {
        // 1<->2 mutual, 3->1: the extra arc points into the hub (node 1) of the mutual pair
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 1);
        net.AddEdge("layer", 3, 1);

        var result = Analyses.TriadicCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(1L, (long)result.Value!["111D"]);
        Assert.Equal(0L, (long)result.Value!["111U"]);
    }

    [Fact]
    public void TriadicCensus_MutualPairPlusOutboundArc_Is111U()
    {
        // 1<->2 mutual, 1->3: the extra arc points out of the hub (node 1) of the mutual pair
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 1);
        net.AddEdge("layer", 1, 3);

        var result = Analyses.TriadicCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(1L, (long)result.Value!["111U"]);
        Assert.Equal(0L, (long)result.Value!["111D"]);
    }

    [Fact]
    public void TriadicCensus_HubEmitsToTwoMutualNodes_Is120D()
    {
        // 1<->2 mutual, 3->1 and 3->2: hub (node 3) emits to both members of the mutual pair
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 1);
        net.AddEdge("layer", 3, 1);
        net.AddEdge("layer", 3, 2);

        var result = Analyses.TriadicCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(1L, (long)result.Value!["120D"]);
        Assert.Equal(0L, (long)result.Value!["120U"]);
        Assert.Equal(0L, (long)result.Value!["120C"]);
    }

    [Fact]
    public void TriadicCensus_TwoMutualPlusOneAsymmetric_Is210()
    {
        // 1<->2 mutual, 1<->3 mutual, 2->3 asymmetric
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 1);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 3, 1);
        net.AddEdge("layer", 2, 3);

        var result = Analyses.TriadicCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(1L, (long)result.Value!["210"]);
    }

    [Fact]
    public void TriadicCensus_AllMutual_Is300()
    {
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        foreach (var (a, b) in new[] { (1u, 2u), (1u, 3u), (2u, 3u) })
        {
            net.AddEdge("layer", a, b);
            net.AddEdge("layer", b, a);
        }

        var result = Analyses.TriadicCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(1L, (long)result.Value!["300"]);
    }

    [Fact]
    public void TriadicCensus_UndirectedLayer_OnlySymmetricTypesNonZero()
    {
        // 4 nodes, undirected, edges 1-2 and 1-3 (node 4 isolated). Of the four triples:
        // {1,2,3} is a wedge (hub 1, both ties mutual) -> 201; {1,2,4} and {1,3,4} each have
        // one mutual edge and an isolated third node -> 102 (x2); {2,3,4} has no edges -> 003.
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);

        var result = Analyses.TriadicCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(1L, (long)result.Value!["201"]);
        Assert.Equal(2L, (long)result.Value!["102"]);
        Assert.Equal(1L, (long)result.Value!["003"]);
        Assert.Equal(4L, (long)result.Value!["Total"]);

        // Undirected ties are trivially mutual, so no asymmetric-only type can occur.
        string[] asymmetricTypes = ["012", "021D", "021U", "021C", "111D", "111U", "030T", "030C", "120D", "120U", "120C", "210"];
        foreach (string key in asymmetricTypes)
            Assert.Equal(0L, (long)result.Value![key]);
    }

    [Fact]
    public void TriadicCensus_TwoModeLayer_Fails()
    {
        var net = MakeNetwork(3);
        net.AddLayerTwoMode("clubs");

        var result = Analyses.TriadicCensus(net, "clubs");

        Assert.False(result.Success);
    }

    [Fact]
    public void TriadicCensus_DefaultSampleSize_IsExactMethod()
    {
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");

        var result = Analyses.TriadicCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal("Exact", (string)result.Value!["Method"]);
    }

    [Fact]
    public void TriadicCensus_Sampled_ThreeNodeNetwork_MatchesExactExactly()
    {
        // With only 3 nodes there is exactly one possible triple, so every sampled draw
        // must land on it: p = 1.0 regardless of the random sequence, so the point estimate
        // and Wilson interval are both fully deterministic (no seeding needed). Unlike a
        // naive Wald interval, Wilson does NOT collapse to a zero-width [1,1] point at p=1 —
        // it still reports a (deterministic, closed-form) non-trivial lower bound.
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);

        var result = Analyses.TriadicCensus(net, "layer", sampleSize: 5);

        Assert.True(result.Success);
        Assert.Equal("Sampled", (string)result.Value!["Method"]);
        Assert.Equal(5, (int)result.Value!["SampleSize"]);
        // Sampled counts are reported as doubles (estimates, not exact tallies) — see the
        // "double, not long" note on TriadicCensusSampled for why they are never rounded.
        Assert.Equal(1.0, (double)result.Value!["030T"], precision: 9);
        Assert.Equal(0.0, (double)result.Value!["030C"], precision: 9);
        Assert.Equal(1L, (long)result.Value!["Total"]);

        // Closed-form Wilson bounds for p=1, m=5: upper is exactly 1 (Total); lower is
        // m/(m+z^2) with z=1.959963984540054 (verified independently in Python).
        var se = (Dictionary<string, object>)result.Value!["StandardErrors"];
        Assert.Equal(0.110839400, (double)se["030T"], precision: 6);

        var ciLow = (Dictionary<string, object>)result.Value!["ConfidenceIntervalLower"];
        var ciHigh = (Dictionary<string, object>)result.Value!["ConfidenceIntervalUpper"];
        Assert.Equal(0.565517535, (double)ciLow["030T"], precision: 6);
        Assert.Equal(1.0, (double)ciHigh["030T"], precision: 9);
    }

    [Fact]
    public void TriadicCensus_Sampled_ZeroObservedCount_HasNonDegenerateConfidenceInterval()
    {
        // With sampleSize=1, at most one of the 16 types can have a nonzero sampled count —
        // the other >=15 are guaranteed to have zero observed hits, regardless of which
        // triple happens to be drawn. A naive Wald interval collapses to [0,0] in that case,
        // falsely implying certainty the true count is zero (exactly the bug a real run on
        // the Lazega network surfaced: a type with a true count of 1 was sampled zero times
        // and reported a zero-width interval). The fix (Wilson score interval) must report a
        // strictly positive upper bound instead.
        var net = MakeNetwork(6);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 3, 1);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 5, 4);

        var result = Analyses.TriadicCensus(net, "layer", sampleSize: 1);

        Assert.True(result.Success);
        var ciUpper = (Dictionary<string, object>)result.Value!["ConfidenceIntervalUpper"];
        string[] allTypes = ["003", "012", "102", "021D", "021U", "021C", "111D", "111U", "030T", "030C", "201", "120D", "120U", "120C", "210", "300"];
        int zeroObservedCount = 0;
        foreach (string type in allTypes)
        {
            if ((double)result.Value![type] == 0.0)
            {
                zeroObservedCount++;
                Assert.True((double)ciUpper[type] > 0.0, $"{type}: a zero-observed type must still have a non-degenerate (nonzero) upper confidence bound");
            }
        }
        Assert.True(zeroObservedCount >= 15, "at most one of the 16 types can be nonzero with sampleSize=1");
    }

    [Fact]
    public void TriadicCensus_Sampled_LargerNetwork_HasWellFormedUncertaintyBounds()
    {
        // Structural/invariant checks that hold regardless of the random sequence drawn:
        // every type's key set matches, CI bounds bracket the estimate and stay within [0,Total].
        var net = MakeNetwork(6);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 3, 1);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 5, 4);

        var result = Analyses.TriadicCensus(net, "layer", sampleSize: 2000);

        Assert.True(result.Success);
        Assert.Equal("Sampled", (string)result.Value!["Method"]);
        long total = (long)result.Value!["Total"];
        Assert.Equal((long)6 * 5 * 4 / 6, total);

        var se = (Dictionary<string, object>)result.Value!["StandardErrors"];
        var ciLow = (Dictionary<string, object>)result.Value!["ConfidenceIntervalLower"];
        var ciHigh = (Dictionary<string, object>)result.Value!["ConfidenceIntervalUpper"];
        string[] allTypes = ["003", "012", "102", "021D", "021U", "021C", "111D", "111U", "030T", "030C", "201", "120D", "120U", "120C", "210", "300"];
        foreach (string type in allTypes)
        {
            double estimate = (double)result.Value![type];
            double low = (double)ciLow[type];
            double high = (double)ciHigh[type];
            double stdErr = (double)se[type];

            Assert.True(stdErr >= 0.0, $"{type}: SE should be non-negative");
            Assert.True(low <= estimate + 1e-6, $"{type}: CI lower should not exceed the estimate");
            Assert.True(high >= estimate - 1e-6, $"{type}: CI upper should not be below the estimate");
            Assert.True(low >= 0.0, $"{type}: CI lower should not be negative");
            Assert.True(high <= total + 1e-6, $"{type}: CI upper should not exceed Total");
        }
    }

    [Fact]
    public void TriadicCensus_MaxThreadsOneVsMany_ProduceSameCounts()
    {
        // Exact integer arithmetic, no randomness involved: parallel execution should match
        // sequential execution bit-for-bit, not just approximately.
        if (Environment.ProcessorCount < 2)
            return; // nothing extra to exercise on a single-core runner

        string[] allTypes = ["003", "012", "102", "021D", "021U", "021C", "111D", "111U", "030T", "030C", "201", "120D", "120U", "120C", "210", "300"];

        try
        {
            var netSeq = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", 1);
            var seqResult = Analyses.TriadicCensus(netSeq, "layer");

            var netPar = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", Math.Min(4, Environment.ProcessorCount));
            var parResult = Analyses.TriadicCensus(netPar, "layer");

            Assert.True(seqResult.Success);
            Assert.True(parResult.Success);
            foreach (string type in allTypes)
                Assert.Equal((long)seqResult.Value![type], (long)parResult.Value![type]);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void TriadicCensus_MaxThreadsGreaterThanOne_Succeeds()
    {
        if (Environment.ProcessorCount < 2)
            return;

        try
        {
            UserSettings.Set("maxthreads", 2);
            var net = MakeRingWithChordsNetwork(15);
            var result = Analyses.TriadicCensus(net, "layer");
            Assert.True(result.Success);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    // ── DyadCensus ───────────────────────────────────────────────────────────────
    // A dyad is by definition a pair of two DISTINCT nodes, so a self-tie must never
    // be counted as Mutual/Asymmetric/Null, nor change Total.

    [Fact]
    public void DyadCensus_SymmetricLayer_SelftiesExcludedFromMutualCount()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer", selfties: true);
        net.AddEdge("layer", 1, 1);
        net.AddEdge("layer", 1, 2);

        var result = Analyses.DyadCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(1L, (long)result.Value!["Mutual"]);
        Assert.Equal(2L, (long)result.Value!["Null"]);
        Assert.Equal(3L, (long)result.Value!["Total"]);
    }

    [Fact]
    public void DyadCensus_DirectedLayer_SelfLoopExcludedFromArcCounts()
    {
        var net = MakeNetwork(3);
        net.AddLayerOneMode("layer", EdgeDirectionality.Directed, EdgeType.Binary, true);
        net.AddEdge("layer", 1, 1);
        net.AddEdge("layer", 1, 2);

        var result = Analyses.DyadCensus(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(0L, (long)result.Value!["Mutual"]);
        Assert.Equal(1L, (long)result.Value!["Asymmetric"]);
        Assert.Equal(2L, (long)result.Value!["Null"]);
        Assert.Equal(3L, (long)result.Value!["Total"]);
    }

    [Fact]
    public void DyadCensus_TwoModeLayer_MatchesOneModeProjection()
    {
        // Pseudo-projected (2-mode) dyad census must equal the dyad census computed on the
        // materialized binary projection of the same hyperedges: trivially all-mutual either way,
        // since an affiliation (like an undirected 1-mode tie) has no direction.
        var net = MakeNetwork(5);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "c2", new uint[] { 3, 4 });
        // node 5 stays isolated

        var twoModeResult = Analyses.DyadCensus(net, "clubs");
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Binary, "clubs_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.DyadCensus(net, "clubs_proj");
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        Assert.Equal((long)oneModeResult.Value!["Mutual"], (long)twoModeResult.Value!["Mutual"]);
        Assert.Equal((long)oneModeResult.Value!["Null"], (long)twoModeResult.Value!["Null"]);
        Assert.Equal((long)oneModeResult.Value!["Total"], (long)twoModeResult.Value!["Total"]);
        Assert.Equal(0L, (long)twoModeResult.Value!["Asymmetric"]);
        Assert.Equal(1.0, (double)twoModeResult.Value!["ArcReciprocity"], precision: 10);
        Assert.Equal(1.0, (double)twoModeResult.Value!["DyadicReciprocity"], precision: 10);
    }

    // ── CommunityDetectionLouvain ────────────────────────────────────────────────────

    [Fact]
    public void CommunityDetectionLouvain_TwoDisconnectedTriangles_FindsTwoCommunities()
    {
        // Two fully-disconnected triangles: merging them can never improve modularity,
        // so this outcome is deterministic regardless of Louvain's random visitation order.
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);

        var result = Analyses.CommunityDetectionLouvain(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);

        var c1 = net.Nodeset.GetNodeAttribute(1, "layer_community");
        var c2 = net.Nodeset.GetNodeAttribute(2, "layer_community");
        var c3 = net.Nodeset.GetNodeAttribute(3, "layer_community");
        var c4 = net.Nodeset.GetNodeAttribute(4, "layer_community");
        var c5 = net.Nodeset.GetNodeAttribute(5, "layer_community");
        var c6 = net.Nodeset.GetNodeAttribute(6, "layer_community");
        Assert.True(c1.Success && c2.Success && c3.Success && c4.Success && c5.Success && c6.Success);

        object v1 = c1.Value.Value.GetValue(c1.Value.Type)!;
        object v2 = c2.Value.Value.GetValue(c2.Value.Type)!;
        object v3 = c3.Value.Value.GetValue(c3.Value.Type)!;
        object v4 = c4.Value.Value.GetValue(c4.Value.Type)!;
        object v5 = c5.Value.Value.GetValue(c5.Value.Type)!;
        object v6 = c6.Value.Value.GetValue(c6.Value.Type)!;

        Assert.Equal(v1, v2);
        Assert.Equal(v1, v3);
        Assert.Equal(v4, v5);
        Assert.Equal(v4, v6);
        Assert.NotEqual(v1, v4);
    }

    [Fact]
    public void CommunityDetectionLouvain_SelfLoopOnNode_DoesNotChangePartitionOrCrash()
    {
        // Same two disconnected triangles, but node 1 also carries a self-loop. BuildAdjacency's
        // own doc comment claims self-loops count twice toward degree (standard undirected
        // convention); regardless of the exact degree math, a self-loop must not move node 1
        // into a different community or crash detection.
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer", selfties: true);
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 1, 1);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);

        var result = Analyses.CommunityDetectionLouvain(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);

        var c1 = net.Nodeset.GetNodeAttribute(1, "layer_community");
        var c2 = net.Nodeset.GetNodeAttribute(2, "layer_community");
        var c4 = net.Nodeset.GetNodeAttribute(4, "layer_community");
        Assert.True(c1.Success && c2.Success && c4.Success);
        Assert.Equal(c1.Value.Value.GetValue(c1.Value.Type), c2.Value.Value.GetValue(c2.Value.Type));
        Assert.NotEqual(c1.Value.Value.GetValue(c1.Value.Type), c4.Value.Value.GetValue(c4.Value.Type));
    }

    [Fact]
    public void CommunityDetectionLouvain_TwoTrianglesWithBridge_KeepsThemSeparate()
    {
        // Two triangles joined by a single bridging edge: the modularity gain from splitting
        // the bridge outweighs any gain from merging, so the two triangles remain distinct
        // communities regardless of visitation order (checked deterministically here, but
        // verified independently across 30 random seeds during development).
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);
        net.AddEdge("layer", 3, 4); // bridge

        var result = Analyses.CommunityDetectionLouvain(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
        Assert.True((double)result.Value!["Modularity"] > 0.0);

        var sizes = (List<int>)result.Value!["CommunitySizes"];
        Assert.Equal(new List<int> { 3, 3 }, sizes);
    }

    [Fact]
    public void CommunityDetectionLouvain_NoEdges_EveryNodeIsOwnCommunity()
    {
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");

        var result = Analyses.CommunityDetectionLouvain(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(4, (int)result.Value!["NbrCommunities"]);
        Assert.Equal(0.0, (double)result.Value!["Modularity"]);
    }

    [Fact]
    public void CommunityDetectionLouvain_DirectedLayer_Symmetrizes()
    {
        // Two triangles built from arcs in one direction only, joined by a bridge: since
        // modularity optimization symmetrizes directed layers (same convention as
        // Constraint/EffectiveSize), this should behave identically to the undirected case.
        var net = MakeNetwork(6);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);
        net.AddEdge("layer", 3, 4);

        var result = Analyses.CommunityDetectionLouvain(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
    }

    [Fact]
    public void CommunityDetectionLouvain_TwoModeLayer_MatchesCountProjection()
    {
        // Pseudo-projected (2-mode) Louvain's tie strength is always count-weighted, so its
        // achieved modularity should match running Louvain on the materialized count projection
        // of the same hyperedges. Compare modularity Q (not the raw partition), since RNG-driven
        // tie-breaking can land on differently-labeled but equally-optimal partitions; numRestarts
        // on both sides makes both runs converge to the same (for this small network, global)
        // optimum.
        var net = MakeAssortativityTwoModeNetwork();

        var twoModeResult = Analyses.CommunityDetectionLouvain(net, new[] { "aff" }, "c_2mode", numRestarts: 10);
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "aff", ProjectionMethod.Count, "aff_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.CommunityDetectionLouvain(net, new[] { "aff_proj" }, "c_proj", numRestarts: 10);
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        Assert.Equal((double)oneModeResult.Value!["Modularity"], (double)twoModeResult.Value!["Modularity"], precision: 6);
    }

    [Fact]
    public void CommunityDetectionLouvain_TwoModeLayerCombinedWithOneModeLayer_Fails()
    {
        var net = MakeAssortativityTwoModeNetwork();
        AddUndirectedLayer(net, "other");
        net.AddEdge("other", 1, 2);

        var result = Analyses.CommunityDetectionLouvain(net, new[] { "aff", "other" });
        Assert.False(result.Success);
        Assert.Equal("InvalidLayerType", result.Code);
    }

    [Fact]
    public void CommunityDetectionLouvain_HigherResolution_FindsAtLeastAsManyCommunities()
    {
        // Higher resolution penalizes large communities more, so it should never merge
        // groups that a lower resolution kept separate — it can only split further or match.
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);
        net.AddEdge("layer", 3, 4);

        var lowRes = Analyses.CommunityDetectionLouvain(net, new[] { "layer" }, "lowres", resolution: 0.5);
        var highRes = Analyses.CommunityDetectionLouvain(net, new[] { "layer" }, "highres", resolution: 4.0);

        Assert.True(lowRes.Success);
        Assert.True(highRes.Success);
        Assert.True((int)highRes.Value!["NbrCommunities"] >= (int)lowRes.Value!["NbrCommunities"]);
    }

    [Fact]
    public void CommunityDetectionLouvain_DefaultNumRestarts_IsOneAndReported()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);

        var result = Analyses.CommunityDetectionLouvain(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(1, (int)result.Value!["NumRestarts"]);
    }

    [Fact]
    public void CommunityDetectionLouvain_NumRestartsZero_ReportsOneSinceOneRunStillExecutes()
    {
        // LouvainCommunities treats numRestarts <= 1 as "run exactly once", so a caller-supplied
        // 0 (or a negative value) must not be echoed back verbatim - that would misreport how
        // many runs actually happened.
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);

        var result = Analyses.CommunityDetectionLouvain(net, new[] { "layer" }, numRestarts: 0);

        Assert.True(result.Success);
        Assert.Equal(1, (int)result.Value!["NumRestarts"]);
    }

    [Fact]
    public void CommunityDetectionLouvain_NumRestartsGreaterThanOne_StillFindsCorrectCommunities()
    {
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);

        var result = Analyses.CommunityDetectionLouvain(net, new[] { "layer" }, numRestarts: 5);

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
        Assert.Equal(5, (int)result.Value!["NumRestarts"]);
    }

    [Fact]
    public void CommunityDetectionLouvain_NumRestartsWithMaxThreadsGreaterThanOne_Succeeds()
    {
        if (Environment.ProcessorCount < 2)
            return;

        try
        {
            UserSettings.Set("maxthreads", 2);
            var net = MakeNetwork(6);
            AddUndirectedLayer(net, "layer");
            net.AddEdge("layer", 1, 2);
            net.AddEdge("layer", 1, 3);
            net.AddEdge("layer", 2, 3);
            net.AddEdge("layer", 4, 5);
            net.AddEdge("layer", 4, 6);
            net.AddEdge("layer", 5, 6);

            var result = Analyses.CommunityDetectionLouvain(net, new[] { "layer" }, numRestarts: 8);

            Assert.True(result.Success);
            Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    // ── CommunityDetectionLeiden ───────────────────────────────────────────────

    [Fact]
    public void CommunityDetectionLeiden_TwoDisconnectedTriangles_FindsTwoCommunities()
    {
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);

        var result = Analyses.CommunityDetectionLeiden(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
    }

    [Fact]
    public void CommunityDetectionLeiden_TwoModeLayer_MatchesCountProjection()
    {
        // Same equivalence check as Louvain: pseudo-projected (2-mode) Leiden's achieved
        // modularity should match running Leiden on the materialized count projection of the
        // same hyperedges (Leiden's refinement runs normally from the first aggregation onward,
        // so there is no further 2-mode-specific divergence to account for once past level 0).
        var net = MakeAssortativityTwoModeNetwork();

        var twoModeResult = Analyses.CommunityDetectionLeiden(net, new[] { "aff" }, "c_2mode", numRestarts: 10);
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "aff", ProjectionMethod.Count, "aff_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.CommunityDetectionLeiden(net, new[] { "aff_proj" }, "c_proj", numRestarts: 10);
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        Assert.Equal((double)oneModeResult.Value!["Modularity"], (double)twoModeResult.Value!["Modularity"], precision: 6);
    }

    [Fact]
    public void CommunityDetectionLeiden_TwoModeLayerCombinedWithOneModeLayer_Fails()
    {
        var net = MakeAssortativityTwoModeNetwork();
        AddUndirectedLayer(net, "other");
        net.AddEdge("other", 1, 2);

        var result = Analyses.CommunityDetectionLeiden(net, new[] { "aff", "other" });
        Assert.False(result.Success);
        Assert.Equal("InvalidLayerType", result.Code);
    }

    [Fact]
    public void CommunityDetectionLeiden_NumRestartsGreaterThanOne_StillFindsCorrectCommunities()
    {
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);

        var result = Analyses.CommunityDetectionLeiden(net, new[] { "layer" }, numRestarts: 5);

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
        Assert.Equal(5, (int)result.Value!["NumRestarts"]);
    }

    // ── CommunityDetectionLabelPropagation ────────────────────────────────────

    [Fact]
    public void CommunityDetectionLabelPropagation_TwoDisconnectedTriangles_FindsTwoCommunities()
    {
        // Label information can never cross a disconnected component (a node's label update
        // only looks at its own neighbors), so this outcome is deterministic regardless of
        // random visitation order — verified independently across 20 seeds during development.
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);

        var result = Analyses.CommunityDetectionLabelPropagation(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);

        var c1 = net.Nodeset.GetNodeAttribute(1, "layer_community");
        var c4 = net.Nodeset.GetNodeAttribute(4, "layer_community");
        var c2 = net.Nodeset.GetNodeAttribute(2, "layer_community");
        var c3 = net.Nodeset.GetNodeAttribute(3, "layer_community");
        var c5 = net.Nodeset.GetNodeAttribute(5, "layer_community");
        var c6 = net.Nodeset.GetNodeAttribute(6, "layer_community");
        Assert.True(c1.Success && c2.Success && c3.Success && c4.Success && c5.Success && c6.Success);

        object v1 = c1.Value.Value.GetValue(c1.Value.Type)!;
        object v2 = c2.Value.Value.GetValue(c2.Value.Type)!;
        object v3 = c3.Value.Value.GetValue(c3.Value.Type)!;
        object v4 = c4.Value.Value.GetValue(c4.Value.Type)!;
        object v5 = c5.Value.Value.GetValue(c5.Value.Type)!;
        object v6 = c6.Value.Value.GetValue(c6.Value.Type)!;

        Assert.Equal(v1, v2);
        Assert.Equal(v1, v3);
        Assert.Equal(v4, v5);
        Assert.Equal(v4, v6);
        Assert.NotEqual(v1, v4);
    }

    [Fact]
    public void CommunityDetectionLabelPropagation_NoEdges_EveryNodeIsOwnCommunity()
    {
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");

        var result = Analyses.CommunityDetectionLabelPropagation(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(4, (int)result.Value!["NbrCommunities"]);
        Assert.Equal(0.0, (double)result.Value!["Modularity"]);
    }

    [Fact]
    public void CommunityDetectionLabelPropagation_DirectedLayer_Symmetrizes()
    {
        var net = MakeNetwork(6);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);

        var result = Analyses.CommunityDetectionLabelPropagation(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
    }

    [Fact]
    public void CommunityDetectionLabelPropagation_TwoModeLayer_MatchesCountProjection()
    {
        // Same equivalence check as Louvain/Leiden. LP doesn't target modularity itself (it's
        // reported for comparability only), so a higher numRestarts is used on both sides to
        // reduce flakiness in which locally-stable labeling each run's majority-vote dynamic
        // lands on before comparing the best-of-restarts modularity.
        var net = MakeAssortativityTwoModeNetwork();

        var twoModeResult = Analyses.CommunityDetectionLabelPropagation(net, new[] { "aff" }, "c_2mode", numRestarts: 20);
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "aff", ProjectionMethod.Count, "aff_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.CommunityDetectionLabelPropagation(net, new[] { "aff_proj" }, "c_proj", numRestarts: 20);
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        Assert.Equal((double)oneModeResult.Value!["Modularity"], (double)twoModeResult.Value!["Modularity"], precision: 6);
    }

    [Fact]
    public void CommunityDetectionLabelPropagation_TwoModeLayerCombinedWithOneModeLayer_Fails()
    {
        var net = MakeAssortativityTwoModeNetwork();
        AddUndirectedLayer(net, "other");
        net.AddEdge("other", 1, 2);

        var result = Analyses.CommunityDetectionLabelPropagation(net, new[] { "aff", "other" });
        Assert.False(result.Success);
        Assert.Equal("InvalidLayerType", result.Code);
    }

    [Fact]
    public void CommunityDetectionLabelPropagation_MaxIterationsOne_StillSucceeds()
    {
        // With only 1 pass allowed, the algorithm may not fully converge, but it must still
        // terminate cleanly and return a valid (if possibly imperfect) partition.
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);

        var result = Analyses.CommunityDetectionLabelPropagation(net, new[] { "layer" }, maxIterations: 1);

        Assert.True(result.Success);
        Assert.True((int)result.Value!["NbrCommunities"] >= 1);
        var sizes = (List<int>)result.Value!["CommunitySizes"];
        Assert.Equal(6, sizes.Sum());
    }

    [Fact]
    public void CommunityDetectionLabelPropagation_NumRestartsGreaterThanOne_StillFindsCorrectCommunities()
    {
        // Label information can never cross a disconnected component, so this holds regardless
        // of how many restarts (or which one is picked by modularity) are used.
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);

        var result = Analyses.CommunityDetectionLabelPropagation(net, new[] { "layer" }, numRestarts: 5);

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
        Assert.Equal(5, (int)result.Value!["NumRestarts"]);
    }

    // ── CommunityDetectionLPAm ─────────────────────────────────────────────────

    [Fact]
    public void CommunityDetectionLPAm_TwoDisconnectedTriangles_FindsTwoCommunities()
    {
        // Moves are only taken when they increase modularity, and merging across a
        // disconnected component can never do that, so this is deterministic regardless
        // of random visitation order.
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);

        var result = Analyses.CommunityDetectionLPAm(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);

        var c1 = net.Nodeset.GetNodeAttribute(1, "layer_community");
        var c2 = net.Nodeset.GetNodeAttribute(2, "layer_community");
        var c3 = net.Nodeset.GetNodeAttribute(3, "layer_community");
        var c4 = net.Nodeset.GetNodeAttribute(4, "layer_community");
        var c5 = net.Nodeset.GetNodeAttribute(5, "layer_community");
        var c6 = net.Nodeset.GetNodeAttribute(6, "layer_community");
        Assert.True(c1.Success && c2.Success && c3.Success && c4.Success && c5.Success && c6.Success);

        object v1 = c1.Value.Value.GetValue(c1.Value.Type)!;
        object v2 = c2.Value.Value.GetValue(c2.Value.Type)!;
        object v3 = c3.Value.Value.GetValue(c3.Value.Type)!;
        object v4 = c4.Value.Value.GetValue(c4.Value.Type)!;
        object v5 = c5.Value.Value.GetValue(c5.Value.Type)!;
        object v6 = c6.Value.Value.GetValue(c6.Value.Type)!;

        Assert.Equal(v1, v2);
        Assert.Equal(v1, v3);
        Assert.Equal(v4, v5);
        Assert.Equal(v4, v6);
        Assert.NotEqual(v1, v4);
    }

    [Fact]
    public void CommunityDetectionLPAm_TwoTrianglesWithBridge_KeepsThemSeparate()
    {
        // Same bridge case used for Louvain: unlike plain label-propagation majority voting,
        // LPAm's modularity-gated moves keep the two triangles apart instead of collapsing
        // into one "monster community" across the bridge.
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);
        net.AddEdge("layer", 3, 4); // bridge

        var result = Analyses.CommunityDetectionLPAm(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
        Assert.True((double)result.Value!["Modularity"] > 0.0);

        var sizes = (List<int>)result.Value!["CommunitySizes"];
        Assert.Equal(new List<int> { 3, 3 }, sizes);
    }

    [Fact]
    public void CommunityDetectionLPAm_NoEdges_EveryNodeIsOwnCommunity()
    {
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");

        var result = Analyses.CommunityDetectionLPAm(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(4, (int)result.Value!["NbrCommunities"]);
        Assert.Equal(0.0, (double)result.Value!["Modularity"]);
    }

    [Fact]
    public void CommunityDetectionLPAm_DirectedLayer_Symmetrizes()
    {
        var net = MakeNetwork(6);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);
        net.AddEdge("layer", 3, 4);

        var result = Analyses.CommunityDetectionLPAm(net, new[] { "layer" });

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
    }

    [Fact]
    public void CommunityDetectionLPAm_TwoModeLayer_MatchesCountProjection()
    {
        // Same equivalence check as Louvain/Leiden: LPAm's modularity-gated moves make it, like
        // them, directly comparable across restarts.
        var net = MakeAssortativityTwoModeNetwork();

        var twoModeResult = Analyses.CommunityDetectionLPAm(net, new[] { "aff" }, "c_2mode", numRestarts: 10);
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "aff", ProjectionMethod.Count, "aff_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.CommunityDetectionLPAm(net, new[] { "aff_proj" }, "c_proj", numRestarts: 10);
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        Assert.Equal((double)oneModeResult.Value!["Modularity"], (double)twoModeResult.Value!["Modularity"], precision: 6);
    }

    [Fact]
    public void CommunityDetectionLPAm_TwoModeLayerCombinedWithOneModeLayer_Fails()
    {
        var net = MakeAssortativityTwoModeNetwork();
        AddUndirectedLayer(net, "other");
        net.AddEdge("other", 1, 2);

        var result = Analyses.CommunityDetectionLPAm(net, new[] { "aff", "other" });
        Assert.False(result.Success);
        Assert.Equal("InvalidLayerType", result.Code);
    }

    [Fact]
    public void CommunityDetectionLPAm_NumRestartsGreaterThanOne_StillFindsCorrectCommunities()
    {
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);

        var result = Analyses.CommunityDetectionLPAm(net, new[] { "layer" }, numRestarts: 5);

        Assert.True(result.Success);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
        Assert.Equal(5, (int)result.Value!["NumRestarts"]);
    }

    // ── CommunityDetectionGirvanNewman ──────────────────────────────────────────

    [Fact]
    public void CommunityDetectionGirvanNewman_TwoTrianglesWithBridge_FindsTwoCommunities()
    {
        // Textbook Girvan-Newman case: the bridge has by far the highest edge betweenness,
        // so it's removed first, immediately splitting the graph into the two triangles —
        // the modularity-maximizing partition, found on the very first removal.
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);
        net.AddEdge("layer", 3, 4); // bridge

        var result = Analyses.CommunityDetectionGirvanNewman(net, new[] { "layer" });

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
        Assert.True((double)result.Value!["Modularity"] > 0.0);

        var sizes = (List<int>)result.Value!["CommunitySizes"];
        Assert.Equal(new List<int> { 3, 3 }, sizes);

        var c1 = net.Nodeset.GetNodeAttribute(1, "layer_community");
        var c3 = net.Nodeset.GetNodeAttribute(3, "layer_community");
        var c4 = net.Nodeset.GetNodeAttribute(4, "layer_community");
        var c6 = net.Nodeset.GetNodeAttribute(6, "layer_community");
        Assert.True(c1.Success && c3.Success && c4.Success && c6.Success);
        object v1 = c1.Value.Value.GetValue(c1.Value.Type)!;
        object v3 = c3.Value.Value.GetValue(c3.Value.Type)!;
        object v4 = c4.Value.Value.GetValue(c4.Value.Type)!;
        object v6 = c6.Value.Value.GetValue(c6.Value.Type)!;
        Assert.Equal(v1, v3);
        Assert.Equal(v4, v6);
        Assert.NotEqual(v1, v4);
    }

    [Fact]
    public void CommunityDetectionGirvanNewman_TwoDisconnectedTriangles_FindsTwoCommunities()
    {
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);

        var result = Analyses.CommunityDetectionGirvanNewman(net, new[] { "layer" });

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
        var sizes = (List<int>)result.Value!["CommunitySizes"];
        Assert.Equal(new List<int> { 3, 3 }, sizes);
    }

    [Fact]
    public void CommunityDetectionGirvanNewman_NoEdges_EveryNodeIsOwnCommunity()
    {
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");

        var result = Analyses.CommunityDetectionGirvanNewman(net, new[] { "layer" });

        Assert.True(result.Success, result.Message);
        Assert.Equal(4, (int)result.Value!["NbrCommunities"]);
        Assert.Equal(0.0, (double)result.Value!["Modularity"]);
    }

    [Fact]
    public void CommunityDetectionGirvanNewman_DirectedLayer_Symmetrizes()
    {
        var net = MakeNetwork(6);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);
        net.AddEdge("layer", 3, 4);

        var result = Analyses.CommunityDetectionGirvanNewman(net, new[] { "layer" });

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, (int)result.Value!["NbrCommunities"]);
    }

    [Fact]
    public void CommunityDetectionGirvanNewman_TwoModeLayer_Fails()
    {
        var net = MakeNetwork(3);
        net.AddLayerTwoMode("clubs");

        var result = Analyses.CommunityDetectionGirvanNewman(net, new[] { "clubs" });

        Assert.False(result.Success);
    }

    [Fact]
    public void CommunityDetectionGirvanNewman_NumRestartsAlwaysReportedAsOne()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);

        var result = Analyses.CommunityDetectionGirvanNewman(net, new[] { "layer" });

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, (int)result.Value!["NumRestarts"]);
    }

    [Fact]
    public void CommunityDetectionGirvanNewman_HigherResolution_FindsAtLeastAsManyCommunities()
    {
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);
        net.AddEdge("layer", 3, 4);

        var lowRes = Analyses.CommunityDetectionGirvanNewman(net, new[] { "layer" }, "lowres", resolution: 0.5);
        var highRes = Analyses.CommunityDetectionGirvanNewman(net, new[] { "layer" }, "highres", resolution: 4.0);

        Assert.True(lowRes.Success);
        Assert.True(highRes.Success);
        Assert.True((int)highRes.Value!["NbrCommunities"] >= (int)lowRes.Value!["NbrCommunities"]);
    }

    [Fact]
    public void CommunityDetectionGirvanNewman_MaxThreadsOneVsMany_ProduceSameResult()
    {
        if (Environment.ProcessorCount < 2)
            return; // nothing extra to exercise on a single-core runner

        try
        {
            var netSeq = MakeRingWithChordsNetwork(12);
            UserSettings.Set("maxthreads", 1);
            var seqResult = Analyses.CommunityDetectionGirvanNewman(netSeq, new[] { "layer" });

            var netPar = MakeRingWithChordsNetwork(12);
            UserSettings.Set("maxthreads", Math.Min(4, Environment.ProcessorCount));
            var parResult = Analyses.CommunityDetectionGirvanNewman(netPar, new[] { "layer" });

            Assert.True(seqResult.Success);
            Assert.True(parResult.Success);
            Assert.Equal((int)seqResult.Value!["NbrCommunities"], (int)parResult.Value!["NbrCommunities"]);
            Assert.Equal((double)seqResult.Value!["Modularity"], (double)parResult.Value!["Modularity"], precision: 10);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
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

    // ── BetweennessCentrality / ClosenessCentrality / HarmonicCentrality ──────────
    // These three share a single parallelized BFS pass (CentralityFunctions.
    // AccumulateBFSCentralitiesForSources); correctness tests below cover both the
    // per-metric math and that parallel execution merges to the same result as sequential.

    private static double GetFloatAttr(Network net, uint nodeId, string attrName)
    {
        var attrResult = net.Nodeset.GetNodeAttribute(nodeId, attrName);
        Assert.True(attrResult.Success);
        var (val, type) = attrResult.Value;
        return (float)val.GetValue(type)!;
    }

    /// <summary>
    /// Builds an n-node ring (1-2-...-n-1) plus a few non-adjacent chords, so the graph is
    /// asymmetric enough to exercise many distinct BFS sources without being a trivial shape.
    /// </summary>
    private static Network MakeRingWithChordsNetwork(int nodeCount)
    {
        var net = MakeNetwork(nodeCount);
        AddUndirectedLayer(net, "layer");
        for (int i = 1; i <= nodeCount; i++)
        {
            int next = i == nodeCount ? 1 : i + 1;
            net.AddEdge("layer", (uint)i, (uint)next);
        }
        for (int i = 1; i + 3 <= nodeCount; i += 3)
            net.AddEdge("layer", (uint)i, (uint)(i + 3));
        return net;
    }

    [Fact]
    public void BFSBrandes_SimplePath_ForwardPassValuesCorrect()
    {
        // Diagnostic test: calls GraphAlgorithms.BFSBrandes directly, bypassing Analyses/
        // CentralityFunctions/Parallel.For entirely, to check the forward pass (Dist/Sigma/Order)
        // in isolation on a trivial 3-node undirected path (1-2-3), source=1.
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);

        var layer = (ILayerOneMode)net.Layers["layer"];
        GraphAlgorithms.SplitLayers([layer], out var oneModes, out var twoModesDynamic, out var twoModesStatic);

        // Probe 1: layer classification sanity - rules out the layer being mis-classified before
        // it ever reaches BFSBrandes.
        Assert.Single(oneModes);
        Assert.Empty(twoModesDynamic);
        Assert.Empty(twoModesStatic);

        uint[] nodeIds = net.Nodeset.NodeIdArray;
        var nodeIndex = new Dictionary<uint, int>();
        for (int i = 0; i < nodeIds.Length; i++) nodeIndex[nodeIds[i]] = i;

        // Probe 2: compact-index sanity - rules out Nodeset.NodeIdArray/index-building surprises
        // specific to this test's setup.
        Assert.Equal(3, nodeIds.Length);
        Assert.Equal(3, nodeIndex.Count);

        // Probe 3: raw traversal sanity, bypassing BFSBrandes entirely - rules out
        // LayerOneMode.VisitAlters/EdgesetBinarySymmetric itself misbehaving for this network.
        var visited = new List<uint>();
        oneModes[0].VisitAlters(1, EdgeTraversal.Out, v => visited.Add(v));
        Assert.Equal([2u], visited);

        var scratch = new GraphAlgorithms.BFSBrandesScratch();
        GraphAlgorithms.BFSBrandes(1, nodeIndex, nodeIds, oneModes, twoModesDynamic, twoModesStatic, EdgeTraversal.Out, scratch);

        int idx1 = nodeIndex[1], idx2 = nodeIndex[2], idx3 = nodeIndex[3];
        Assert.Equal(3, scratch.OrderCount);
        Assert.Equal(0, scratch.Dist[idx1]);
        Assert.Equal(1, scratch.Dist[idx2]);
        Assert.Equal(2, scratch.Dist[idx3]);
        Assert.Equal(1L, scratch.Sigma[idx1]);
        Assert.Equal(1L, scratch.Sigma[idx2]);
        Assert.Equal(1L, scratch.Sigma[idx3]);
    }

    [Fact]
    public void BetweennessCentrality_PathGraph_MiddleNodeHighest()
    {
        // 1-2-3-4-5 undirected path: node 3 sits on every shortest path between {1,2} and {4,5}
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 3, 4);
        net.AddEdge("layer", 4, 5);

        var result = Analyses.BetweennessCentrality(net, new[] { "layer" }, "bw");

        Assert.True(result.Success);
        double bwMiddle = GetFloatAttr(net, 3, "bw");
        double bwEnd = GetFloatAttr(net, 1, "bw");
        Assert.Equal(0.0, bwEnd, precision: 6);
        Assert.True(bwMiddle > bwEnd);
    }

    [Fact]
    public void BetweennessCentrality_PathGraph_MiddleNode_MatchesKnownExactValue()
    {
        // 1-2-3-4-5 undirected path. Node 3's normalized betweenness is the textbook
        // value 4/6 = 0.6667: it sits on the unique shortest path for the 4 unordered
        // pairs {1,4},{1,5},{2,4},{2,5}, out of (n-1)(n-2)/2 = 6 unordered pairs not
        // involving node 3 itself. This pins down the normalization constant exactly,
        // rather than only checking relative ordering against another node.
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 3, 4);
        net.AddEdge("layer", 4, 5);

        var result = Analyses.BetweennessCentrality(net, new[] { "layer" }, "bw");

        Assert.True(result.Success);
        Assert.Equal(4.0 / 6.0, GetFloatAttr(net, 3, "bw"), precision: 5);
    }

    [Fact]
    public void ClosenessCentrality_StarGraph_CenterReachesAllAtDistanceOne()
    {
        // Node 1 is the hub, connected to 2,3,4,5: closeness of node 1 is 1.0
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 1, 4);
        net.AddEdge("layer", 1, 5);

        var result = Analyses.ClosenessCentrality(net, new[] { "layer" }, "cl");

        Assert.True(result.Success);
        Assert.Equal(1.0, GetFloatAttr(net, 1, "cl"), precision: 6);
    }

    [Fact]
    public void HarmonicCentrality_StarGraph_CenterHighest()
    {
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 1, 4);
        net.AddEdge("layer", 1, 5);

        var result = Analyses.HarmonicCentrality(net, new[] { "layer" }, "hm", normalize: false);

        Assert.True(result.Success);
        double hmCenter = GetFloatAttr(net, 1, "hm");
        double hmLeaf = GetFloatAttr(net, 2, "hm");
        Assert.Equal(4.0, hmCenter, precision: 6); // 4 leaves, each at distance 1
        Assert.True(hmCenter > hmLeaf);
    }

    [Fact]
    public void ClosenessCentrality_TwoModeLayer_MatchesHandComputedValues()
    {
        // Hand-computed via pseudo-projected BFS over the standard 3-hyperedge fixture
        // (c1={1,2,3}, c2={2,3,4}, c3={4,5,6}; projected edges 1-2,1-3,2-3,2-4,3-4,4-5,4-6,5-6).
        // Node 1's own BFS stresses the "multiple members of one hyperedge arrive at the same
        // level" case this test exists to cover: c2's members 2 and 3 both sit at level 1 and
        // both reach node 4 at level 2. Closeness = reach^2 / ((n-1)*distSum), n=6.
        var net = MakeAssortativityTwoModeNetwork();

        var result = Analyses.ClosenessCentrality(net, new[] { "aff" }, "cl");

        Assert.True(result.Success, result.Message);
        Assert.Equal(25.0 / 50.0, GetFloatAttr(net, 1, "cl"), precision: 6); // distSum=10
        Assert.Equal(25.0 / 35.0, GetFloatAttr(net, 2, "cl"), precision: 6); // distSum=7
        Assert.Equal(25.0 / 35.0, GetFloatAttr(net, 3, "cl"), precision: 6); // distSum=7
        Assert.Equal(25.0 / 30.0, GetFloatAttr(net, 4, "cl"), precision: 6); // distSum=6
        Assert.Equal(25.0 / 45.0, GetFloatAttr(net, 5, "cl"), precision: 6); // distSum=9
        Assert.Equal(25.0 / 45.0, GetFloatAttr(net, 6, "cl"), precision: 6); // distSum=9
    }

    [Fact]
    public void HarmonicCentrality_TwoModeLayer_MatchesHandComputedValues()
    {
        // Same fixture and hand-derived distances as the closeness test above.
        // Raw harmonic sum (1/distance to every reachable node), unnormalized.
        var net = MakeAssortativityTwoModeNetwork();

        var result = Analyses.HarmonicCentrality(net, new[] { "aff" }, "hm", normalize: false);

        Assert.True(result.Success, result.Message);
        Assert.Equal(19.0 / 6.0, GetFloatAttr(net, 1, "hm"), precision: 6); // 1+1+1/2+1/3+1/3
        Assert.Equal(4.0, GetFloatAttr(net, 2, "hm"), precision: 6);       // 1+1+1+1/2+1/2
        Assert.Equal(4.0, GetFloatAttr(net, 3, "hm"), precision: 6);
        Assert.Equal(4.5, GetFloatAttr(net, 4, "hm"), precision: 6);       // 1+1+1+1+1/2
        Assert.Equal(10.0 / 3.0, GetFloatAttr(net, 5, "hm"), precision: 6); // 1+1+1/2+1/2+1/3
        Assert.Equal(10.0 / 3.0, GetFloatAttr(net, 6, "hm"), precision: 6);
    }

    [Fact]
    public void BetweennessCentrality_NonExistentLayer_Fails()
    {
        var net = MakeNetwork(3);
        var result = Analyses.BetweennessCentrality(net, new[] { "ghost" });
        Assert.False(result.Success);
    }

    [Fact]
    public void BetweennessClosenessHarmonic_MaxThreadsOneVsMany_ProduceSameValues()
    {
        if (Environment.ProcessorCount < 2)
            return; // nothing extra to exercise on a single-core runner

        try
        {
            var netSeq = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", 1);
            Analyses.BetweennessCentrality(netSeq, new[] { "layer" }, "bw");
            Analyses.ClosenessCentrality(netSeq, new[] { "layer" }, "cl");
            Analyses.HarmonicCentrality(netSeq, new[] { "layer" }, "hm");

            var netPar = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", Math.Min(4, Environment.ProcessorCount));
            Analyses.BetweennessCentrality(netPar, new[] { "layer" }, "bw");
            Analyses.ClosenessCentrality(netPar, new[] { "layer" }, "cl");
            Analyses.HarmonicCentrality(netPar, new[] { "layer" }, "hm");

            for (uint id = 1; id <= 20; id++)
            {
                Assert.Equal(GetFloatAttr(netSeq, id, "bw"), GetFloatAttr(netPar, id, "bw"), precision: 5);
                Assert.Equal(GetFloatAttr(netSeq, id, "cl"), GetFloatAttr(netPar, id, "cl"), precision: 5);
                Assert.Equal(GetFloatAttr(netSeq, id, "hm"), GetFloatAttr(netPar, id, "hm"), precision: 5);
            }
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    // ── PageRank ────────────────────────────────────────────────────────────────

    [Fact]
    public void PageRank_RegularUndirectedCycle_ConvergesToUniform()
    {
        // 1-2-3-4-1 undirected cycle: every node has the same (out-)degree, so the
        // stationary distribution of the underlying random walk is exactly uniform,
        // 1/n for every node, regardless of damping factor - a clean, exact sanity
        // check independent of any iteration-count/tolerance assumption.
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 3, 4);
        net.AddEdge("layer", 4, 1);

        var result = Analyses.PageRank(net, new[] { "layer" }, "pr");

        Assert.True(result.Success);
        for (uint id = 1; id <= 4; id++)
            Assert.Equal(0.25, GetFloatAttr(net, id, "pr"), precision: 6);
    }

    [Fact]
    public void PageRank_DirectedWithDanglingNode_MatchesHandComputedFixedPoint()
    {
        // Directed edge 1->2 only; node 2 is dangling (no out-edges). Hand-solving the
        // fixed point (pr1+pr2=1, default damping=0.85):
        //   pr1 = (1-d)/2 + d*pr2/2             (teleport + its share of node 2's dangling mass)
        //   pr2 = (1-d)/2 + d*pr1 + d*pr2/2     (teleport + node 1's full share + dangling mass)
        // Substituting pr2=1-pr1 into the first equation and solving:
        //   pr1 = 0.075 + 0.425*(1-pr1) => 1.425*pr1 = 0.5 => pr1 = 20/57, pr2 = 37/57.
        var net = MakeNetwork(2);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);

        var result = Analyses.PageRank(net, new[] { "layer" }, "pr");

        Assert.True(result.Success);
        Assert.Equal(20.0 / 57.0, GetFloatAttr(net, 1, "pr"), precision: 6);
        Assert.Equal(37.0 / 57.0, GetFloatAttr(net, 2, "pr"), precision: 6);
    }

    [Fact]
    public void PageRank_TwoModeLayer_MatchesOneModeBinaryProjection()
    {
        // pagerank()'s 2-mode precompute dedupes co-affiliates into a HashSet (no
        // shared-hyperedge-count weighting), the same binary convention projecttwomode()'s
        // 'binary' method uses - so the pseudo-projected 2-mode result must match running
        // pagerank() on the materialized binary projection of the same hyperedges.
        var net = MakeAssortativityTwoModeNetwork();

        var twoModeResult = Analyses.PageRank(net, new[] { "aff" }, "pr_2mode");
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "aff", ProjectionMethod.Binary, "aff_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.PageRank(net, new[] { "aff_proj" }, "pr_1mode");
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        for (uint id = 1; id <= 6; id++)
            Assert.Equal(GetFloatAttr(net, id, "pr_1mode"), GetFloatAttr(net, id, "pr_2mode"), precision: 5);
    }

    [Fact]
    public void PageRank_MaxThreadsOneVsMany_ProduceSameValues()
    {
        if (Environment.ProcessorCount < 2)
            return; // nothing extra to exercise on a single-core runner

        try
        {
            var netSeq = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", 1);
            Analyses.PageRank(netSeq, new[] { "layer" }, "pr");

            var netPar = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", Math.Min(4, Environment.ProcessorCount));
            Analyses.PageRank(netPar, new[] { "layer" }, "pr");

            for (uint id = 1; id <= 20; id++)
                Assert.Equal(GetFloatAttr(netSeq, id, "pr"), GetFloatAttr(netPar, id, "pr"), precision: 5);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void BetweennessCentrality_MaxThreadsGreaterThanOne_Succeeds()
    {
        if (Environment.ProcessorCount < 2)
            return;

        try
        {
            UserSettings.Set("maxthreads", 2);
            var net = MakeRingWithChordsNetwork(15);
            var result = Analyses.BetweennessCentrality(net, new[] { "layer" }, "bw");
            Assert.True(result.Success);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    // ── EdgeBetweennessCentrality ────────────────────────────────────────────────
    // Shares the Brandes backward pass with node betweenness (CentralityFunctions.
    // AccumulateEdgeBetweenness credits each predecessor edge with the same
    // contribution term node betweenness folds into the node's delta).

    private static ILayerOneMode GetLayer(Network net, string layerName) => (ILayerOneMode)net.Layers[layerName];

    [Fact]
    public void EdgeBetweennessCentrality_TwoTrianglesWithBridge_BridgeHasHighestScore()
    {
        // Two triangles {1,2,3} and {4,5,6} joined by a single bridge edge (3,4): every
        // cross-triangle shortest path must cross the bridge, so it should dominate every
        // triangle-internal edge.
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);
        net.AddEdge("layer", 3, 4);

        var result = Analyses.EdgeBetweennessCentrality(net, "layer", "layer-edgebetweenness");

        Assert.True(result.Success);
        var eb = GetLayer(net, "layer-edgebetweenness");
        double bridge = eb.GetEdgeValue(3, 4);
        double internalEdge = eb.GetEdgeValue(1, 2);
        Assert.True(bridge > internalEdge, $"Bridge ({bridge}) should exceed an internal triangle edge ({internalEdge})");
    }

    [Fact]
    public void EdgeBetweennessCentrality_DefaultNewLayerName_UsesEdgebetweennessSuffix()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "friends");
        net.AddEdge("friends", 1, 2);
        net.AddEdge("friends", 2, 3);

        string newLayerName = net.GetNextAvailableLayerName("friends-edgebetweenness");
        var result = Analyses.EdgeBetweennessCentrality(net, "friends", newLayerName);

        Assert.True(result.Success);
        Assert.True(net.Layers.ContainsKey("friends-edgebetweenness"));
    }

    [Fact]
    public void EdgeBetweennessCentrality_NewLayer_MatchesSourceDirectionality()
    {
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);

        var result = Analyses.EdgeBetweennessCentrality(net, "layer", "layer-edgebetweenness");

        Assert.True(result.Success);
        var eb = GetLayer(net, "layer-edgebetweenness");
        Assert.True(eb.IsDirectional);
    }

    [Fact]
    public void EdgeBetweennessCentrality_NormalizeFalse_YieldsLargerRawValues()
    {
        var net = MakeNetwork(6);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 4, 6);
        net.AddEdge("layer", 5, 6);
        net.AddEdge("layer", 3, 4);

        Analyses.EdgeBetweennessCentrality(net, "layer", "normalized", normalize: true);
        Analyses.EdgeBetweennessCentrality(net, "layer", "raw", normalize: false);

        double normalizedBridge = GetLayer(net, "normalized").GetEdgeValue(3, 4);
        double rawBridge = GetLayer(net, "raw").GetEdgeValue(3, 4);
        Assert.True(rawBridge > normalizedBridge);
    }

    [Fact]
    public void EdgeBetweennessCentrality_NonExistentLayer_Fails()
    {
        var net = MakeNetwork(3);
        var result = Analyses.EdgeBetweennessCentrality(net, "ghost", "ghost-edgebetweenness");
        Assert.False(result.Success);
    }

    [Fact]
    public void EdgeBetweennessCentrality_TwoModeLayer_Fails()
    {
        var net = MakeNetwork(3);
        net.AddLayerTwoMode("clubs");

        var result = Analyses.EdgeBetweennessCentrality(net, "clubs", "clubs-edgebetweenness");

        Assert.False(result.Success);
    }

    [Fact]
    public void EdgeBetweennessCentrality_NewLayerNameAlreadyExists_Fails()
    {
        var net = MakeNetwork(3);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        AddUndirectedLayer(net, "taken");

        var result = Analyses.EdgeBetweennessCentrality(net, "layer", "taken");

        Assert.False(result.Success);
    }

    [Fact]
    public void EdgeBetweennessCentrality_MaxThreadsOneVsMany_ProduceSameValues()
    {
        if (Environment.ProcessorCount < 2)
            return; // nothing extra to exercise on a single-core runner

        try
        {
            var netSeq = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", 1);
            Analyses.EdgeBetweennessCentrality(netSeq, "layer", "eb");

            var netPar = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", Math.Min(4, Environment.ProcessorCount));
            Analyses.EdgeBetweennessCentrality(netPar, "layer", "eb");

            var seqLayer = GetLayer(netSeq, "eb");
            var parLayer = GetLayer(netPar, "eb");
            foreach (var (egoId, alters, _) in seqLayer.GetAllEgoData())
                foreach (uint alterId in alters.Span)
                    if (alterId > egoId)
                        Assert.Equal(seqLayer.GetEdgeValue(egoId, alterId), parLayer.GetEdgeValue(egoId, alterId), precision: 5);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void EdgeBetweennessCentrality_MaxThreadsGreaterThanOne_Succeeds()
    {
        if (Environment.ProcessorCount < 2)
            return;

        try
        {
            UserSettings.Set("maxthreads", 2);
            var net = MakeRingWithChordsNetwork(15);
            var result = Analyses.EdgeBetweennessCentrality(net, "layer", "eb");
            Assert.True(result.Success);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    // ── Eccentricity ─────────────────────────────────────────────────────────────
    // Unlike Diameter, computed per node individually — not restricted to the largest
    // component — so disconnected pieces and isolated nodes each get their own value.

    [Fact]
    public void Eccentricity_PathGraph_MatchesKnownValues()
    {
        // 1-2-3-4-5 undirected path: eccentricities are 4,3,2,3,4 (distance to the farthest end).
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 3, 4);
        net.AddEdge("layer", 4, 5);

        var result = Analyses.Eccentricity(net, "layer");

        Assert.True(result.Success, result.Message);
        (int, int)[] expected = [(1, 4), (2, 3), (3, 2), (4, 3), (5, 4)];
        foreach (var (nodeId, expectedEcc) in expected)
        {
            var attr = net.Nodeset.GetNodeAttribute((uint)nodeId, "eccentricity");
            Assert.True(attr.Success);
            Assert.Equal(expectedEcc, (int)attr.Value.Value.GetValue(attr.Value.Type)!);
        }
    }

    [Fact]
    public void Eccentricity_DisconnectedNetwork_EachComponentGetsItsOwnValue()
    {
        // Triangle {1,2,3} (eccentricity 1 for every member) + isolated node 4 (eccentricity 0).
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);

        var result = Analyses.Eccentricity(net, "layer");

        Assert.True(result.Success, result.Message);
        foreach (uint nodeId in new uint[] { 1, 2, 3 })
        {
            var attr = net.Nodeset.GetNodeAttribute(nodeId, "eccentricity");
            Assert.Equal(1, (int)attr.Value.Value.GetValue(attr.Value.Type)!);
        }
        var isolated = net.Nodeset.GetNodeAttribute(4, "eccentricity");
        Assert.Equal(0, (int)isolated.Value.Value.GetValue(isolated.Value.Type)!);
    }

    [Fact]
    public void Eccentricity_DirectedLayer_TraversalChangesResult()
    {
        // 1->2->3: from 'out', node 3's eccentricity is 0 (no outbound reach); from 'both',
        // direction is ignored so node 3 can reach node 1 (distance 2).
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);

        var outResult = Analyses.Eccentricity(net, "layer", "ecc_out", EdgeTraversal.Out);
        var bothResult = Analyses.Eccentricity(net, "layer", "ecc_both", EdgeTraversal.Both);

        Assert.True(outResult.Success);
        Assert.True(bothResult.Success);
        var out3 = net.Nodeset.GetNodeAttribute(3, "ecc_out");
        var both3 = net.Nodeset.GetNodeAttribute(3, "ecc_both");
        Assert.Equal(0, (int)out3.Value.Value.GetValue(out3.Value.Type)!);
        Assert.Equal(2, (int)both3.Value.Value.GetValue(both3.Value.Type)!);
    }

    [Fact]
    public void Eccentricity_NonExistentLayer_Fails()
    {
        var net = MakeNetwork(3);
        var result = Analyses.Eccentricity(net, "ghost");
        Assert.False(result.Success);
    }

    [Fact]
    public void Eccentricity_TwoModeLayer_MatchesOneModeProjection()
    {
        // Pseudo-projected (2-mode) eccentricity must equal eccentricity computed on the
        // materialized binary projection of the same hyperedges.
        var net = MakeNetwork(5);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "c2", new uint[] { 3, 4 });
        // node 5 stays isolated (no hyperedge membership)

        var twoModeResult = Analyses.Eccentricity(net, "clubs", "ecc_2mode");
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Binary, "clubs_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.Eccentricity(net, "clubs_proj", "ecc_1mode");
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        for (uint nodeId = 1; nodeId <= 5; nodeId++)
        {
            var twoModeAttr = net.Nodeset.GetNodeAttribute(nodeId, "ecc_2mode");
            var oneModeAttr = net.Nodeset.GetNodeAttribute(nodeId, "ecc_1mode");
            Assert.Equal(
                (int)oneModeAttr.Value.Value.GetValue(oneModeAttr.Value.Type)!,
                (int)twoModeAttr.Value.Value.GetValue(twoModeAttr.Value.Type)!);
        }
    }

    [Fact]
    public void Eccentricity_MaxThreadsOneVsMany_ProduceSameValues()
    {
        if (Environment.ProcessorCount < 2)
            return; // nothing extra to exercise on a single-core runner

        try
        {
            var netSeq = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", 1);
            Analyses.Eccentricity(netSeq, "layer");

            var netPar = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", Math.Min(4, Environment.ProcessorCount));
            Analyses.Eccentricity(netPar, "layer");

            foreach (uint nodeId in netSeq.Nodeset.NodeIdArray)
            {
                var seqAttr = netSeq.Nodeset.GetNodeAttribute(nodeId, "eccentricity");
                var parAttr = netPar.Nodeset.GetNodeAttribute(nodeId, "eccentricity");
                Assert.Equal((int)seqAttr.Value.Value.GetValue(seqAttr.Value.Type)!, (int)parAttr.Value.Value.GetValue(parAttr.Value.Type)!);
            }
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    // ── Diameter ─────────────────────────────────────────────────────────────────
    // Restricted to the largest weakly connected component; exact mode runs a full BFS
    // from every node in that component (NetworkLevelFunctions.Diameter), sampled mode
    // estimates AvgDistance via per-source means (normal-approximation CI) and reports a
    // DiameterLowerBound via double-sweep trials rather than a confidence interval.

    [Fact]
    public void Diameter_PathGraph_ExactDiameterAndAvgDistance()
    {
        // 1-2-3-4-5 undirected path: diameter is the 1↔5 distance (4); AvgDistance over
        // all 20 ordered reachable pairs works out to exactly 2.0.
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 3, 4);
        net.AddEdge("layer", 4, 5);

        var result = Analyses.Diameter(net, "layer");

        Assert.True(result.Success);
        Assert.Equal("Exact", result.Value!["Method"]);
        Assert.Equal(4, (int)result.Value!["Diameter"]);
        Assert.Equal(2.0, (double)result.Value!["AvgDistance"], precision: 10);
        Assert.Equal(20L, (long)result.Value!["PairsConsidered"]);
        Assert.Equal(5, (int)result.Value!["ComponentSize"]);
        Assert.Equal(5, (int)result.Value!["TotalNodes"]);
    }

    [Fact]
    public void Diameter_DisconnectedNetwork_RestrictsToLargestComponent()
    {
        // A 3-node triangle {1,2,3} plus a separate 4-node path {4-5-6-7}: the path is the
        // larger (weakly connected) component, so diameter/avgdistance should reflect it
        // (diameter 3), not the triangle (diameter 1).
        var net = MakeNetwork(7);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 4, 5);
        net.AddEdge("layer", 5, 6);
        net.AddEdge("layer", 6, 7);

        var result = Analyses.Diameter(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(3, (int)result.Value!["Diameter"]);
        Assert.Equal(4, (int)result.Value!["ComponentSize"]);
        Assert.Equal(7, (int)result.Value!["TotalNodes"]);
    }

    [Fact]
    public void Diameter_DirectedLayer_TraversalChangesReachablePairs()
    {
        // 1->2->3: following 'out' only, node 3 reaches nobody, so just 3 ordered pairs are
        // reachable; following 'both' ignores direction, so all 6 ordered pairs are reachable.
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);

        var outResult = Analyses.Diameter(net, "layer", traversal: EdgeTraversal.Out);
        var bothResult = Analyses.Diameter(net, "layer", traversal: EdgeTraversal.Both);

        Assert.True(outResult.Success);
        Assert.True(bothResult.Success);
        Assert.Equal(3L, (long)outResult.Value!["PairsConsidered"]);
        Assert.Equal(6L, (long)bothResult.Value!["PairsConsidered"]);
        Assert.Equal(2, (int)outResult.Value!["Diameter"]);
        Assert.Equal(2, (int)bothResult.Value!["Diameter"]);
    }

    [Fact]
    public void Diameter_SampledMode_LowerBoundAndConfidenceIntervalAreConsistent()
    {
        var net = MakeRingWithChordsNetwork(15);
        var exact = Analyses.Diameter(net, "layer");
        var sampled = Analyses.Diameter(net, "layer", sampleSize: 8, numSweeps: 5);

        Assert.True(exact.Success);
        Assert.True(sampled.Success);
        Assert.Equal("Sampled", sampled.Value!["Method"]);
        int diameterLowerBound = (int)sampled.Value!["DiameterLowerBound"];
        Assert.True(diameterLowerBound <= (int)exact.Value!["Diameter"],
            $"DiameterLowerBound ({diameterLowerBound}) must not exceed the true diameter ({exact.Value!["Diameter"]})");
        Assert.True(diameterLowerBound > 0);
        double avg = (double)sampled.Value!["AvgDistance"];
        double ciLower = (double)sampled.Value!["ConfidenceIntervalLower"];
        double ciUpper = (double)sampled.Value!["ConfidenceIntervalUpper"];
        Assert.True(ciLower <= avg && avg <= ciUpper);
        Assert.Equal(8, (int)sampled.Value!["SampleSize"]);
    }

    [Fact]
    public void Diameter_EmptyNetwork_Fails()
    {
        var net = MakeNetwork(0);
        AddUndirectedLayer(net, "layer");

        var result = Analyses.Diameter(net, "layer");

        Assert.False(result.Success);
    }

    [Fact]
    public void Diameter_NonExistentLayer_Fails()
    {
        var net = MakeNetwork(3);
        var result = Analyses.Diameter(net, "ghost");
        Assert.False(result.Success);
    }

    [Fact]
    public void Diameter_TwoModeLayer_MatchesOneModeProjection()
    {
        // Pseudo-projected (2-mode) diameter/avgdistance must equal the same measures computed
        // on the materialized binary projection of the same hyperedges (restricted, on both
        // sides, to the largest weakly connected component).
        var net = MakeNetwork(7);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "c2", new uint[] { 3, 4, 5 });
        net.AddHyperedge("clubs", "c3", new uint[] { 6, 7 });

        var twoModeResult = Analyses.Diameter(net, "clubs");
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Binary, "clubs_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.Diameter(net, "clubs_proj");
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        Assert.Equal((int)oneModeResult.Value!["Diameter"], (int)twoModeResult.Value!["Diameter"]);
        Assert.Equal((int)oneModeResult.Value!["ComponentSize"], (int)twoModeResult.Value!["ComponentSize"]);
        Assert.Equal((double)oneModeResult.Value!["AvgDistance"], (double)twoModeResult.Value!["AvgDistance"]);
    }

    // ── Diameter: diameterOnly (iFUB) ──────────────────────────────────────────

    [Fact]
    public void Diameter_DiameterOnly_UndirectedPathGraph_MatchesExactDiameterViaIFUB()
    {
        // Same 5-node path as Diameter_PathGraph_ExactDiameterAndAvgDistance (true diameter 4).
        // Undirected, so iFUB applies; diameterOnly's result must match the exhaustive method's
        // Diameter exactly and must not carry AvgDistance/PairsConsidered.
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 3, 4);
        net.AddEdge("layer", 4, 5);

        var result = Analyses.Diameter(net, "layer", diameterOnly: true);

        Assert.True(result.Success);
        Assert.Equal("iFUB", result.Value!["Algorithm"]);
        Assert.Equal(4, (int)result.Value!["Diameter"]);
        Assert.Equal(5, (int)result.Value!["ComponentSize"]);
        Assert.Equal(5, (int)result.Value!["TotalNodes"]);
        Assert.False(result.Value!.ContainsKey("AvgDistance"));
        Assert.False(result.Value!.ContainsKey("PairsConsidered"));
    }

    [Fact]
    public void Diameter_DiameterOnly_RingWithChords_MatchesExhaustiveDiameter()
    {
        // A richer, less trivially-shaped graph than a path - cross-checks iFUB's early
        // termination logic against the (unchanged, trusted) exhaustive all-pairs Diameter on
        // the same network, rather than a single hand-derived example.
        var net = MakeRingWithChordsNetwork(20);

        var exact = Analyses.Diameter(net, "layer");
        var fast = Analyses.Diameter(net, "layer", diameterOnly: true);

        Assert.True(exact.Success);
        Assert.True(fast.Success);
        Assert.Equal("iFUB", fast.Value!["Algorithm"]);
        Assert.Equal((int)exact.Value!["Diameter"], (int)fast.Value!["Diameter"]);
        Assert.Equal((int)exact.Value!["ComponentSize"], (int)fast.Value!["ComponentSize"]);
    }

    [Fact]
    public void Diameter_DiameterOnly_DirectedOutTraversal_FallsBackToAllPairs()
    {
        // 1->2->3, default 'out' traversal: distances are not symmetric (iFUB's exactness proof
        // doesn't apply), so diameterOnly falls back to the exhaustive all-pairs pass - same
        // Diameter value as the non-diameterOnly call, but Algorithm reports which path ran and
        // AvgDistance/PairsConsidered are still omitted.
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);

        var exact = Analyses.Diameter(net, "layer", traversal: EdgeTraversal.Out);
        var fast = Analyses.Diameter(net, "layer", traversal: EdgeTraversal.Out, diameterOnly: true);

        Assert.True(exact.Success);
        Assert.True(fast.Success);
        Assert.Equal("AllPairs", fast.Value!["Algorithm"]);
        Assert.Equal((int)exact.Value!["Diameter"], (int)fast.Value!["Diameter"]);
        Assert.False(fast.Value!.ContainsKey("AvgDistance"));
    }

    [Fact]
    public void Diameter_DiameterOnly_DirectedBothTraversal_UsesIFUB()
    {
        // Same directed layer as above, but traversal=Both makes the query effectively
        // symmetric (BFS follows edges in both directions regardless of the layer's own
        // directionality), so diameterOnly uses iFUB here even though the layer itself is
        // directed.
        var net = MakeNetwork(3);
        AddDirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 2, 3);

        var exact = Analyses.Diameter(net, "layer", traversal: EdgeTraversal.Both);
        var fast = Analyses.Diameter(net, "layer", traversal: EdgeTraversal.Both, diameterOnly: true);

        Assert.True(exact.Success);
        Assert.True(fast.Success);
        Assert.Equal("iFUB", fast.Value!["Algorithm"]);
        Assert.Equal((int)exact.Value!["Diameter"], (int)fast.Value!["Diameter"]);
    }

    [Fact]
    public void Diameter_DiameterOnly_TwoModeLayer_UsesIFUB_MatchesExactDiameter()
    {
        // Same hyperedges as Diameter_TwoModeLayer_MatchesOneModeProjection. A 2-mode layer has
        // no direction concept at all, so it's always effectively symmetric - diameterOnly must
        // use iFUB here regardless of the (moot) traversal argument.
        var net = MakeNetwork(7);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "c1", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "c2", new uint[] { 3, 4, 5 });
        net.AddHyperedge("clubs", "c3", new uint[] { 6, 7 });

        var exact = Analyses.Diameter(net, "clubs");
        var fast = Analyses.Diameter(net, "clubs", diameterOnly: true);

        Assert.True(exact.Success);
        Assert.True(fast.Success);
        Assert.Equal("iFUB", fast.Value!["Algorithm"]);
        Assert.Equal((int)exact.Value!["Diameter"], (int)fast.Value!["Diameter"]);
        Assert.Equal((int)exact.Value!["ComponentSize"], (int)fast.Value!["ComponentSize"]);
    }

    [Fact]
    public void Diameter_DiameterOnly_MaxThreadsOneVsMany_ProduceSameValues()
    {
        // iFUB's own per-level parallel BFS pass needs the same sequential-vs-parallel
        // equivalence check as the exhaustive method already has (Diameter_MaxThreadsOneVsMany_
        // ProduceSameValues).
        if (Environment.ProcessorCount < 2)
            return;

        try
        {
            var netSeq = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", 1);
            var seqResult = Analyses.Diameter(netSeq, "layer", diameterOnly: true);

            var netPar = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", Math.Min(4, Environment.ProcessorCount));
            var parResult = Analyses.Diameter(netPar, "layer", diameterOnly: true);

            Assert.True(seqResult.Success);
            Assert.True(parResult.Success);
            Assert.Equal("iFUB", seqResult.Value!["Algorithm"]);
            Assert.Equal("iFUB", parResult.Value!["Algorithm"]);
            Assert.Equal((int)seqResult.Value!["Diameter"], (int)parResult.Value!["Diameter"]);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void Diameter_MaxThreadsOneVsMany_ProduceSameValues()
    {
        if (Environment.ProcessorCount < 2)
            return; // nothing extra to exercise on a single-core runner

        try
        {
            var netSeq = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", 1);
            var seqResult = Analyses.Diameter(netSeq, "layer");

            var netPar = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", Math.Min(4, Environment.ProcessorCount));
            var parResult = Analyses.Diameter(netPar, "layer");

            Assert.True(seqResult.Success);
            Assert.True(parResult.Success);
            Assert.Equal((int)seqResult.Value!["Diameter"], (int)parResult.Value!["Diameter"]);
            Assert.Equal((double)seqResult.Value!["AvgDistance"], (double)parResult.Value!["AvgDistance"], precision: 10);
            Assert.Equal((long)seqResult.Value!["PairsConsidered"], (long)parResult.Value!["PairsConsidered"]);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void Diameter_MaxThreadsGreaterThanOne_Succeeds()
    {
        if (Environment.ProcessorCount < 2)
            return;

        try
        {
            UserSettings.Set("maxthreads", 2);
            var net = MakeRingWithChordsNetwork(15);
            var result = Analyses.Diameter(net, "layer");
            Assert.True(result.Success);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    // ── SmallWorldCoefficient ────────────────────────────────────────────────────
    // The random reference is stochastic (independent rewire() trials), so these tests
    // check mechanical correctness (observed values, field shapes, no side effects) rather
    // than asserting a specific qualitative "this network is small-world" outcome.

    [Fact]
    public void SmallWorldCoefficient_ObservedAvgDistanceMatchesDiameter()
    {
        var net = MakeRingWithChordsNetwork(15);

        var swResult = Analyses.SmallWorldCoefficient(net, "layer", nullTrials: 1);
        var diameterResult = Analyses.Diameter(net, "layer");

        Assert.True(swResult.Success, swResult.Message);
        Assert.True(diameterResult.Success);
        Assert.Equal((double)diameterResult.Value!["AvgDistance"], (double)swResult.Value!["ObservedAvgDistance"], precision: 10);
    }

    [Fact]
    public void SmallWorldCoefficient_ObservedClusteringMatchesClusteringCoefficient()
    {
        var net = MakeRingWithChordsNetwork(15);

        var swResult = Analyses.SmallWorldCoefficient(net, "layer", nullTrials: 1);
        Assert.True(swResult.Success, swResult.Message);

        var ccResult = Analyses.ClusteringCoefficient(net, new[] { "layer" }, "cc_check");
        Assert.True(ccResult.Success);
        double expectedClustering = net.Nodeset.NodeIdArray.Select(id =>
        {
            var attr = net.Nodeset.GetNodeAttribute(id, "cc_check");
            return (double)(float)attr.Value.Value.GetValue(attr.Value.Type)!;
        }).Average();

        // Loose tolerance: the independently-computed value round-trips through a float
        // node attribute (string -> float), while SmallWorldCoefficient's own value does not.
        Assert.Equal(expectedClustering, (double)swResult.Value!["ObservedClustering"], precision: 4);
    }

    [Fact]
    public void SmallWorldCoefficient_ReturnsAllExpectedFields()
    {
        var net = MakeRingWithChordsNetwork(15);

        var result = Analyses.SmallWorldCoefficient(net, "layer", nullTrials: 3);

        Assert.True(result.Success, result.Message);
        Assert.True(result.Value!.ContainsKey("Sigma"));
        Assert.True(result.Value!.ContainsKey("ObservedClustering"));
        Assert.True(result.Value!.ContainsKey("ObservedAvgDistance"));
        Assert.True(result.Value!.ContainsKey("RandomClustering"));
        Assert.True(result.Value!.ContainsKey("RandomAvgDistance"));
        Assert.Equal(3, (int)result.Value!["NullTrials"]);
        Assert.True((double)result.Value!["RandomClustering"] >= 0.0);
        Assert.True((double)result.Value!["RandomAvgDistance"] >= 0.0);
    }

    [Fact]
    public void SmallWorldCoefficient_DoesNotModifyNetwork()
    {
        var net = MakeRingWithChordsNetwork(15);
        int layerCountBefore = net.Layers.Count;
        ulong edgeCountBefore = ((ILayerOneMode)net.Layers["layer"]).NbrEdges;

        var result = Analyses.SmallWorldCoefficient(net, "layer", nullTrials: 3);

        Assert.True(result.Success, result.Message);
        Assert.Equal(layerCountBefore, net.Layers.Count);
        Assert.Equal(edgeCountBefore, ((ILayerOneMode)net.Layers["layer"]).NbrEdges);
    }

    [Fact]
    public void SmallWorldCoefficient_NonExistentLayer_Fails()
    {
        var net = MakeNetwork(3);
        var result = Analyses.SmallWorldCoefficient(net, "ghost");
        Assert.False(result.Success);
    }

    [Fact]
    public void SmallWorldCoefficient_TwoModeLayer_Fails()
    {
        var net = MakeNetwork(3);
        net.AddLayerTwoMode("clubs");

        var result = Analyses.SmallWorldCoefficient(net, "clubs");

        Assert.False(result.Success);
    }

    [Fact]
    public void SmallWorldCoefficient_NullTrialsLessThanOne_Fails()
    {
        var net = MakeRingWithChordsNetwork(10);
        var result = Analyses.SmallWorldCoefficient(net, "layer", nullTrials: 0);
        Assert.False(result.Success);
    }

    // ── ClusteringCoefficient ────────────────────────────────────────────────────
    // Each node's coefficient is computed independently (LocalStructureFunctions.
    // ComputePerNodeParallel); tests below cover the Watts-Strogatz formula's math plus
    // that all four formulas merge to the same result under parallel execution.

    [Fact]
    public void ClusteringCoefficient_TriangleWithPendant_ComputesExpectedValues()
    {
        // Triangle 1-2-3, plus pendant 4 attached only to node 1.
        // Node 1: neighbors {2,3,4}; only pair (2,3) is connected → C = 1/3.
        // Node 2: neighbors {1,3}; pair (1,3) is connected → C = 1.0.
        // Node 3: neighbors {1,2}; pair (1,2) is connected → C = 1.0.
        // Node 4: only 1 neighbor → C = 0.0.
        var net = MakeNetwork(4);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 2, 3);
        net.AddEdge("layer", 1, 4);

        var result = Analyses.ClusteringCoefficient(net, new[] { "layer" }, "cc", ClusteringMethod.WattsStrogatz);

        Assert.True(result.Success);
        Assert.Equal(1.0 / 3.0, GetFloatAttr(net, 1, "cc"), precision: 5);
        Assert.Equal(1.0, GetFloatAttr(net, 2, "cc"), precision: 5);
        Assert.Equal(1.0, GetFloatAttr(net, 3, "cc"), precision: 5);
        Assert.Equal(0.0, GetFloatAttr(net, 4, "cc"), precision: 5);
    }

    [Fact]
    public void ClusteringCoefficient_NonExistentLayer_Fails()
    {
        var net = MakeNetwork(3);
        var result = Analyses.ClusteringCoefficient(net, new[] { "ghost" });
        Assert.False(result.Success);
    }

    [Fact]
    public void ClusteringCoefficient_OnnelaOnLayerAllowingSelfties_FailsEvenWithoutAnySelfLoopEdge()
    {
        // Onnela normalizes by the global maximum edge weight, which does not currently scan
        // self-tie values - so the method is refused for any layer where selfties are merely
        // allowed, regardless of whether one is actually present.
        var net = MakeNetwork(4);
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Valued, true);
        net.AddEdge("layer", 1, 2, 2.0f);

        var result = Analyses.ClusteringCoefficient(net, new[] { "layer" }, "cc", ClusteringMethod.Onnela);
        Assert.False(result.Success);
        Assert.Equal("LayerAllowsSelfties", result.Code);
    }

    [Fact]
    public void ClusteringCoefficient_OnnelaOnLayerWithoutSelfties_StillSucceeds()
    {
        var net = MakeNetwork(4);
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("layer", 1, 2, 2.0f);
        net.AddEdge("layer", 1, 3, 3.0f);
        net.AddEdge("layer", 2, 3, 1.0f);

        var result = Analyses.ClusteringCoefficient(net, new[] { "layer" }, "cc", ClusteringMethod.Onnela);
        Assert.True(result.Success);
    }

    [Fact]
    public void ClusteringCoefficient_BarratOnLayerAllowingSelfties_StillSucceeds()
    {
        // Only Onnela is refused for selfties - Barrat doesn't rely on the affected
        // max-edge-weight scan, so it should be unaffected.
        var net = MakeNetwork(4);
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Valued, true);
        net.AddEdge("layer", 1, 2, 2.0f);

        var result = Analyses.ClusteringCoefficient(net, new[] { "layer" }, "cc", ClusteringMethod.Barrat);
        Assert.True(result.Success);
    }

    [Theory]
    [InlineData(ClusteringMethod.WattsStrogatz)]
    [InlineData(ClusteringMethod.Fagiolo)]
    [InlineData(ClusteringMethod.Barrat)]
    [InlineData(ClusteringMethod.Onnela)]
    public void ClusteringCoefficient_MaxThreadsOneVsMany_ProduceSameValues(ClusteringMethod method)
    {
        if (Environment.ProcessorCount < 2)
            return; // nothing extra to exercise on a single-core runner

        try
        {
            var netSeq = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", 1);
            Analyses.ClusteringCoefficient(netSeq, new[] { "layer" }, "cc", method);

            var netPar = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", Math.Min(4, Environment.ProcessorCount));
            Analyses.ClusteringCoefficient(netPar, new[] { "layer" }, "cc", method);

            for (uint id = 1; id <= 20; id++)
                Assert.Equal(GetFloatAttr(netSeq, id, "cc"), GetFloatAttr(netPar, id, "cc"), precision: 5);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void ClusteringCoefficient_MaxThreadsGreaterThanOne_Succeeds()
    {
        if (Environment.ProcessorCount < 2)
            return;

        try
        {
            UserSettings.Set("maxthreads", 2);
            var net = MakeRingWithChordsNetwork(15);
            var result = Analyses.ClusteringCoefficient(net, new[] { "layer" }, "cc", ClusteringMethod.WattsStrogatz);
            Assert.True(result.Success);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    // ── Assortativity ────────────────────────────────────────────────────────────
    // PearsonEdgeCorrelation (shared by DegreeAssortativity and ContinuousAssortativity)
    // must symmetrize undirected edges - counting each edge from both endpoints - to match
    // Newman's formula; GetAllEgoData only yields each undirected edge once (lower-id side).

    [Fact]
    public void DegreeAssortativity_StarGraph_MatchesKnownExactValue()
    {
        // Hub (node 1, degree 4) connected to four degree-1 leaves: a pure star graph has the
        // textbook assortativity r = -1.0 (perfect negative degree correlation).
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 1, 4);
        net.AddEdge("layer", 1, 5);

        var result = Analyses.DegreeAssortativity(net, "layer");

        Assert.True(result.Success);
        Assert.Equal(-1.0, result.Value, precision: 6);
    }

    [Fact]
    public void Assortativity_ContinuousAttributeEqualToDegree_StarGraph_MatchesDegreeAssortativity()
    {
        // A Float attribute set to each node's own degree must reproduce the same -1.0 result
        // as DegreeAssortativity on the same star graph, since both reduce to the identical
        // Pearson correlation of (degree, degree) across edges.
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 1, 4);
        net.AddEdge("layer", 1, 5);
        net.Nodeset.DefineNodeAttribute("deg", "float");
        net.Nodeset.SetNodeAttribute(1, "deg", "4");
        net.Nodeset.SetNodeAttribute(2, "deg", "1");
        net.Nodeset.SetNodeAttribute(3, "deg", "1");
        net.Nodeset.SetNodeAttribute(4, "deg", "1");
        net.Nodeset.SetNodeAttribute(5, "deg", "1");

        var result = Analyses.Assortativity(net, "layer", "deg");

        Assert.True(result.Success);
        Assert.Equal(-1.0, result.Value, precision: 5);
    }

    /// <summary>
    /// Builds a small 2-mode network (nodes 1-6) where node pair (2,3) shares two hyperedges
    /// (c1 and c2), so the 'binary' and 'count' projection methods disagree on how that one pair
    /// is weighted - the case this is designed to exercise.
    /// </summary>
    private static Network MakeAssortativityTwoModeNetwork(string layerName = "aff")
    {
        var net = MakeNetwork(6);
        net.AddLayerTwoMode(layerName);
        net.AddHyperedge(layerName, "c1", new uint[] { 1, 2, 3 });
        net.AddHyperedge(layerName, "c2", new uint[] { 2, 3, 4 });
        net.AddHyperedge(layerName, "c3", new uint[] { 4, 5, 6 });
        return net;
    }

    [Fact]
    public void DegreeAssortativity_TwoModeLayer_Binary_MatchesOneModeProjection()
    {
        // 'binary' pair-counting on the pseudo-projected 2-mode layer must match running
        // DegreeAssortativity on the materialized binary projection of the same hyperedges,
        // since both use the same (distinct-alter) degree and the same one-pair-per-dedup-edge
        // weighting.
        var net = MakeAssortativityTwoModeNetwork();

        var twoModeResult = Analyses.DegreeAssortativity(net, "aff", ProjectionMethod.Binary);
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "aff", ProjectionMethod.Binary, "aff_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.DegreeAssortativity(net, "aff_proj");
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        Assert.Equal(oneModeResult.Value, twoModeResult.Value, precision: 10);
    }

    [Fact]
    public void DegreeAssortativity_TwoModeLayer_Count_WeightsRepeatedCoaffiliationMoreHeavily()
    {
        // Hand-computed expectation: degrees are always the binary projected degree
        // (node: 1->2, 2->3, 3->3, 4->4, 5->2, 6->2), but each co-affiliate pair is weighted by
        // its shared-hyperedge count (so pair (2,3), sharing c1 and c2, counts twice). Working
        // through Newman's Pearson-over-edges formula with that weighting gives r = -40/176 =
        // -5/22, versus -3/13 for 'binary' (every pair weighted 1, regardless of how many
        // hyperedges it shares) - two distinct values, confirming 'projection' actually changes
        // the outcome, even though they happen to be numerically close for this small network.
        var net = MakeAssortativityTwoModeNetwork();

        var countResult = Analyses.DegreeAssortativity(net, "aff", ProjectionMethod.Count);
        var binaryResult = Analyses.DegreeAssortativity(net, "aff", ProjectionMethod.Binary);

        Assert.True(countResult.Success, countResult.Message);
        Assert.True(binaryResult.Success, binaryResult.Message);
        Assert.Equal(-40.0 / 176.0, countResult.Value, precision: 10);
        Assert.Equal(-3.0 / 13.0, binaryResult.Value, precision: 10);
        Assert.NotEqual(binaryResult.Value, countResult.Value, precision: 10);
    }

    [Fact]
    public void Assortativity_TwoModeLayer_ContinuousBinary_MatchesOneModeProjection()
    {
        // Same equivalence check as DegreeAssortativity, but routed through the generic
        // (Float attribute) continuous-assortativity path rather than the degree-specific one.
        var net = MakeAssortativityTwoModeNetwork();
        net.Nodeset.DefineNodeAttribute("val", "float");
        for (uint id = 1; id <= 6; id++)
            net.Nodeset.SetNodeAttribute(id, "val", (id * 1.5).ToString());

        var twoModeResult = Analyses.Assortativity(net, "aff", "val", ProjectionMethod.Binary);
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "aff", ProjectionMethod.Binary, "aff_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.Assortativity(net, "aff_proj", "val");
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        Assert.Equal(oneModeResult.Value, twoModeResult.Value, precision: 10);
    }

    [Fact]
    public void Assortativity_TwoModeLayer_ContinuousCount_NoMissingValues_MatchesHandComputedValue()
    {
        // Hand-computed expectation for the Count-mode closed-form per-hyperedge reduction.
        // c1={1,2,3} vals={1.5,3,4.5}: S=9,    Q=31.5   -> sumJK+=49.5,  sumJ/K+=18,   sumJ2/K2+=63,    M+=6
        // c2={2,3,4} vals={3,4.5,6}:   S=13.5, Q=65.25  -> sumJK+=117,   sumJ/K+=27,   sumJ2/K2+=130.5, M+=6
        // c3={4,5,6} vals={6,7.5,9}:   S=22.5, Q=173.25 -> sumJK+=333,   sumJ/K+=45,   sumJ2/K2+=346.5, M+=6
        // Totals: sumJK=499.5, sumJ=sumK=90, sumJ2=sumK2=540, M=18.
        // r = (M*sumJK - sumJ*sumK) / (M*sumJ2 - sumJ^2) = (18*499.5 - 90*90) / (18*540 - 90*90)
        //   = (8991 - 8100) / (9720 - 8100) = 891/1620 = 11/20 = 0.55 exactly.
        var net = MakeAssortativityTwoModeNetwork();
        net.Nodeset.DefineNodeAttribute("val", "float");
        for (uint id = 1; id <= 6; id++)
            net.Nodeset.SetNodeAttribute(id, "val", (id * 1.5).ToString());

        var twoModeResult = Analyses.Assortativity(net, "aff", "val", ProjectionMethod.Count);
        Assert.True(twoModeResult.Success, twoModeResult.Message);
        Assert.Equal(11.0 / 20.0, twoModeResult.Value, precision: 10);

        // Deliberately NOT expected to equal assortativity() run on the materialized
        // Count-projection: PearsonEdgeCorrelation (the 1-mode baseline) ignores edge weight
        // entirely - it treats every existing edge as one unweighted sample, regardless of its
        // projected weight. So projecting with Count (pair (2,3) -> weight 2, from sharing both
        // c1 and c2) and then calling assortativity() on that projection computes the
        // *unweighted* correlation over the same 8 distinct edges, not one weighted by
        // shared-hyperedge count - a different, equally valid, but genuinely different quantity:
        // sumJK=472.5, sumJ=sumK=82.5, sumJ2=sumK2=510.75, M=16 (same formula, weight=1 per
        // edge), giving r = (16*472.5 - 82.5^2) / (16*510.75 - 82.5^2) = 753.75/1365.75 =
        // 335/607 (~0.5518945634). Asserting both values, and that they differ, documents this
        // non-equivalence explicitly so it isn't mistaken for a regression again.
        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "aff", ProjectionMethod.Count, "aff_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.Assortativity(net, "aff_proj", "val");
        Assert.True(oneModeResult.Success, oneModeResult.Message);
        Assert.Equal(335.0 / 607.0, oneModeResult.Value, precision: 10);
        Assert.NotEqual(oneModeResult.Value, twoModeResult.Value);
    }

    [Fact]
    public void Assortativity_TwoModeLayer_ContinuousCount_WithMissingAttributeValue_MatchesHandComputation()
    {
        // Stress-tests the Count-mode closed form's per-hyperedge missing-value filtering -
        // the one real correctness risk in that optimization (degree assortativity never has
        // a missing value, so that existing coverage can't exercise this). Node 3 is left
        // without a 'val' attribute; hand-computed (and cross-checked by hand against what the
        // old per-pair enumeration would accumulate) expectation is r = 159/269.
        //
        // Values: 1->10, 2->20, 3->(missing), 4->40, 5->50, 6->60. Every pair touching node 3
        // (shared via c1 and/or c2) contributes nothing; the remaining weighted pairs are
        // {1,2} w=1, {2,4} w=1, {4,5} w=1, {4,6} w=1, {5,6} w=1 (all weight 1 - no pair other
        // than {2,3}, which is excluded anyway, shares more than one hyperedge here).
        var net = MakeAssortativityTwoModeNetwork();
        net.Nodeset.DefineNodeAttribute("val", "float");
        net.Nodeset.SetNodeAttribute(1, "val", "10");
        net.Nodeset.SetNodeAttribute(2, "val", "20");
        // node 3 intentionally left unset
        net.Nodeset.SetNodeAttribute(4, "val", "40");
        net.Nodeset.SetNodeAttribute(5, "val", "50");
        net.Nodeset.SetNodeAttribute(6, "val", "60");

        var result = Analyses.Assortativity(net, "aff", "val", ProjectionMethod.Count);

        Assert.True(result.Success, result.Message);
        Assert.Equal(159.0 / 269.0, result.Value, precision: 10);
    }

    [Fact]
    public void Assortativity_TwoModeLayer_CategoricalBinary_MatchesOneModeProjection()
    {
        // Same equivalence check, routed through the categorical (nominal mixing) path: nodes
        // 1-3 and 5-6 get category 'a', node 4 gets 'b', so category match/mismatch across the
        // projected edges is non-trivial on both sides.
        var net = MakeAssortativityTwoModeNetwork();
        net.Nodeset.DefineNodeAttribute("grp", "char");
        foreach (uint id in new uint[] { 1, 2, 3, 5, 6 })
            net.Nodeset.SetNodeAttribute(id, "grp", "a");
        net.Nodeset.SetNodeAttribute(4, "grp", "b");

        var twoModeResult = Analyses.Assortativity(net, "aff", "grp", ProjectionMethod.Binary);
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "aff", ProjectionMethod.Binary, "aff_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.Assortativity(net, "aff_proj", "grp");
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        Assert.Equal(oneModeResult.Value, twoModeResult.Value, precision: 10);
    }

    // ── Constraint / EffectiveSize (Burt's structural holes) ───────────────────────
    // StructuralHoles' per-node O(k²) computation is independent across nodes once the
    // symmetrized weight/strength tables are built; tests below cover both the math and
    // that parallel execution merges to the same result as sequential.

    [Fact]
    public void Constraint_StarGraph_CenterLowLeavesHigh()
    {
        // Center (1) connects to 4 mutually-unconnected leaves: no redundancy among ties.
        // Center: C = k * (1/k)^2 = 1/4 = 0.25. Leaves: single contact → C = 1.0.
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 1, 4);
        net.AddEdge("layer", 1, 5);

        var result = Analyses.Constraint(net, new[] { "layer" }, "con");

        Assert.True(result.Success);
        Assert.Equal(0.25, GetFloatAttr(net, 1, "con"), precision: 5);
        Assert.Equal(1.0, GetFloatAttr(net, 2, "con"), precision: 5);
    }

    [Fact]
    public void EffectiveSize_StarGraph_CenterEqualsDegreeLeavesEqualOne()
    {
        // No redundancy among center's 4 unconnected leaves → effective size = k = 4.
        // Leaves have a single contact → effective size = 1.
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);
        net.AddEdge("layer", 1, 4);
        net.AddEdge("layer", 1, 5);

        var result = Analyses.EffectiveSize(net, new[] { "layer" }, "es");

        Assert.True(result.Success);
        Assert.Equal(4.0, GetFloatAttr(net, 1, "es"), precision: 5);
        Assert.Equal(1.0, GetFloatAttr(net, 2, "es"), precision: 5);
    }

    [Fact]
    public void Constraint_NonExistentLayer_Fails()
    {
        var net = MakeNetwork(3);
        var result = Analyses.Constraint(net, new[] { "ghost" });
        Assert.False(result.Success);
    }

    [Fact]
    public void Constraint_LayerAllowsSelfties_FailsEvenWithoutAnySelfLoopEdge()
    {
        // Burt's constraint has no defined treatment of a self-tie, so a layer where selfties
        // are merely allowed is refused outright - the check is on the layer's type, not its data.
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer", selfties: true);
        net.AddEdge("layer", 1, 2);

        var result = Analyses.Constraint(net, new[] { "layer" }, "con");
        Assert.False(result.Success);
        Assert.Equal("LayerAllowsSelfties", result.Code);
    }

    [Fact]
    public void Constraint_AllLayers_RefusesIfAnyAllowsSelfties()
    {
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer", selfties: true);
        net.AddEdge("layer", 1, 2);

        var result = Analyses.Constraint(net, null, "con");
        Assert.False(result.Success);
        Assert.Equal("LayerAllowsSelfties", result.Code);
    }

    [Fact]
    public void EffectiveSize_LayerAllowsSelfties_FailsEvenWithoutAnySelfLoopEdge()
    {
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer", selfties: true);
        net.AddEdge("layer", 1, 2);

        var result = Analyses.EffectiveSize(net, new[] { "layer" }, "es");
        Assert.False(result.Success);
        Assert.Equal("LayerAllowsSelfties", result.Code);
    }

    [Fact]
    public void EffectiveSize_AllLayers_RefusesIfAnyAllowsSelfties()
    {
        var net = MakeNetwork(5);
        AddUndirectedLayer(net, "layer", selfties: true);
        net.AddEdge("layer", 1, 2);

        var result = Analyses.EffectiveSize(net, null, "es");
        Assert.False(result.Success);
        Assert.Equal("LayerAllowsSelfties", result.Code);
    }

    [Fact]
    public void Constraint_TwoModeLayer_WithoutSampleSize_Fails()
    {
        var net = MakeAssortativityTwoModeNetwork();
        var result = Analyses.Constraint(net, new[] { "aff" }, "con");
        Assert.False(result.Success);
        Assert.Equal("SampleSizeRequired", result.Code);
    }

    [Fact]
    public void EffectiveSize_TwoModeLayer_WithoutSampleSize_Fails()
    {
        var net = MakeAssortativityTwoModeNetwork();
        var result = Analyses.EffectiveSize(net, new[] { "aff" }, "es");
        Assert.False(result.Success);
        Assert.Equal("SampleSizeRequired", result.Code);
    }

    [Fact]
    public void Constraint_TwoModeLayerCombinedWithOneModeLayer_Fails()
    {
        var net = MakeAssortativityTwoModeNetwork();
        AddUndirectedLayer(net, "other");
        net.AddEdge("other", 1, 2);

        var result = Analyses.Constraint(net, new[] { "aff", "other" }, "con", sampleSize: 6);
        Assert.False(result.Success);
        Assert.Equal("InvalidLayerType", result.Code);
    }

    [Fact]
    public void Constraint_TwoModeLayer_FullSample_MatchesCountProjection()
    {
        // With samplesize covering every node, the pseudo-projected (2-mode) result for each
        // node must equal Constraint on the materialized count projection of the same
        // hyperedges, since 2-mode tie strength is defined as the count-weighted
        // shared-hyperedge count.
        var net = MakeAssortativityTwoModeNetwork();

        var twoModeResult = Analyses.Constraint(net, new[] { "aff" }, "con_2mode", sampleSize: 6);
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "aff", ProjectionMethod.Count, "aff_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.Constraint(net, new[] { "aff_proj" }, "con_proj");
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        for (uint id = 1; id <= 6; id++)
            Assert.Equal(GetFloatAttr(net, id, "con_proj"), GetFloatAttr(net, id, "con_2mode"), precision: 5);
    }

    [Fact]
    public void EffectiveSize_TwoModeLayer_FullSample_MatchesCountProjection()
    {
        var net = MakeAssortativityTwoModeNetwork();

        var twoModeResult = Analyses.EffectiveSize(net, new[] { "aff" }, "es_2mode", sampleSize: 6);
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "aff", ProjectionMethod.Count, "aff_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Analyses.EffectiveSize(net, new[] { "aff_proj" }, "es_proj");
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        for (uint id = 1; id <= 6; id++)
            Assert.Equal(GetFloatAttr(net, id, "es_proj"), GetFloatAttr(net, id, "es_2mode"), precision: 5);
    }

    [Fact]
    public void Constraint_TwoModeLayer_PartialSample_OnlySetsThatManySampledNodes()
    {
        var net = MakeAssortativityTwoModeNetwork();
        var result = Analyses.Constraint(net, new[] { "aff" }, "con_partial", sampleSize: 2);
        Assert.True(result.Success, result.Message);

        int nbrSet = 0;
        for (uint id = 1; id <= 6; id++)
            if (net.Nodeset.GetNodeAttribute(id, "con_partial").Success)
                nbrSet++;
        Assert.Equal(2, nbrSet);
    }

    [Fact]
    public void ConstraintEffectiveSize_MaxThreadsOneVsMany_ProduceSameValues()
    {
        if (Environment.ProcessorCount < 2)
            return; // nothing extra to exercise on a single-core runner

        try
        {
            var netSeq = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", 1);
            Analyses.Constraint(netSeq, new[] { "layer" }, "con");
            Analyses.EffectiveSize(netSeq, new[] { "layer" }, "es");

            var netPar = MakeRingWithChordsNetwork(20);
            UserSettings.Set("maxthreads", Math.Min(4, Environment.ProcessorCount));
            Analyses.Constraint(netPar, new[] { "layer" }, "con");
            Analyses.EffectiveSize(netPar, new[] { "layer" }, "es");

            for (uint id = 1; id <= 20; id++)
            {
                Assert.Equal(GetFloatAttr(netSeq, id, "con"), GetFloatAttr(netPar, id, "con"), precision: 5);
                Assert.Equal(GetFloatAttr(netSeq, id, "es"), GetFloatAttr(netPar, id, "es"), precision: 5);
            }
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void Constraint_MaxThreadsGreaterThanOne_Succeeds()
    {
        if (Environment.ProcessorCount < 2)
            return;

        try
        {
            UserSettings.Set("maxthreads", 2);
            var net = MakeRingWithChordsNetwork(15);
            var result = Analyses.Constraint(net, new[] { "layer" }, "con");
            Assert.True(result.Success);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }
}