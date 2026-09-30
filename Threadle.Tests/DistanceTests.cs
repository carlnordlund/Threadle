using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
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

}