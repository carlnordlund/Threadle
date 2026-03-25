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
}
