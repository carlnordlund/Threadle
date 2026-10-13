using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Processing.Enums;

namespace Threadle.Tests;

public class AlterDetailsTests
{
    /// <summary>
    /// Nodes 1-6. 'kin' is directed 1-mode: 1->2, 1->3, 4->1.
    /// 'household' is 2-mode: hh1 = {1,2,5}, hh2 = {1,6}, hh3 = {3,4}.
    /// Attribute 'state' (char-like string): 2 = "I", 3 = "S", 5 = "I", 6 = "R"; 4 has none.
    /// </summary>
    private static Network MakeNetwork()
    {
        var nodeset = new Nodeset("ns");
        for (uint i = 1; i <= 6; i++) nodeset.AddNode(i);
        nodeset.DefineNodeAttribute("state", "string");
        nodeset.SetNodeAttribute(2, "state", "I");
        nodeset.SetNodeAttribute(3, "state", "S");
        nodeset.SetNodeAttribute(5, "state", "I");
        nodeset.SetNodeAttribute(6, "state", "R");
        var net = new Network("net", nodeset);
        net.AddLayerOneMode("kin", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("kin", 1, 2);
        net.AddEdge("kin", 1, 3);
        net.AddEdge("kin", 4, 1);
        net.AddLayerTwoMode("household");
        net.AddHyperedge("household", "hh1", [1, 2, 5]);
        net.AddHyperedge("household", "hh2", [1, 6]);
        net.AddHyperedge("household", "hh3", [3, 4]);
        return net;
    }

    private static List<(uint ego, uint alter, string layer, string? hyper, int size)> Rows(AlterDetails t) =>
        Enumerable.Range(0, t.NbrRows).Select(i => (t.Ego[i], t.Alter[i], t.Layer[i], t.Hyperedge[i], t.Hypersize[i])).ToList();

    [Fact]
    public void GetAlterDetails_OneAndTwoMode_RowsPerEgoAlterLayerAffiliation()
    {
        var net = MakeNetwork();
        var result = NetworkProcessor.GetAlterDetails(net, [1], ["kin", "household"], EdgeTraversal.Out);
        Assert.True(result.Success);
        var rows = Rows(result.Value!);
        Assert.Equal(5, rows.Count);
        Assert.Contains((1u, 2u, "kin", (string?)null, 0), rows);
        Assert.Contains((1u, 3u, "kin", (string?)null, 0), rows);
        Assert.Contains((1u, 2u, "household", "hh1", 3), rows);
        Assert.Contains((1u, 5u, "household", "hh1", 3), rows);
        Assert.Contains((1u, 6u, "household", "hh2", 2), rows);
        // Ego excluded from its own 2-mode alters
        Assert.DoesNotContain(rows, r => r.alter == 1);
    }

    [Fact]
    public void GetAlterDetails_NoDeduplication_AlterInSeveralLayersAppearsPerLayer()
    {
        var net = MakeNetwork();
        var rows = Rows(NetworkProcessor.GetAlterDetails(net, [1], null, EdgeTraversal.Out).Value!);
        Assert.Equal(2, rows.Count(r => r.alter == 2));
    }

    [Fact]
    public void GetAlterDetails_MultipleEgos_RowsForEach()
    {
        var net = MakeNetwork();
        var rows = Rows(NetworkProcessor.GetAlterDetails(net, [1, 4], ["household"], EdgeTraversal.Out).Value!);
        Assert.Equal(4, rows.Count);
        Assert.Contains((4u, 3u, "household", "hh3", 2), rows);
    }

    [Theory]
    [InlineData(EdgeTraversal.Out, new uint[] { 2, 3 })]
    [InlineData(EdgeTraversal.In, new uint[] { 4 })]
    [InlineData(EdgeTraversal.Both, new uint[] { 2, 3, 4 })]
    public void GetAlterDetails_Direction_AppliesToOneModeLayers(EdgeTraversal direction, uint[] expected)
    {
        var net = MakeNetwork();
        var table = NetworkProcessor.GetAlterDetails(net, [1], ["kin"], direction).Value!;
        Assert.Equal(expected, table.Alter.Order().ToArray());
    }

    [Fact]
    public void GetAlterDetails_Condition_AppliesToAltersOnly()
    {
        var net = MakeNetwork();
        // Ego 1 has no 'state' itself, but is still queried
        var table = NetworkProcessor.GetAlterDetails(net, [1], null, EdgeTraversal.Out, "state", ConditionType.eq, "I").Value!;
        Assert.Equal(3, table.NbrRows);
        Assert.All(table.Alter, a => Assert.True(a == 2 || a == 5));
    }

    [Fact]
    public void GetAlterDetails_IsNullCondition_MissingAttributeCountsAsNull()
    {
        var net = MakeNetwork();
        var table = NetworkProcessor.GetAlterDetails(net, [3], ["household"], EdgeTraversal.Out, "state", ConditionType.isnull).Value!;
        Assert.Equal(new uint[] { 4 }, table.Alter.ToArray());
    }

    [Fact]
    public void GetAlterDetails_PackedLayers_SameRows()
    {
        var net = MakeNetwork();
        var dynamicRows = Rows(NetworkProcessor.GetAlterDetails(net, [1], null, EdgeTraversal.Out).Value!);
        net.Layers["kin"] = LayerOneModeStatic.FromDynamic((LayerOneMode)net.Layers["kin"]);
        net.Layers["household"] = LayerTwoModeStatic.FromDynamic((LayerTwoMode)net.Layers["household"]);
        var packedRows = Rows(NetworkProcessor.GetAlterDetails(net, [1], null, EdgeTraversal.Out).Value!);
        Assert.Equal(dynamicRows.Order(), packedRows.Order());
    }

    [Fact]
    public void GetAlterDetails_NodeWithoutAlters_NoRows()
    {
        var net = MakeNetwork();
        net.Nodeset.AddNode(7);
        var result = NetworkProcessor.GetAlterDetails(net, [7], null, EdgeTraversal.Both);
        Assert.True(result.Success);
        Assert.Equal(0, result.Value!.NbrRows);
    }

    [Fact]
    public void GetAlterDetails_UnknownNode_Fails()
    {
        var result = NetworkProcessor.GetAlterDetails(MakeNetwork(), [1, 99], null, EdgeTraversal.Out);
        Assert.False(result.Success);
        Assert.Equal("NodeNotFound", result.Code);
    }

    [Fact]
    public void GetAlterDetails_UnknownLayer_Fails()
    {
        var result = NetworkProcessor.GetAlterDetails(MakeNetwork(), [1], ["ghost"], EdgeTraversal.Out);
        Assert.False(result.Success);
        Assert.Equal("LayerNotFound", result.Code);
    }

    [Fact]
    public void GetAlterDetails_UnknownAttribute_Fails()
    {
        var result = NetworkProcessor.GetAlterDetails(MakeNetwork(), [1], null, EdgeTraversal.Out, "ghost", ConditionType.eq, "x");
        Assert.False(result.Success);
        Assert.Equal("AttributeUnknown", result.Code);
    }

    [Fact]
    public void GetAlterDetails_MissingAttributeValue_Fails()
    {
        var result = NetworkProcessor.GetAlterDetails(MakeNetwork(), [1], null, EdgeTraversal.Out, "state", ConditionType.eq, "");
        Assert.False(result.Success);
        Assert.Equal("MissingAttributeValue", result.Code);
    }

    // ── GetNodeHyperedgesWithMembers ─────────────────────────────────────────

    [Fact]
    public void GetNodeHyperedgesWithMembers_DynamicAndPacked_Agree()
    {
        var net = MakeNetwork();
        var dynamicLayer = (ILayerTwoMode)net.Layers["household"];
        var packedLayer = LayerTwoModeStatic.FromDynamic((LayerTwoMode)dynamicLayer);
        foreach (ILayerTwoMode layer in new[] { dynamicLayer, packedLayer })
        {
            var data = layer.GetNodeHyperedgesWithMembers(1).ToDictionary(h => h.hypername, h => h.nodeIds.ToArray().Order().ToArray());
            Assert.Equal(2, data.Count);
            Assert.Equal(new uint[] { 1, 2, 5 }, data["hh1"]);
            Assert.Equal(new uint[] { 1, 6 }, data["hh2"]);
            Assert.Empty(layer.GetNodeHyperedgesWithMembers(99));
        }
    }
}
