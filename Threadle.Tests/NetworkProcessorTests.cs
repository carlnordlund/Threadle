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

        var result = NetworkProcessor.DichotomizeLayer(net, "trust", ConditionType.gte, 3f, 1f, 0f, "binary");

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

        var result = NetworkProcessor.DichotomizeLayer(net, "trust", ConditionType.gte, 3f, 1f, 0f, "binary");

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

        NetworkProcessor.DichotomizeLayer(net, "dyn", ConditionType.gte, 3f, 1f, 0f, "dich_dyn");
        NetworkProcessor.DichotomizeLayer(net, "packed", ConditionType.gte, 3f, 1f, 0f, "dich_packed");

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

        NetworkProcessor.DichotomizeLayer(net, "trust", ConditionType.gte, 1f, 1f, 0f, "binary");

        Assert.False(net.Layers["binary"].IsStatic);
    }
}
