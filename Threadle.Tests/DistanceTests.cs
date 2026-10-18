using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// Tests for GetRandomAlter (multilayer extension) and RandomWalkNodeAttributeDistances.
/// </summary>
public class DistanceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a network with nodes 1..n and no layers.</summary>
    private static Network MakeNetwork(int n)
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= n; i++)
            ns.AddNode(i);
        return new Network("net", ns);
    }

    /// <summary>Adds an undirected binary layer and returns the network.</summary>
    private static Network AddUndirected(Network net, string name)
    {
        net.AddLayerOneMode(name, EdgeDirectionality.Undirected, EdgeType.Binary, false);
        return net;
    }

    /// <summary>Adds a directed binary layer and returns the network.</summary>
    private static Network AddDirected(Network net, string name)
    {
        net.AddLayerOneMode(name, EdgeDirectionality.Directed, EdgeType.Binary, false);
        return net;
    }

    /// <summary>Adds a directed valued layer and returns the network.</summary>
    private static Network AddDirectedValued(Network net, string name)
    {
        net.AddLayerOneMode(name, EdgeDirectionality.Directed, EdgeType.Valued, false);
        return net;
    }

    /// <summary>Builds a complete undirected graph on n nodes (ids 1..n).</summary>
    private static Network MakeCompleteNetwork(int n, string layerName = "layer")
    {
        var net = MakeNetwork(n);
        AddUndirected(net, layerName);
        for (uint i = 1; i <= n; i++)
            for (uint j = i + 1; j <= n; j++)
                net.AddEdge(layerName, i, j);
        return net;
    }

    /// <summary>Assigns a char attribute: nodes 1..splitAt get 'a', the rest get 'b'.</summary>
    private static void AssignTwoGroupCharAttr(Network net, string attrName, int splitAt)
    {
        net.Nodeset.DefineNodeAttribute(attrName, "char");
        foreach (uint id in net.Nodeset.NodeIdArray)
            net.Nodeset.SetNodeAttribute(id, attrName, id <= splitAt ? "a" : "b");
    }

    /// <summary>Returns the avgdistance layer from a StructureResult.</summary>
    private static ILayerOneMode GetAvgLayer(StructureResult sr, string attrName)
    {
        var resultNet = (Network)sr.MainStructure;
        Assert.True(resultNet.Layers.ContainsKey(attrName + "_avgdistance"), "avgdistance layer missing");
        return (ILayerOneMode)resultNet.Layers[attrName + "_avgdistance"];
    }

    /// <summary>Returns the result nodeset from a StructureResult.</summary>
    private static Nodeset GetResultNodeset(StructureResult sr) =>
        (Nodeset)sr.AdditionalStructures["nodeset"];

    // ── GetRandomAlter — multilayer extension ─────────────────────────────────

    [Fact]
    public void GetRandomAlter_SingleLayerArray_ReturnsValidAlter()
    {
        var net = MakeNetwork(5);
        AddUndirected(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 1, 3);

        var result = Analyses.GetRandomAlter(net, 1, ["layer"]);

        Assert.True(result.Success);
        Assert.Contains(result.Value, new uint[] { 2, 3 });
    }

    [Fact]
    public void GetRandomAlter_NullLayers_UsesAllLayers()
    {
        // Node 1 has alter 2 in "friends" and alter 3 in "cowork".
        // With null both layers are searched so both alters should be reachable.
        var net = MakeNetwork(4);
        AddUndirected(net, "friends");
        AddUndirected(net, "cowork");
        net.AddEdge("friends", 1, 2);
        net.AddEdge("cowork", 1, 3);

        bool got2 = false, got3 = false;
        for (int i = 0; i < 200; i++)
        {
            var r = Analyses.GetRandomAlter(net, 1, null);
            Assert.True(r.Success);
            if (r.Value == 2) got2 = true;
            if (r.Value == 3) got3 = true;
            if (got2 && got3) break;
        }
        Assert.True(got2, "Should reach alter 2 via 'friends'");
        Assert.True(got3, "Should reach alter 3 via 'cowork'");
    }

    [Fact]
    public void GetRandomAlter_UnknownLayerName_Fails()
    {
        var net = MakeNetwork(3);
        AddUndirected(net, "layer");
        net.AddEdge("layer", 1, 2);

        var result = Analyses.GetRandomAlter(net, 1, ["nonexistent"]);

        Assert.False(result.Success);
    }

    [Fact]
    public void GetRandomAlter_NodeHasNoAlters_Fails()
    {
        var net = MakeNetwork(3);
        AddUndirected(net, "layer");
        // No edges — node 1 has no alters

        var result = Analyses.GetRandomAlter(net, 1, null);

        Assert.False(result.Success);
        Assert.Equal("ConstraintNoAlters", result.Code);
    }

    [Fact]
    public void GetRandomAlter_BalancedFalse_SharedAlterPickedMoreOften()
    {
        // Node 1 has alter 2 in both layers, alter 3 only in layerB.
        // With balanced=false (pool), alter 2 is in the pool twice → ~2× as likely as alter 3.
        var net = MakeNetwork(4);
        AddUndirected(net, "layerA");
        AddUndirected(net, "layerB");
        net.AddEdge("layerA", 1, 2);
        net.AddEdge("layerB", 1, 2);
        net.AddEdge("layerB", 1, 3);

        int count2 = 0, count3 = 0;
        for (int i = 0; i < 3000; i++)
        {
            var r = Analyses.GetRandomAlter(net, 1, null, EdgeTraversal.Both, balanced: false);
            Assert.True(r.Success);
            if (r.Value == 2) count2++;
            else if (r.Value == 3) count3++;
        }
        // Expect alter 2 roughly twice as frequent as alter 3
        Assert.True(count2 > count3 * 1.4,
            $"Expected alter 2 ~2× more frequent than alter 3, got {count2} vs {count3}");
    }

    [Fact]
    public void GetRandomAlter_BalancedTrue_NodeHasAltersOnlyInOneLayer_Succeeds()
    {
        // Node 1 has alters only in layerB. Balanced=true should filter out layerA
        // (no alters for this node) and successfully pick from layerB.
        var net = MakeNetwork(4);
        AddUndirected(net, "layerA");
        AddUndirected(net, "layerB");
        net.AddEdge("layerB", 1, 2);
        net.AddEdge("layerB", 1, 3);

        var result = Analyses.GetRandomAlter(net, 1, null, EdgeTraversal.Both, balanced: true);

        Assert.True(result.Success);
        Assert.Contains(result.Value, new uint[] { 2, 3 });
    }

    [Fact]
    public void GetRandomAlter_DirectedOut_OnlyOutboundAltersReturned()
    {
        // Node 1 → 2 (outbound), Node 3 → 1 (inbound only for node 1)
        var net = MakeNetwork(4);
        AddDirected(net, "layer");
        net.AddEdge("layer", 1, 2);
        net.AddEdge("layer", 3, 1);

        for (int i = 0; i < 50; i++)
        {
            var r = Analyses.GetRandomAlter(net, 1, ["layer"], EdgeTraversal.Out);
            Assert.True(r.Success);
            Assert.Equal(2u, r.Value);
        }
    }

    [Fact]
    public void GetRandomAlter_Weighted_HighWeightAlterPickedMoreOften()
    {
        // Alter 2: weight 1, Alter 3: weight 9 → alter 3 should be picked ~90% of the time
        var net = MakeNetwork(4);
        AddDirectedValued(net, "layer");
        net.AddEdge("layer", 1, 2, 1f);
        net.AddEdge("layer", 1, 3, 9f);

        int count3 = 0;
        for (int i = 0; i < 1000; i++)
        {
            var r = Analyses.GetRandomAlter(net, 1, ["layer"], EdgeTraversal.Out, weighted: true);
            Assert.True(r.Success);
            if (r.Value == 3) count3++;
        }
        // Accept 75–99% to allow for variance
        Assert.InRange(count3, 750, 999);
    }

    // ── GetRandomAlter — layerweights ──────────────────────────────────────────

    [Fact]
    public void GetRandomAlter_LayerWeights_HighWeightLayerPickedMoreOften()
    {
        // layer1 weight 8, layer2 weight 16: layer2's alter (3) should be picked ~2x as often
        // as layer1's alter (2).
        var net = MakeNetwork(3);
        AddUndirected(net, "layer1");
        AddUndirected(net, "layer2");
        net.AddEdge("layer1", 1, 2);
        net.AddEdge("layer2", 1, 3);

        int count2 = 0, count3 = 0;
        for (int i = 0; i < 3000; i++)
        {
            var r = Analyses.GetRandomAlter(net, 1, ["layer1", "layer2"], EdgeTraversal.Both, balanced: true, layerWeights: [8f, 16f]);
            Assert.True(r.Success);
            if (r.Value == 2) count2++;
            else if (r.Value == 3) count3++;
        }
        Assert.True(count3 > count2 * 1.4,
            $"Expected alter 3 (layer2, weight 16) ~2x more frequent than alter 2 (layer1, weight 8), got {count2} vs {count3}");
    }

    [Fact]
    public void GetRandomAlter_LayerWeights_IneligibleLayerExcludedFromWeighting()
    {
        // Node 1 has no alters in layer2 at all. Despite layer2's much larger weight, the pick
        // must still succeed and come from layer1 (the only eligible layer for this node).
        var net = MakeNetwork(3);
        AddUndirected(net, "layer1");
        AddUndirected(net, "layer2");
        net.AddEdge("layer1", 1, 2);

        var result = Analyses.GetRandomAlter(net, 1, ["layer1", "layer2"], EdgeTraversal.Both, balanced: true, layerWeights: [1f, 100f]);

        Assert.True(result.Success);
        Assert.Equal(2u, result.Value);
    }

    [Fact]
    public void GetRandomAlter_LayerWeights_IgnoredWhenBalancedFalse()
    {
        var net = MakeNetwork(3);
        AddUndirected(net, "layer1");
        AddUndirected(net, "layer2");
        net.AddEdge("layer1", 1, 2);
        net.AddEdge("layer2", 1, 3);

        var result = Analyses.GetRandomAlter(net, 1, ["layer1", "layer2"], EdgeTraversal.Both, balanced: false, layerWeights: [8f, 16f]);

        Assert.True(result.Success);
    }

    [Fact]
    public void GetRandomAlter_LayerWeightsWithoutLayerNames_Fails()
    {
        var net = MakeNetwork(3);
        AddUndirected(net, "layer1");
        net.AddEdge("layer1", 1, 2);

        var result = Analyses.GetRandomAlter(net, 1, null, EdgeTraversal.Both, balanced: true, layerWeights: [1f]);

        Assert.False(result.Success);
    }

    [Fact]
    public void GetRandomAlter_LayerWeightsLengthMismatch_Fails()
    {
        var net = MakeNetwork(3);
        AddUndirected(net, "layer1");
        AddUndirected(net, "layer2");
        net.AddEdge("layer1", 1, 2);
        net.AddEdge("layer2", 1, 3);

        var result = Analyses.GetRandomAlter(net, 1, ["layer1", "layer2"], EdgeTraversal.Both, balanced: true, layerWeights: [1f]);

        Assert.False(result.Success);
    }

    [Fact]
    public void GetRandomAlter_LayerWeightsNonPositive_Fails()
    {
        var net = MakeNetwork(3);
        AddUndirected(net, "layer1");
        AddUndirected(net, "layer2");
        net.AddEdge("layer1", 1, 2);
        net.AddEdge("layer2", 1, 3);

        var result = Analyses.GetRandomAlter(net, 1, ["layer1", "layer2"], EdgeTraversal.Both, balanced: true, layerWeights: [1f, 0f]);

        Assert.False(result.Success);
    }


    // ── RandomWalkNodeAttributeFirstPassageTimeDistances (parallelized initial pass) ─────

    [Fact]
    public void RwFpt_ValidNetworkAndAttr_Succeeds()
    {
        var net = MakeCompleteNetwork(10);
        AssignTwoGroupCharAttr(net, "role", 5);

        var (result, _) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "role", 3, null, 2f, 0, false, false);

        Assert.True(result.Success);
    }

    [Fact]
    public void RwFpt_UnknownAttribute_Fails()
    {
        var net = MakeCompleteNetwork(4);

        var (result, _) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "nonexistent", 2, null, 1f, 0, false, false);

        Assert.False(result.Success);
        Assert.Equal("AttributeUnknown", result.Code);
    }

    [Fact]
    public void RwFpt_FloatAttribute_Fails()
    {
        var net = MakeCompleteNetwork(4);
        net.Nodeset.DefineNodeAttribute("score", "float");

        var (result, _) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "score", 2, null, 1f, 0, false, false);

        Assert.False(result.Success);
        Assert.Equal("InvalidAttributeType", result.Code);
    }

    [Fact]
    public void RwFpt_UnknownLayerName_Fails()
    {
        var net = MakeCompleteNetwork(4);
        AssignTwoGroupCharAttr(net, "role", 2);

        var (result, _) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "role", 2, ["nonexistent"], 1f, 0, false, false);

        Assert.False(result.Success);
        Assert.Equal("LayerNotFound", result.Code);
    }

    [Fact]
    public void RwFpt_ResultNodesetHasOneNodePerUniqueValue()
    {
        var net = MakeCompleteNetwork(12);
        net.Nodeset.DefineNodeAttribute("role", "char");
        foreach (uint id in net.Nodeset.NodeIdArray)
            net.Nodeset.SetNodeAttribute(id, "role", id <= 4 ? "a" : id <= 8 ? "b" : "c");

        var (result, _) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "role", 2, null, 2f, 0, false, false);

        Assert.Equal(3, GetResultNodeset(result.Value!).Count);
    }

    [Fact]
    public void RwFpt_MinPairObsZero_SkipsTargetedRestarts_Succeeds()
    {
        var net = MakeCompleteNetwork(10);
        AssignTwoGroupCharAttr(net, "role", 5);

        var (result, _) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "role", 3, null, 2f, 0, false, false);

        Assert.True(result.Success);
    }

    [Fact]
    public void RwFpt_MinPairObsPositive_TriggersTargetedRestarts_Succeeds()
    {
        // Small, heavily imbalanced groups so the initial pass alone is unlikely to satisfy minPairObs,
        // exercising the (now randomly-sampled) targeted-restart phase.
        var net = MakeCompleteNetwork(12);
        AssignTwoGroupCharAttr(net, "role", 1);

        var (result, _) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "role", 3, null, 1f, 5, false, false);

        Assert.True(result.Success);
    }

    [Fact]
    public void RwFpt_ReturnHistogramsTrue_HistogramsPopulated()
    {
        var net = MakeCompleteNetwork(10);
        AssignTwoGroupCharAttr(net, "role", 5);

        var (result, histograms) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "role", 3, null, 3f, 0, false, false, returnHistograms: true);

        Assert.True(result.Success);
        Assert.NotNull(histograms);
        Assert.NotEmpty(histograms!);
    }

    [Fact]
    public void RwFpt_MaxThreadsOne_Succeeds()
    {
        try
        {
            UserSettings.Set("maxthreads", 1);
            var net = MakeCompleteNetwork(10);
            AssignTwoGroupCharAttr(net, "role", 5);

            var (result, _) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "role", 3, null, 2f, 0, false, false);

            Assert.True(result.Success);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void RwFpt_MaxThreadsGreaterThanOne_Succeeds()
    {
        if (Environment.ProcessorCount < 2)
            return; // nothing extra to exercise on a single-core runner

        try
        {
            UserSettings.Set("maxthreads", 2);
            var net = MakeCompleteNetwork(20);
            AssignTwoGroupCharAttr(net, "role", 10);

            var (result, _) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "role", 3, null, 3f, 0, false, false);

            Assert.True(result.Success);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void RwFpt_LayerWeightsWithoutLayers_Fails()
    {
        var net = MakeCompleteNetwork(6);
        AssignTwoGroupCharAttr(net, "role", 3);

        var (result, _) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "role", 3, null, 2f, 0, true, false, layerWeights: [1f]);

        Assert.False(result.Success);
    }

    [Fact]
    public void RwFpt_LayerWeightsLengthMismatch_Fails()
    {
        var net = MakeNetwork(6);
        AddUndirected(net, "layerA");
        AddUndirected(net, "layerB");
        for (uint i = 1; i <= 6; i++)
            for (uint j = i + 1; j <= 6; j++)
            {
                net.AddEdge("layerA", i, j);
                net.AddEdge("layerB", i, j);
            }
        AssignTwoGroupCharAttr(net, "role", 3);

        var (result, _) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "role", 3, ["layerA", "layerB"], 2f, 0, true, false, layerWeights: [1f]);

        Assert.False(result.Success);
    }

    [Fact]
    public void RwFpt_LayerWeights_Succeeds()
    {
        var net = MakeNetwork(6);
        AddUndirected(net, "layerA");
        AddUndirected(net, "layerB");
        for (uint i = 1; i <= 6; i++)
            for (uint j = i + 1; j <= 6; j++)
            {
                net.AddEdge("layerA", i, j);
                net.AddEdge("layerB", i, j);
            }
        AssignTwoGroupCharAttr(net, "role", 3);

        var (result, _) = Distance.RandomWalkNodeAttributeFirstPassageTimeDistances(net, "role", 3, ["layerA", "layerB"], 2f, 0, true, false, layerWeights: [1f, 3f]);

        Assert.True(result.Success, result.Message);
    }

    // ── ShortestPathsNodeAttributeDistances (parallelized per-source BFS) ────────

    /// <summary>Builds an n-node ring (1-2-...-n-1), undirected binary.</summary>
    private static Network MakeRingNetwork(int n, string layerName = "layer")
    {
        var net = MakeNetwork(n);
        AddUndirected(net, layerName);
        for (int i = 1; i <= n; i++)
        {
            int next = i == n ? 1 : i + 1;
            net.AddEdge(layerName, (uint)i, (uint)next);
        }
        return net;
    }

    /// <summary>Assigns a char attribute based on (nodeId mod modulus), as category labels '0'..'modulus-1'.</summary>
    private static void AssignModuloGroupCharAttr(Network net, string attrName, int modulus)
    {
        net.Nodeset.DefineNodeAttribute(attrName, "char");
        foreach (uint id in net.Nodeset.NodeIdArray)
            net.Nodeset.SetNodeAttribute(id, attrName, (id % (uint)modulus).ToString());
    }

    [Fact]
    public void ShortestPathsNodeAttributeDistances_CompleteGraph_ComputesExpectedMeanAndCount()
    {
        // Complete graph on 4 nodes: every distinct pair is at distance 1. Nodes 1,2 -> 'a'
        // (category node id 0, alphabetically first), nodes 3,4 -> 'b' (category node id 1).
        var net = MakeCompleteNetwork(4);
        AssignTwoGroupCharAttr(net, "grp", 2);

        var result = Distance.ShortestPathsNodeAttributeDistances(net, "grp", null);

        Assert.True(result.Success);
        var resultNet = (Network)result.Value!.MainStructure;
        var avgLayer = (ILayerOneMode)resultNet.Layers["grp_sp_avg"];
        var countLayer = (ILayerOneMode)resultNet.Layers["grp_sp_count"];

        Assert.Equal(1.0f, avgLayer.GetEdgeValue(0, 1), 5);
        Assert.Equal(4f, countLayer.GetEdgeValue(0, 1), 5);
    }

    [Fact]
    public void ShortestPathsNodeAttributeDistances_TwoModeLayer_MatchesOneModeProjection()
    {
        // Pseudo-projected (2-mode) attribute-distance aggregation must equal the same
        // aggregation computed on the materialized binary projection of the same hyperedges.
        var net = MakeNetwork(6);
        AssignTwoGroupCharAttr(net, "grp", 3); // nodes 1-3 -> 'a', nodes 4-6 -> 'b'
        net.AddLayerTwoMode("aff");
        net.AddHyperedge("aff", "c1", new uint[] { 1, 2, 4 });
        net.AddHyperedge("aff", "c2", new uint[] { 2, 3, 5 });
        net.AddHyperedge("aff", "c3", new uint[] { 4, 5, 6 });

        var twoModeResult = Distance.ShortestPathsNodeAttributeDistances(net, "grp", ["aff"]);
        Assert.True(twoModeResult.Success, twoModeResult.Message);

        var projResult = NetworkProcessor.ProjectTwoModeToOneMode(net, "aff", ProjectionMethod.Binary, "aff_proj");
        Assert.True(projResult.Success, projResult.Message);
        var oneModeResult = Distance.ShortestPathsNodeAttributeDistances(net, "grp", ["aff_proj"]);
        Assert.True(oneModeResult.Success, oneModeResult.Message);

        var twoModeNet = (Network)twoModeResult.Value!.MainStructure;
        var oneModeNet = (Network)oneModeResult.Value!.MainStructure;
        var twoModeAvg = (ILayerOneMode)twoModeNet.Layers["grp_sp_avg"];
        var oneModeAvg = (ILayerOneMode)oneModeNet.Layers["grp_sp_avg"];
        var twoModeCount = (ILayerOneMode)twoModeNet.Layers["grp_sp_count"];
        var oneModeCount = (ILayerOneMode)oneModeNet.Layers["grp_sp_count"];

        foreach (uint c1 in new uint[] { 0, 1 })
            foreach (uint c2 in new uint[] { 0, 1 })
            {
                Assert.Equal(oneModeAvg.GetEdgeValue(c1, c2), twoModeAvg.GetEdgeValue(c1, c2), 5);
                Assert.Equal(oneModeCount.GetEdgeValue(c1, c2), twoModeCount.GetEdgeValue(c1, c2), 5);
            }
    }

    [Fact]
    public void ShortestPathsNodeAttributeDistances_NonExistentAttribute_Fails()
    {
        var net = MakeCompleteNetwork(3);
        var result = Distance.ShortestPathsNodeAttributeDistances(net, "nonexistent", null);
        Assert.False(result.Success);
    }

    [Fact]
    public void ShortestPathsNodeAttributeDistances_MaxThreadsOneVsMany_ProduceSameValues()
    {
        if (Environment.ProcessorCount < 2)
            return; // nothing extra to exercise on a single-core runner

        string[] metrics = ["sp_avg", "sp_q1", "sp_median", "sp_q3", "sp_stdev", "sp_se", "sp_count"];

        try
        {
            var netSeq = MakeRingNetwork(20);
            AssignModuloGroupCharAttr(netSeq, "grp", 3);
            UserSettings.Set("maxthreads", 1);
            var seqResult = Distance.ShortestPathsNodeAttributeDistances(netSeq, "grp", null);

            var netPar = MakeRingNetwork(20);
            AssignModuloGroupCharAttr(netPar, "grp", 3);
            UserSettings.Set("maxthreads", Math.Min(4, Environment.ProcessorCount));
            var parResult = Distance.ShortestPathsNodeAttributeDistances(netPar, "grp", null);

            Assert.True(seqResult.Success);
            Assert.True(parResult.Success);

            var seqNet = (Network)seqResult.Value!.MainStructure;
            var parNet = (Network)parResult.Value!.MainStructure;

            foreach (string metric in metrics)
            {
                var seqLayer = (ILayerOneMode)seqNet.Layers["grp_" + metric];
                var parLayer = (ILayerOneMode)parNet.Layers["grp_" + metric];
                for (uint from = 0; from < 3; from++)
                    for (uint to = 0; to < 3; to++)
                        if (from != to)
                            Assert.Equal(seqLayer.GetEdgeValue(from, to), parLayer.GetEdgeValue(from, to), 4);
            }
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }

    [Fact]
    public void ShortestPathsNodeAttributeDistances_MaxThreadsGreaterThanOne_Succeeds()
    {
        if (Environment.ProcessorCount < 2)
            return;

        try
        {
            UserSettings.Set("maxthreads", 2);
            var net = MakeRingNetwork(15);
            AssignModuloGroupCharAttr(net, "grp", 3);
            var result = Distance.ShortestPathsNodeAttributeDistances(net, "grp", null);
            Assert.True(result.Success);
        }
        finally
        {
            UserSettings.MaxDegreeOfParallelism = -1;
        }
    }
}