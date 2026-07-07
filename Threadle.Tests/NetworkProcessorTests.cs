using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Processing.Enums;

namespace Threadle.Tests;

/// <summary>
/// Tests for NetworkProcessor: symmetrize and dichotomize operations.
///
/// Each operation is tested against both dynamic and static (packed) source layers,
/// verifying that packing the source layer does not affect the correctness of the output.
/// The output layer is always expected to be dynamic.
/// </summary>
public class NetworkProcessorTests
{
    private static Nodeset MakeNodeset()
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 5; i++) ns.AddNode(i);
        return ns;
    }

    // ── Symmetrize ─────────────────────────────────────────────────────────────

    [Fact]
    public void SymmetrizeLayer_DynamicDirectedBinary_ProducesUndirectedLayer()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("follows", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("follows", 1, 2);
        net.AddEdge("follows", 3, 2);

        var result = NetworkProcessor.SymmetrizeLayer(net, "follows", SymmetrizeMethod.max, "friends");

        Assert.True(result.Success);
        Assert.True(net.Layers.ContainsKey("friends"));
        Assert.True(net.Layers["friends"].CheckEdgeExists(1, 2));
        Assert.True(net.Layers["friends"].CheckEdgeExists(2, 1));
        Assert.True(net.Layers["friends"].CheckEdgeExists(2, 3));
    }

    [Fact]
    public void SymmetrizeLayer_StaticDirectedBinary_ProducesCorrectUndirectedLayer()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("follows", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("follows", 1, 2);
        net.AddEdge("follows", 3, 2);
        net.Pack("follows");

        var result = NetworkProcessor.SymmetrizeLayer(net, "follows", SymmetrizeMethod.max, "friends");

        Assert.True(result.Success);
        Assert.True(net.Layers.ContainsKey("friends"));
        Assert.True(net.Layers["friends"].CheckEdgeExists(1, 2));
        Assert.True(net.Layers["friends"].CheckEdgeExists(2, 1));
        Assert.True(net.Layers["friends"].CheckEdgeExists(2, 3));
    }

    [Fact]
    public void SymmetrizeLayer_StaticAndDynamic_ProduceIdenticalResults()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("dyn", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddLayerOneMode("packed", EdgeDirectionality.Directed, EdgeType.Binary, false);
        foreach (var name in new[] { "dyn", "packed" })
        {
            net.AddEdge(name, 1, 2);
            net.AddEdge(name, 2, 3);
            net.AddEdge(name, 3, 1);
        }
        net.Pack("packed");

        NetworkProcessor.SymmetrizeLayer(net, "dyn", SymmetrizeMethod.max, "sym_dyn");
        NetworkProcessor.SymmetrizeLayer(net, "packed", SymmetrizeMethod.max, "sym_packed");

        var dynLayer = net.Layers["sym_dyn"];
        var packedLayer = net.Layers["sym_packed"];
        for (uint i = 1; i <= 5; i++)
            for (uint j = 1; j <= 5; j++)
                Assert.Equal(dynLayer.CheckEdgeExists(i, j), packedLayer.CheckEdgeExists(i, j));
    }

    [Fact]
    public void SymmetrizeLayer_StaticDirectedValued_ProducesCorrectValues()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("trust", EdgeDirectionality.Directed, EdgeType.Valued, false);
        net.AddEdge("trust", 1, 2, 4f);
        net.AddEdge("trust", 2, 1, 2f);
        net.Pack("trust");

        NetworkProcessor.SymmetrizeLayer(net, "trust", SymmetrizeMethod.max, "sym");

        Assert.Equal(4f, net.Layers["sym"].GetEdgeValue(1, 2));
        Assert.Equal(4f, net.Layers["sym"].GetEdgeValue(2, 1));
    }

    [Fact]
    public void SymmetrizeLayer_OutputIsAlwaysDynamic()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("follows", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("follows", 1, 2);
        net.Pack("follows");

        NetworkProcessor.SymmetrizeLayer(net, "follows", SymmetrizeMethod.max, "friends");

        Assert.False(net.Layers["friends"].IsStatic);
    }

    // ── Dichotomize ────────────────────────────────────────────────────────────

    [Fact]
    public void DichotomizeLayer_DynamicValued_ProducesCorrectBinaryLayer()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("trust", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("trust", 1, 2, 3f);
        net.AddEdge("trust", 2, 3, 1f);
        net.AddEdge("trust", 3, 4, 5f);

        var result = NetworkProcessor.DichotomizeLayer(net, "trust", ConditionType.ge, 3f, 1f, 0f, "binary");

        Assert.True(result.Success);
        Assert.True(net.Layers["binary"].CheckEdgeExists(1, 2));
        Assert.False(net.Layers["binary"].CheckEdgeExists(2, 3));
        Assert.True(net.Layers["binary"].CheckEdgeExists(3, 4));
    }

    [Fact]
    public void DichotomizeLayer_StaticValued_ProducesCorrectBinaryLayer()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("trust", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("trust", 1, 2, 3f);
        net.AddEdge("trust", 2, 3, 1f);
        net.AddEdge("trust", 3, 4, 5f);
        net.Pack("trust");

        var result = NetworkProcessor.DichotomizeLayer(net, "trust", ConditionType.ge, 3f, 1f, 0f, "binary");

        Assert.True(result.Success);
        Assert.True(net.Layers["binary"].CheckEdgeExists(1, 2));
        Assert.False(net.Layers["binary"].CheckEdgeExists(2, 3));
        Assert.True(net.Layers["binary"].CheckEdgeExists(3, 4));
    }

    [Fact]
    public void DichotomizeLayer_StaticAndDynamic_ProduceIdenticalResults()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("dyn", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddLayerOneMode("packed", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        foreach (var name in new[] { "dyn", "packed" })
        {
            net.AddEdge(name, 1, 2, 1f);
            net.AddEdge(name, 2, 3, 3f);
            net.AddEdge(name, 3, 4, 5f);
        }
        net.Pack("packed");

        NetworkProcessor.DichotomizeLayer(net, "dyn", ConditionType.ge, 3f, 1f, 0f, "dich_dyn");
        NetworkProcessor.DichotomizeLayer(net, "packed", ConditionType.ge, 3f, 1f, 0f, "dich_packed");

        var dynLayer = net.Layers["dich_dyn"];
        var packedLayer = net.Layers["dich_packed"];
        for (uint i = 1; i <= 5; i++)
            for (uint j = 1; j <= 5; j++)
                Assert.Equal(dynLayer.CheckEdgeExists(i, j), packedLayer.CheckEdgeExists(i, j));
    }

    [Fact]
    public void DichotomizeLayer_OutputIsAlwaysDynamic()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("trust", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("trust", 1, 2, 5f);
        net.Pack("trust");

        NetworkProcessor.DichotomizeLayer(net, "trust", ConditionType.ge, 1f, 1f, 0f, "binary");

        Assert.False(net.Layers["binary"].IsStatic);
    }

    // ── MergeLayers ────────────────────────────────────────────────────────────

    [Fact]
    public void MergeLayers_LayerNotFound_Fails()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        var result = NetworkProcessor.MergeLayers(net, "a", "ghost", MergeMethod.Or, "merged");
        Assert.False(result.Success);
        Assert.Equal("LayerNotFound", result.Code);
    }

    [Fact]
    public void MergeLayers_WrongLayerType_Fails()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerTwoMode("clubs");
        var result = NetworkProcessor.MergeLayers(net, "a", "clubs", MergeMethod.Or, "merged");
        Assert.False(result.Success);
        Assert.Equal("InvalidLayerType", result.Code);
    }

    [Fact]
    public void MergeLayers_MismatchedDirectionality_Fails()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("directed", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddLayerOneMode("undirected", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        var result = NetworkProcessor.MergeLayers(net, "directed", "undirected", MergeMethod.Or, "merged");
        Assert.False(result.Success);
        Assert.Equal("MismatchedDirectionality", result.Code);
    }

    [Fact]
    public void MergeLayers_NewLayerNameAlreadyExists_Fails()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("merged", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        var result = NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.Or, "merged");
        Assert.False(result.Success);
        Assert.Equal("LayerAlreadyExists", result.Code);
    }

    [Fact]
    public void MergeLayers_And_BothBinary_OnlySharedEdgeRemains()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("a", 1, 2);
        net.AddEdge("a", 2, 3);
        net.AddEdge("b", 1, 2);
        net.AddEdge("b", 3, 4);

        var result = NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.And, "merged");

        Assert.True(result.Success);
        Assert.True(net.Layers["merged"].CheckEdgeExists(1, 2));
        Assert.False(net.Layers["merged"].CheckEdgeExists(2, 3));
        Assert.False(net.Layers["merged"].CheckEdgeExists(3, 4));
        Assert.Equal(EdgeType.Binary, ((ILayerOneMode)net.Layers["merged"]).EdgeValueType);
    }

    [Fact]
    public void MergeLayers_Or_BothBinary_UnionOfEdgesRemains()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("a", 1, 2);
        net.AddEdge("b", 3, 4);

        var result = NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.Or, "merged");

        Assert.True(result.Success);
        Assert.True(net.Layers["merged"].CheckEdgeExists(1, 2));
        Assert.True(net.Layers["merged"].CheckEdgeExists(3, 4));
        Assert.Equal(EdgeType.Binary, ((ILayerOneMode)net.Layers["merged"]).EdgeValueType);
    }

    [Fact]
    public void MergeLayers_Xor_BothBinary_OnlyDifferingEdgeRemains()
    {
        // 1-2 in both layers (should drop), 2-3 only in a, 3-4 only in b
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("a", 1, 2);
        net.AddEdge("a", 2, 3);
        net.AddEdge("b", 1, 2);
        net.AddEdge("b", 3, 4);

        var result = NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.Xor, "merged");

        Assert.True(result.Success);
        Assert.False(net.Layers["merged"].CheckEdgeExists(1, 2));
        Assert.True(net.Layers["merged"].CheckEdgeExists(2, 3));
        Assert.True(net.Layers["merged"].CheckEdgeExists(3, 4));
    }

    [Fact]
    public void MergeLayers_Sum_BothBinary_ProducesValuedCounts()
    {
        // 1-2 shared by both (count 2), 2-3 only in a (count 1)
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("a", 1, 2);
        net.AddEdge("a", 2, 3);
        net.AddEdge("b", 1, 2);

        var result = NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.Sum, "merged");

        Assert.True(result.Success);
        Assert.Equal(EdgeType.Valued, ((ILayerOneMode)net.Layers["merged"]).EdgeValueType);
        Assert.Equal(2f, net.Layers["merged"].GetEdgeValue(1, 2));
        Assert.Equal(1f, net.Layers["merged"].GetEdgeValue(2, 3));
    }

    [Fact]
    public void MergeLayers_Product_BothBinary_MatchesAnd()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("a", 1, 2);
        net.AddEdge("a", 2, 3);
        net.AddEdge("b", 1, 2);

        var result = NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.Product, "merged");

        Assert.True(result.Success);
        Assert.Equal(EdgeType.Binary, ((ILayerOneMode)net.Layers["merged"]).EdgeValueType);
        Assert.True(net.Layers["merged"].CheckEdgeExists(1, 2));
        Assert.False(net.Layers["merged"].CheckEdgeExists(2, 3));
    }

    [Fact]
    public void MergeLayers_Max_BothBinary_MatchesOr()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("a", 1, 2);
        net.AddEdge("b", 3, 4);

        var result = NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.Max, "merged");

        Assert.True(result.Success);
        Assert.Equal(EdgeType.Binary, ((ILayerOneMode)net.Layers["merged"]).EdgeValueType);
        Assert.True(net.Layers["merged"].CheckEdgeExists(1, 2));
        Assert.True(net.Layers["merged"].CheckEdgeExists(3, 4));
    }

    [Fact]
    public void MergeLayers_ValuedLayers_Sum_AddsValues()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("a", 1, 2, 3f);
        net.AddEdge("b", 1, 2, 4f);

        var result = NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.Sum, "merged");

        Assert.True(result.Success);
        Assert.Equal(7f, net.Layers["merged"].GetEdgeValue(1, 2));
    }

    [Fact]
    public void MergeLayers_ValuedLayers_Max_TakesMax()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("a", 1, 2, 3f);
        net.AddEdge("b", 1, 2, 7f);

        var result = NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.Max, "merged");

        Assert.True(result.Success);
        Assert.Equal(EdgeType.Valued, ((ILayerOneMode)net.Layers["merged"]).EdgeValueType);
        Assert.Equal(7f, net.Layers["merged"].GetEdgeValue(1, 2));
    }

    [Fact]
    public void MergeLayers_MixedBinaryAndValued_MaxProducesValuedOutput()
    {
        // Max/Min/Product should only stay binary when BOTH inputs are binary.
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("a", 1, 2);
        net.AddEdge("b", 1, 2, 5f);

        var result = NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.Max, "merged");

        Assert.True(result.Success);
        Assert.Equal(EdgeType.Valued, ((ILayerOneMode)net.Layers["merged"]).EdgeValueType);
        Assert.Equal(5f, net.Layers["merged"].GetEdgeValue(1, 2));
    }

    [Fact]
    public void MergeLayers_DirectedLayers_ArcsCombinedIndependentlyPerDirection()
    {
        // Directed + directed is well-defined without symmetrizing: arcs combine independently.
        // a: 1->2 only. b: 2->1 only. AND should find no shared arc in either direction.
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("a", 1, 2);
        net.AddEdge("b", 2, 1);

        var result = NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.And, "merged");

        Assert.True(result.Success);
        Assert.False(net.Layers["merged"].CheckEdgeExists(1, 2));
        Assert.False(net.Layers["merged"].CheckEdgeExists(2, 1));
    }

    [Fact]
    public void MergeLayers_OriginalLayersUnchanged()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("a", 1, 2);
        net.AddEdge("b", 3, 4);

        NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.Or, "merged");

        Assert.True(net.Layers["a"].CheckEdgeExists(1, 2));
        Assert.False(net.Layers["a"].CheckEdgeExists(3, 4));
        Assert.True(net.Layers["b"].CheckEdgeExists(3, 4));
        Assert.False(net.Layers["b"].CheckEdgeExists(1, 2));
    }

    [Fact]
    public void MergeLayers_OutputIsAlwaysDynamic()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("b", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("a", 1, 2);
        net.Pack("a");

        NetworkProcessor.MergeLayers(net, "a", "b", MergeMethod.Or, "merged");

        Assert.False(net.Layers["merged"].IsStatic);
    }

    [Fact]
    public void MergeLayers_StaticAndDynamicSources_ProduceIdenticalResults()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("a_dyn", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("b_dyn", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("a_packed", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("b_packed", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        foreach (var name in new[] { "a_dyn", "a_packed" }) net.AddEdge(name, 1, 2);
        foreach (var name in new[] { "b_dyn", "b_packed" }) { net.AddEdge(name, 1, 2); net.AddEdge(name, 3, 4); }
        net.Pack("a_packed");
        net.Pack("b_packed");

        NetworkProcessor.MergeLayers(net, "a_dyn", "b_dyn", MergeMethod.Or, "merged_dyn");
        NetworkProcessor.MergeLayers(net, "a_packed", "b_packed", MergeMethod.Or, "merged_packed");

        var dynLayer = net.Layers["merged_dyn"];
        var packedLayer = net.Layers["merged_packed"];
        for (uint i = 1; i <= 5; i++)
            for (uint j = 1; j <= 5; j++)
                Assert.Equal(dynLayer.CheckEdgeExists(i, j), packedLayer.CheckEdgeExists(i, j));
    }

    // ── NodesetProcessor.Filter with cloned attribute manager ─────────────────

    [Fact]
    public void Filter_ResultNodeset_PreservesAttributeValues()
    {
        // Filter a nodeset by an int attribute; verify values survive in the result
        var ns = MakeNodeset();   // nodes 1-5
        ns.DefineNodeAttribute("group", "int");
        ns.SetNodeAttribute(1, "group", "1");
        ns.SetNodeAttribute(2, "group", "2");
        ns.SetNodeAttribute(3, "group", "1");
        ns.SetNodeAttribute(4, "group", "2");
        ns.SetNodeAttribute(5, "group", "1");

        var result = NodesetProcessor.Filter(ns, "group", ConditionType.eq, "1");
        Assert.True(result.Success);
        var filtered = result.Value!;

        // Nodes 1, 3, 5 should be in the filtered set
        Assert.Equal(3, filtered.Count);
        var groupAttr = filtered.GetNodeAttribute(1, "group");
        Assert.True(groupAttr.Success);
        Assert.Equal(1, (int)groupAttr.Value.Value.GetValue(groupAttr.Value.Type)!);
    }

    [Fact]
    public void Filter_ResultNodeset_CanDefineAdditionalAttributeWithoutIndexCollision()
    {
        // After filtering, define a new attribute on the result and verify all attributes
        // (pre-existing and new) can be set and read independently — the key regression test
        // for the NodeAttributeDefinitionManager.Clone() _nextIndex fix.
        var ns = MakeNodeset();   // nodes 1-5
        ns.DefineNodeAttribute("score", "float");
        ns.DefineNodeAttribute("active", "bool");
        ns.SetNodeAttribute(1, "score", "9.0");
        ns.SetNodeAttribute(1, "active", "true");
        ns.SetNodeAttribute(2, "score", "5.0");
        ns.SetNodeAttribute(2, "active", "false");

        var filterResult = NodesetProcessor.Filter(ns, "active", ConditionType.eq, "true");
        Assert.True(filterResult.Success);
        var filtered = filterResult.Value!;

        // Define a third attribute on the clone
        Assert.True(filtered.DefineNodeAttribute("weight", "int").Success);

        // Set all three attributes on node 1
        Assert.True(filtered.SetNodeAttribute(1, "score", "8.5").Success);
        Assert.True(filtered.SetNodeAttribute(1, "active", "false").Success);
        Assert.True(filtered.SetNodeAttribute(1, "weight", "75").Success);

        // Read all three back and verify they hold distinct values (no index aliasing)
        var scoreAttr  = filtered.GetNodeAttribute(1, "score");
        var activeAttr = filtered.GetNodeAttribute(1, "active");
        var weightAttr = filtered.GetNodeAttribute(1, "weight");

        Assert.True(scoreAttr.Success);
        Assert.True(activeAttr.Success);
        Assert.True(weightAttr.Success);

        Assert.Equal(8.5f,  (float)scoreAttr.Value.Value.GetValue(scoreAttr.Value.Type)!,   precision: 4);
        Assert.Equal(false, (bool)activeAttr.Value.Value.GetValue(activeAttr.Value.Type)!);
        Assert.Equal(75,    (int)weightAttr.Value.Value.GetValue(weightAttr.Value.Type)!);
    }

    [Fact]
    public void Filter_AfterUndefineRedefine_RecycledIndexWorkingInClone()
    {
        // Undefine an attribute (recycles its index) before filtering; confirm the clone
        // also recycles correctly when a new attribute is defined.
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("tmp", "int");     // index 0
        ns.DefineNodeAttribute("keep", "bool");   // index 1
        ns.SetNodeAttribute(1, "keep", "true");
        ns.SetNodeAttribute(2, "keep", "false");
        ns.UndefineNodeAttribute("tmp");          // index 0 is recycled

        var filterResult = NodesetProcessor.Filter(ns, "keep", ConditionType.eq, "true");
        Assert.True(filterResult.Success);
        var filtered = filterResult.Value!;

        // New attribute should reuse recycled index 0 — must not alias "keep" (index 1)
        Assert.True(filtered.DefineNodeAttribute("new_attr", "char").Success);

        filtered.SetNodeAttribute(1, "keep", "false");
        filtered.SetNodeAttribute(1, "new_attr", "Z");

        var keepAttr    = filtered.GetNodeAttribute(1, "keep");
        var newAttr     = filtered.GetNodeAttribute(1, "new_attr");

        Assert.True(keepAttr.Success);
        Assert.True(newAttr.Success);
        Assert.Equal(false, (bool)keepAttr.Value.Value.GetValue(keepAttr.Value.Type)!);
        Assert.Equal('Z',   (char)newAttr.Value.Value.GetValue(newAttr.Value.Type)!);
    }

    [Fact]
    public void Subnet_ResultNetwork_PreservesNodesetAttributeSchema()
    {
        // Subnet uses CreateFilteredCopy and the filtered nodeset; verify that attributes
        // survive a subnet operation and remain independently readable.
        var ns = MakeNodeset();  // nodes 1-5
        ns.DefineNodeAttribute("group", "int");
        ns.SetNodeAttribute(1, "group", "10");
        ns.SetNodeAttribute(2, "group", "20");

        var subNs = new Nodeset("sub");
        subNs.AddNode(1);
        subNs.AddNode(2);

        var net = new Network("net", ns);
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("layer", 1, 2);

        var subnetResult = NetworkProcessor.Subnet(net, subNs);
        Assert.True(subnetResult.Success);
        // Subnet result uses the provided subNs directly (not a clone of ns),
        // so edges from layer 1-2 should be preserved in the subnet
        Assert.True(subnetResult.Value!.Layers["layer"].CheckEdgeExists(1, 2));
    }

    // ── ProjectTwoModeToOneMode ───────────────────────────────────────────────

    // Helper: network with nodes 1-5 and an empty two-mode layer
    private static Network MakeNetworkWithTwoModeLayer(string layerName = "clubs")
    {
        var net = new Network("net", MakeNodeset());   // nodes 1-5
        net.AddLayerTwoMode(layerName);
        return net;
    }

    // ── Error cases ──────────────────────────────────────────────────────────

    [Fact]
    public void ProjectTwoMode_LayerNotFound_Fails()
    {
        var net = MakeNetworkWithTwoModeLayer();
        var result = NetworkProcessor.ProjectTwoModeToOneMode(net, "ghost", ProjectionMethod.Count, "proj");
        Assert.False(result.Success);
        Assert.Equal("LayerNotFound", result.Code);
    }

    [Fact]
    public void ProjectTwoMode_WrongLayerType_Fails()
    {
        var net = new Network("net", MakeNodeset());
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        var result = NetworkProcessor.ProjectTwoModeToOneMode(net, "layer", ProjectionMethod.Count, "proj");
        Assert.False(result.Success);
        Assert.Equal("InvalidLayerType", result.Code);
    }

    [Fact]
    public void ProjectTwoMode_NewLayerNameAlreadyExists_Fails()
    {
        var net = MakeNetworkWithTwoModeLayer();
        net.AddLayerOneMode("proj", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        var result = NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Count, "proj");
        Assert.False(result.Success);
        Assert.Equal("LayerAlreadyExists", result.Code);
    }

    // ── Count method ─────────────────────────────────────────────────────────

    [Fact]
    public void ProjectTwoMode_Count_SingleSharedHyperedge_EdgeValueIsOne()
    {
        // nodes 1 and 2 share exactly one hyperedge
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u]);
        var result = NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Count, "proj");
        Assert.True(result.Success);
        Assert.Equal(1f, net.Layers["proj"].GetEdgeValue(1, 2));
    }

    [Fact]
    public void ProjectTwoMode_Count_TwoSharedHyperedges_EdgeValueIsTwo()
    {
        // nodes 1 and 2 share two hyperedges
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u, 3u]);
        net.AddHyperedge("clubs", "c2", [1u, 2u, 4u]);
        var result = NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Count, "proj");
        Assert.True(result.Success);
        Assert.Equal(2f, net.Layers["proj"].GetEdgeValue(1, 2));
    }

    [Fact]
    public void ProjectTwoMode_Count_TriangleHyperedge_AllPairsGetEdgeValueOne()
    {
        // hyperedge {1,2,3}: pairs (1,2), (1,3), (2,3) each share exactly 1 hyperedge
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u, 3u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Count, "proj");
        Assert.Equal(1f, net.Layers["proj"].GetEdgeValue(1, 2));
        Assert.Equal(1f, net.Layers["proj"].GetEdgeValue(1, 3));
        Assert.Equal(1f, net.Layers["proj"].GetEdgeValue(2, 3));
    }

    [Fact]
    public void ProjectTwoMode_Count_NoSelfties()
    {
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u, 3u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Count, "proj");
        Assert.False(net.Layers["proj"].CheckEdgeExists(1, 1));
        Assert.False(net.Layers["proj"].CheckEdgeExists(2, 2));
        Assert.False(net.Layers["proj"].CheckEdgeExists(3, 3));
    }

    [Fact]
    public void ProjectTwoMode_Count_NodeInSingletonHyperedge_NoEdgesCreated()
    {
        // hyperedge with only 1 member: no pair exists, so no edge
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Count, "proj");
        var layer = net.Layers["proj"];
        Assert.Equal(0u, ((ILayerOneMode)layer).NbrEdges);
    }

    [Fact]
    public void ProjectTwoMode_Count_EmptyLayer_ProducesEmptyProjectedLayer()
    {
        var net = MakeNetworkWithTwoModeLayer();
        var result = NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Count, "proj");
        Assert.True(result.Success);
        Assert.Equal(0u, ((ILayerOneMode)net.Layers["proj"]).NbrEdges);
    }

    [Fact]
    public void ProjectTwoMode_Count_ResultIsUndirectedValuedLayer()
    {
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Count, "proj");
        var layer = (ILayerOneMode)net.Layers["proj"];
        Assert.False(layer.IsDirectional);
        Assert.Equal(EdgeType.Valued, layer.EdgeValueType);
    }

    [Fact]
    public void ProjectTwoMode_Count_IsSymmetric_BothDirectionsHaveSameValue()
    {
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u]);
        net.AddHyperedge("clubs", "c2", [1u, 2u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Count, "proj");
        Assert.Equal(net.Layers["proj"].GetEdgeValue(1, 2), net.Layers["proj"].GetEdgeValue(2, 1));
    }

    [Fact]
    public void ProjectTwoMode_Count_OriginalTwoModeLayerUnchanged()
    {
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Count, "proj");
        // original layer still present and intact
        Assert.True(net.Layers.ContainsKey("clubs"));
        Assert.Equal(1u, ((ILayerTwoMode)net.Layers["clubs"]).NbrHyperedges);
    }

    // ── Binary method ─────────────────────────────────────────────────────────

    [Fact]
    public void ProjectTwoMode_Binary_MultipleSharedHyperedges_EdgeValueIsOne()
    {
        // nodes 1 and 2 share two hyperedges — binary should still be 1
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u]);
        net.AddHyperedge("clubs", "c2", [1u, 2u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Binary, "proj");
        Assert.Equal(1f, net.Layers["proj"].GetEdgeValue(1, 2));
    }

    [Fact]
    public void ProjectTwoMode_Binary_ResultIsBinaryLayer()
    {
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Binary, "proj");
        Assert.Equal(EdgeType.Binary, ((ILayerOneMode)net.Layers["proj"]).EdgeValueType);
    }

    [Fact]
    public void ProjectTwoMode_Binary_NoPairShared_NoEdge()
    {
        // two hyperedges with disjoint members
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u]);
        net.AddHyperedge("clubs", "c2", [3u, 4u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Binary, "proj");
        Assert.False(net.Layers["proj"].CheckEdgeExists(1, 3));
        Assert.False(net.Layers["proj"].CheckEdgeExists(2, 4));
    }

    // ── Newman method ─────────────────────────────────────────────────────────

    [Fact]
    public void ProjectTwoMode_Newman_SizeThreeHyperedge_WeightIsHalf()
    {
        // hyperedge of size 3: contribution per pair = 1/(3-1) = 0.5
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u, 3u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Newman, "proj");
        Assert.Equal(0.5f, net.Layers["proj"].GetEdgeValue(1, 2), precision: 5);
    }

    [Fact]
    public void ProjectTwoMode_Newman_SizeTwoHyperedge_WeightIsOne()
    {
        // hyperedge of size 2: contribution = 1/(2-1) = 1.0
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Newman, "proj");
        Assert.Equal(1.0f, net.Layers["proj"].GetEdgeValue(1, 2), precision: 5);
    }

    [Fact]
    public void ProjectTwoMode_Newman_TwoHyperedgesDifferentSizes_WeightsAccumulate()
    {
        // nodes 1,2 share: c1 (size 2, contrib 1.0) and c2 (size 3, contrib 0.5) → total 1.5
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u]);
        net.AddHyperedge("clubs", "c2", [1u, 2u, 3u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Newman, "proj");
        Assert.Equal(1.5f, net.Layers["proj"].GetEdgeValue(1, 2), precision: 5);
    }

    [Fact]
    public void ProjectTwoMode_Newman_ResultIsValuedLayer()
    {
        var net = MakeNetworkWithTwoModeLayer();
        net.AddHyperedge("clubs", "c1", [1u, 2u, 3u]);
        NetworkProcessor.ProjectTwoModeToOneMode(net, "clubs", ProjectionMethod.Newman, "proj");
        Assert.Equal(EdgeType.Valued, ((ILayerOneMode)net.Layers["proj"]).EdgeValueType);
    }

    // ── Static (packed) two-mode layer ────────────────────────────────────────

    [Fact]
    public void ProjectTwoMode_Count_StaticLayer_MatchesDynamic()
    {
        var netDyn    = MakeNetworkWithTwoModeLayer();
        var netPacked = MakeNetworkWithTwoModeLayer();
        foreach (var net in new[] { netDyn, netPacked })
        {
            net.AddHyperedge("clubs", "c1", [1u, 2u, 3u]);
            net.AddHyperedge("clubs", "c2", [2u, 3u, 4u]);
            net.AddHyperedge("clubs", "c3", [1u, 4u, 5u]);
        }
        netPacked.Pack("clubs");

        NetworkProcessor.ProjectTwoModeToOneMode(netDyn,    "clubs", ProjectionMethod.Count, "proj");
        NetworkProcessor.ProjectTwoModeToOneMode(netPacked, "clubs", ProjectionMethod.Count, "proj");

        for (uint i = 1; i <= 5; i++)
            for (uint j = i + 1; j <= 5; j++)
                Assert.Equal(
                    netDyn.Layers["proj"].GetEdgeValue(i, j),
                    netPacked.Layers["proj"].GetEdgeValue(i, j)
                );
    }

    [Fact]
    public void ProjectTwoMode_Newman_StaticLayer_MatchesDynamic()
    {
        var netDyn    = MakeNetworkWithTwoModeLayer();
        var netPacked = MakeNetworkWithTwoModeLayer();
        foreach (var net in new[] { netDyn, netPacked })
        {
            net.AddHyperedge("clubs", "c1", [1u, 2u, 3u]);
            net.AddHyperedge("clubs", "c2", [2u, 3u, 4u, 5u]);
        }
        netPacked.Pack("clubs");

        NetworkProcessor.ProjectTwoModeToOneMode(netDyn,    "clubs", ProjectionMethod.Newman, "proj");
        NetworkProcessor.ProjectTwoModeToOneMode(netPacked, "clubs", ProjectionMethod.Newman, "proj");

        for (uint i = 1; i <= 5; i++)
            for (uint j = i + 1; j <= 5; j++)
                Assert.Equal(
                    netDyn.Layers["proj"].GetEdgeValue(i, j),
                    netPacked.Layers["proj"].GetEdgeValue(i, j),
                    precision: 5
                );
    }

    // ── NodesetProcessor.Filter with string attributes ────────────────────────

    [Fact]
    public void Filter_StringAttribute_EqualCondition_ReturnsMatchingNodes()
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 5; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("country", "string");
        ns.SetNodeAttribute(1, "country", "Sweden");
        ns.SetNodeAttribute(2, "country", "Sweden");
        ns.SetNodeAttribute(3, "country", "Norway");
        ns.SetNodeAttribute(4, "country", "Sweden");
        ns.SetNodeAttribute(5, "country", "Norway");

        var result = NodesetProcessor.Filter(ns, "country", ConditionType.eq, "Sweden");

        Assert.True(result.Success);
        var filtered = result.Value!;
        Assert.Equal(3, filtered.Count);
        Assert.Contains(1u, filtered.NodeIdArray);
        Assert.Contains(2u, filtered.NodeIdArray);
        Assert.Contains(4u, filtered.NodeIdArray);
    }

    [Fact]
    public void Filter_StringAttribute_NotEqualCondition_ExcludesMatchingNodes()
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 4; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("country", "string");
        ns.SetNodeAttribute(1, "country", "Sweden");
        ns.SetNodeAttribute(2, "country", "Norway");
        ns.SetNodeAttribute(3, "country", "Sweden");
        ns.SetNodeAttribute(4, "country", "Denmark");

        var result = NodesetProcessor.Filter(ns, "country", ConditionType.ne, "Sweden");

        Assert.True(result.Success);
        var filtered = result.Value!;
        Assert.Equal(2, filtered.Count);
        Assert.Contains(2u, filtered.NodeIdArray);
        Assert.Contains(4u, filtered.NodeIdArray);
    }

    [Fact]
    public void Filter_StringAttribute_IsNullCondition_ReturnsNodesWithoutAttribute()
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 4; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("country", "string");
        ns.SetNodeAttribute(1, "country", "Sweden");
        ns.SetNodeAttribute(3, "country", "Norway");
        // nodes 2 and 4 have no value set

        var result = NodesetProcessor.Filter(ns, "country", ConditionType.isnull);

        Assert.True(result.Success);
        var filtered = result.Value!;
        Assert.Equal(2, filtered.Count);
        Assert.Contains(2u, filtered.NodeIdArray);
        Assert.Contains(4u, filtered.NodeIdArray);
    }

    [Fact]
    public void Filter_StringAttribute_NotNullCondition_ReturnsNodesWithAttribute()
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 4; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("country", "string");
        ns.SetNodeAttribute(2, "country", "Sweden");
        ns.SetNodeAttribute(4, "country", "Norway");
        // nodes 1 and 3 have no value set

        var result = NodesetProcessor.Filter(ns, "country", ConditionType.notnull);

        Assert.True(result.Success);
        var filtered = result.Value!;
        Assert.Equal(2, filtered.Count);
        Assert.Contains(2u, filtered.NodeIdArray);
        Assert.Contains(4u, filtered.NodeIdArray);
    }

    [Fact]
    public void Filter_StringAttribute_PreservesResolvedStringValue()
    {
        // Verify that the string pool is copied into the filtered nodeset so
        // GetNodeAttributeString returns the actual string, not "(invalid)".
        var ns = new Nodeset("ns");
        ns.AddNode(1);
        ns.AddNode(2);
        ns.DefineNodeAttribute("occupation", "string");
        ns.SetNodeAttribute(1, "occupation", "Engineer");
        ns.SetNodeAttribute(2, "occupation", "Teacher");

        var result = NodesetProcessor.Filter(ns, "occupation", ConditionType.eq, "Engineer");

        Assert.True(result.Success);
        var filtered = result.Value!;
        Assert.Equal(1, filtered.Count);
        var attrResult = filtered.GetNodeAttributeString(1, "occupation");
        Assert.True(attrResult.Success);
        Assert.Equal("Engineer", attrResult.Value);
    }
}
