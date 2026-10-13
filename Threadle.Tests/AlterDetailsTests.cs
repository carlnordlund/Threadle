using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing.Enums;

namespace Threadle.Tests;

/// <summary>
/// Tests for setting attributes for multiple nodes, attribute conditions, and detailed alter queries
/// (the Core functionality behind the 'setattrs' and 'getalterdetails' CLI commands).
/// </summary>
public class AlterDetailsTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a network with nodes 0-5 and a char attribute 'state', where:
    /// - 'kin' is an undirected 1-mode layer with edges 0-1 and 0-2
    /// - 'household' is a 2-mode layer with hyperedges h1 = {0,1,3} and h2 = {0,3,4}, and h3 = {5}
    /// </summary>
    private static Network MakeNetwork()
    {
        var nodeset = new Nodeset("ns", 6);
        nodeset.DefineNodeAttribute("state", "char");
        var net = new Network("net", nodeset);
        net.AddLayerOneMode("kin", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("kin", 0, 1);
        net.AddEdge("kin", 0, 2);
        net.AddLayerTwoMode("household");
        net.AddHyperedge("household", "h1", [0u, 1u, 3u]);
        net.AddHyperedge("household", "h2", [0u, 3u, 4u]);
        net.AddHyperedge("household", "h3", [5u]);
        return net;
    }

    /// <summary>Returns the rows of a column-oriented alter details table as tuples, sorted.</summary>
    private static List<(uint ego, uint alter, string layer, string? hyperedge, int hypersize)> Rows(Dictionary<string, object> table)
    {
        var egos = (uint[])table["ego"];
        var alters = (uint[])table["alter"];
        var layers = (string[])table["layer"];
        var hyperedges = (string?[])table["hyperedge"];
        var hypersizes = (int[])table["hypersize"];
        Assert.Equal(egos.Length, alters.Length);
        Assert.Equal(egos.Length, layers.Length);
        Assert.Equal(egos.Length, hyperedges.Length);
        Assert.Equal(egos.Length, hypersizes.Length);
        return Enumerable.Range(0, egos.Length)
            .Select(i => (egos[i], alters[i], layers[i], hyperedges[i], hypersizes[i]))
            .OrderBy(r => r.Item1).ThenBy(r => r.Item3).ThenBy(r => r.Item4).ThenBy(r => r.Item2)
            .ToList();
    }

    // ── SetMultipleNodeAttributes ───────────────────────────────────────────────

    [Fact]
    public void SetMultipleNodeAttributes_SingleValue_SetsListedNodesOnly()
    {
        var net = MakeNetwork();
        var result = net.Nodeset.SetMultipleNodeAttributes([1u, 3u], "state", ["I"]);
        Assert.True(result.Success);
        Assert.Equal('I', net.Nodeset.GetNodeAttribute(1, "state").Value.Value.GetValue(NodeAttributeType.Char));
        Assert.Equal('I', net.Nodeset.GetNodeAttribute(3, "state").Value.Value.GetValue(NodeAttributeType.Char));
        Assert.False(net.Nodeset.GetNodeAttribute(2, "state").Success);
    }

    [Fact]
    public void SetMultipleNodeAttributes_NullNodes_SetsAllNodes()
    {
        var net = MakeNetwork();
        var result = net.Nodeset.SetMultipleNodeAttributes(null, "state", ["S"]);
        Assert.True(result.Success);
        foreach (uint nodeId in net.Nodeset.NodeIdArray)
            Assert.Equal('S', net.Nodeset.GetNodeAttribute(nodeId, "state").Value.Value.GetValue(NodeAttributeType.Char));
    }

    [Fact]
    public void SetMultipleNodeAttributes_OneValuePerNode_SetsInOrder()
    {
        var net = MakeNetwork();
        var result = net.Nodeset.SetMultipleNodeAttributes([4u, 5u], "state", ["E", "R"]);
        Assert.True(result.Success);
        Assert.Equal('E', net.Nodeset.GetNodeAttribute(4, "state").Value.Value.GetValue(NodeAttributeType.Char));
        Assert.Equal('R', net.Nodeset.GetNodeAttribute(5, "state").Value.Value.GetValue(NodeAttributeType.Char));
    }

    [Fact]
    public void SetMultipleNodeAttributes_StringAttribute_Succeeds()
    {
        var net = MakeNetwork();
        net.Nodeset.DefineNodeAttribute("label", "string");
        var result = net.Nodeset.SetMultipleNodeAttributes([0u, 1u], "label", ["alpha", "beta"]);
        Assert.True(result.Success);
        Assert.Equal("alpha", net.Nodeset.GetNodeAttributeString(0, "label").Value);
        Assert.Equal("beta", net.Nodeset.GetNodeAttributeString(1, "label").Value);
    }

    [Fact]
    public void SetMultipleNodeAttributes_ValueCountMismatch_Fails()
    {
        var net = MakeNetwork();
        var result = net.Nodeset.SetMultipleNodeAttributes([0u, 1u, 2u], "state", ["S", "I"]);
        Assert.False(result.Success);
        Assert.Equal("ValueCountMismatch", result.Code);
    }

    [Fact]
    public void SetMultipleNodeAttributes_MissingNode_FailsAndChangesNothing()
    {
        var net = MakeNetwork();
        var result = net.Nodeset.SetMultipleNodeAttributes([0u, 99u], "state", ["I"]);
        Assert.False(result.Success);
        Assert.Equal("NodeNotFound", result.Code);
        Assert.False(net.Nodeset.GetNodeAttribute(0, "state").Success);
    }

    [Fact]
    public void SetMultipleNodeAttributes_UnparsableValue_FailsAndChangesNothing()
    {
        var net = MakeNetwork();
        var result = net.Nodeset.SetMultipleNodeAttributes([0u, 1u], "state", ["I", "too long"]);
        Assert.False(result.Success);
        Assert.Equal("ParseAttributeValueError", result.Code);
        Assert.False(net.Nodeset.GetNodeAttribute(0, "state").Success);
    }

    [Fact]
    public void SetMultipleNodeAttributes_UnknownAttribute_Fails()
    {
        var net = MakeNetwork();
        var result = net.Nodeset.SetMultipleNodeAttributes([0u], "ghost", ["I"]);
        Assert.False(result.Success);
        Assert.Equal("AttributeUnknown", result.Code);
    }

    // ── CreateNodeAttributeCondition ────────────────────────────────────────────

    [Fact]
    public void CreateNodeAttributeCondition_FollowsFilterSemantics()
    {
        var net = MakeNetwork();
        net.Nodeset.SetMultipleNodeAttributes([0u, 1u], "state", ["S", "I"]);

        var eq = net.Nodeset.CreateNodeAttributeCondition("state", ConditionType.eq, "S").Value!;
        Assert.True(eq(0));
        Assert.False(eq(1));
        Assert.False(eq(2));    // missing attribute fails 'eq'

        var ne = net.Nodeset.CreateNodeAttributeCondition("state", ConditionType.ne, "S").Value!;
        Assert.False(ne(0));
        Assert.True(ne(1));
        Assert.True(ne(2));     // missing attribute counts as 'not equal'

        var isnull = net.Nodeset.CreateNodeAttributeCondition("state", ConditionType.isnull).Value!;
        Assert.False(isnull(0));
        Assert.True(isnull(2));
    }

    [Fact]
    public void CreateNodeAttributeCondition_IntComparison()
    {
        var net = MakeNetwork();
        net.Nodeset.DefineNodeAttribute("age", "int");
        net.Nodeset.SetMultipleNodeAttributes([0u, 1u], "age", ["30", "75"]);
        var ge = net.Nodeset.CreateNodeAttributeCondition("age", ConditionType.ge, "70").Value!;
        Assert.False(ge(0));
        Assert.True(ge(1));
    }

    [Fact]
    public void CreateNodeAttributeCondition_InvalidInput_Fails()
    {
        var net = MakeNetwork();
        Assert.Equal("AttributeUnknown", net.Nodeset.CreateNodeAttributeCondition("ghost", ConditionType.eq, "S").Code);
        Assert.Equal("MissingAttributeValue", net.Nodeset.CreateNodeAttributeCondition("state", ConditionType.eq, "").Code);
        Assert.Equal("ParseAttributeValueError", net.Nodeset.CreateNodeAttributeCondition("state", ConditionType.eq, "SS").Code);
    }

    // ── GetAlterDetails ─────────────────────────────────────────────────────────

    [Fact]
    public void GetAlterDetails_AllLayers_ReturnsOneRowPerEgoAlterLayerHyperedge()
    {
        var net = MakeNetwork();
        var result = net.GetAlterDetails([0u], null, EdgeTraversal.Both);
        Assert.True(result.Success);
        var expected = new List<(uint, uint, string, string?, int)>
        {
            (0, 1, "household", "h1", 3),
            (0, 3, "household", "h1", 3),
            (0, 3, "household", "h2", 3),   // alter 3 shares two hyperedges with ego: two rows
            (0, 4, "household", "h2", 3),
            (0, 1, "kin", null, 0),
            (0, 2, "kin", null, 0),
        };
        Assert.Equal(expected, Rows(result.Value!));
    }

    [Fact]
    public void GetAlterDetails_MultipleEgos_DoesNotDeduplicate()
    {
        var net = MakeNetwork();
        var rows = Rows(net.GetAlterDetails([1u, 4u], ["household"], EdgeTraversal.Both).Value!);
        var expected = new List<(uint, uint, string, string?, int)>
        {
            (1, 0, "household", "h1", 3),
            (1, 3, "household", "h1", 3),
            (4, 0, "household", "h2", 3),
            (4, 3, "household", "h2", 3),   // alter 3 appears for both egos 1 and 4
        };
        Assert.Equal(expected, rows);
    }

    [Fact]
    public void GetAlterDetails_EgoAloneInHyperedge_ReturnsNoRows()
    {
        var net = MakeNetwork();
        var result = net.GetAlterDetails([5u], null, EdgeTraversal.Both);
        Assert.True(result.Success);
        Assert.Empty((uint[])result.Value!["ego"]);
    }

    [Fact]
    public void GetAlterDetails_WithCondition_KeepsOnlyMatchingAlters()
    {
        var net = MakeNetwork();
        net.Nodeset.SetMultipleNodeAttributes(null, "state", ["S"]);
        net.Nodeset.SetMultipleNodeAttributes([0u, 3u], "state", ["I"]);
        var condition = net.Nodeset.CreateNodeAttributeCondition("state", ConditionType.eq, "S").Value!;

        var rows = Rows(net.GetAlterDetails([0u, 3u], null, EdgeTraversal.Both, condition).Value!);
        Assert.All(rows, r => Assert.NotEqual(3u, r.alter));
        Assert.All(rows, r => Assert.NotEqual(0u, r.alter));
        var expected = new List<(uint, uint, string, string?, int)>
        {
            (0, 1, "household", "h1", 3),
            (0, 4, "household", "h2", 3),
            (0, 1, "kin", null, 0),
            (0, 2, "kin", null, 0),
            (3, 1, "household", "h1", 3),
            (3, 4, "household", "h2", 3),
        };
        Assert.Equal(expected, rows);
    }

    [Fact]
    public void GetAlterDetails_PackedLayers_GiveSameRowsAsDynamic()
    {
        var net = MakeNetwork();
        var dynamicRows = Rows(net.GetAlterDetails([0u, 1u, 3u], null, EdgeTraversal.Out).Value!);
        Assert.True(net.Pack(null).Success);
        Assert.True(net.Layers["household"].IsStatic);
        var packedRows = Rows(net.GetAlterDetails([0u, 1u, 3u], null, EdgeTraversal.Out).Value!);
        Assert.Equal(dynamicRows, packedRows);
    }

    [Fact]
    public void GetAlterDetails_DirectedLayer_RespectsDirection()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("follows", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("follows", 0, 1);
        net.AddEdge("follows", 2, 0);
        Assert.Equal([1u], (uint[])net.GetAlterDetails([0u], ["follows"], EdgeTraversal.Out).Value!["alter"]);
        Assert.Equal([2u], (uint[])net.GetAlterDetails([0u], ["follows"], EdgeTraversal.In).Value!["alter"]);
    }

    [Fact]
    public void GetAlterDetails_MissingNode_Fails()
    {
        var net = MakeNetwork();
        var result = net.GetAlterDetails([0u, 99u], null, EdgeTraversal.Both);
        Assert.False(result.Success);
        Assert.Equal("NodeNotFound", result.Code);
    }

    [Fact]
    public void GetAlterDetails_MissingLayer_Fails()
    {
        var net = MakeNetwork();
        var result = net.GetAlterDetails([0u], ["ghost"], EdgeTraversal.Both);
        Assert.False(result.Success);
        Assert.Equal("LayerNotFound", result.Code);
    }
}
