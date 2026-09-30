using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// Tests for importing edgelists and matrices to layers, focusing on string node labels (labelattr)
/// and filtered edgelist import (filtercol/filtervalue).
/// </summary>
public class ImportTests : IDisposable
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private readonly List<string> _tempFiles = [];

    /// <summary>Writes the given lines to a new temp file and returns its path.</summary>
    private string WriteTempFile(params string[] lines)
    {
        string path = Path.Combine(Path.GetTempPath(), $"threadle_import_test_{Guid.NewGuid()}.txt");
        File.WriteAllLines(path, lines);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f))
                File.Delete(f);
    }

    /// <summary>Returns the node id that has the given label, failing the test if not exactly one is found.</summary>
    private static uint IdOf(Nodeset nodeset, string label, string attr = "label")
    {
        var ids = nodeset.NodeIdArray.Where(id =>
        {
            var r = nodeset.GetNodeAttributeString(id, attr);
            return r.Success && r.Value == label;
        }).ToList();
        Assert.Single(ids);
        return ids[0];
    }

    private static Network EmptyNetwork() => new("net", new Nodeset("ns"));

    // ── 1-mode edgelist with labels ───────────────────────────────────────────

    [Fact]
    public void ImportOneModeEdgelist_Labels_CreatesNodesWithLabels()
    {
        var net = EmptyNetwork();
        net.AddLayerOneMode("syn", EdgeDirectionality.Directed, EdgeType.Binary, false);
        var layer = (LayerOneMode)net.Layers["syn"];
        string path = WriteTempFile("pre\tpost", "neuron_A\tneuron_B", "neuron_B\tneuron_C", "\"neuron_A\"\tneuron_C");

        var result = FileManager.ImportOneModeEdgeList(path, net, layer, 0, 1, 2, true, '\t', true, "label");

        Assert.True(result.Success, result.Message);
        Assert.Equal(3, net.Nodeset.Count);
        uint a = IdOf(net.Nodeset, "neuron_A"), b = IdOf(net.Nodeset, "neuron_B"), c = IdOf(net.Nodeset, "neuron_C");
        Assert.True(net.CheckEdgeExists("syn", a, b).Value);
        Assert.True(net.CheckEdgeExists("syn", b, c).Value);
        Assert.True(net.CheckEdgeExists("syn", a, c).Value);
        Assert.False(net.CheckEdgeExists("syn", b, a).Value);
    }

    [Fact]
    public void ImportOneModeEdgelist_Labels_NewIdsContinueAfterExistingNodes()
    {
        var ns = new Nodeset("ns");
        ns.AddNode(10);
        ns.AddNode(20);
        var net = new Network("net", ns);
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        string path = WriteTempFile("x\ty");

        FileManager.ImportOneModeEdgeList(path, net, (LayerOneMode)net.Layers["l"], 0, 1, 2, false, '\t', true, "label");

        Assert.Equal(4, net.Nodeset.Count);
        Assert.Equal(21u, IdOf(net.Nodeset, "x"));
        Assert.Equal(22u, IdOf(net.Nodeset, "y"));
    }

    [Fact]
    public void ImportOneModeEdgelist_Labels_SecondLayerReusesExistingLabels()
    {
        var net = EmptyNetwork();
        net.AddLayerOneMode("l1", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("l2", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        string path1 = WriteTempFile("anna,bo", "bo,cai");
        string path2 = WriteTempFile("anna,cai,2.5", "cai,dan,1");

        FileManager.ImportOneModeEdgeList(path1, net, (LayerOneMode)net.Layers["l1"], 0, 1, 2, false, ',', true, "label");
        FileManager.ImportOneModeEdgeList(path2, net, (LayerOneMode)net.Layers["l2"], 0, 1, 2, false, ',', true, "label");

        Assert.Equal(4, net.Nodeset.Count);
        Assert.Equal(2.5f, net.GetEdge("l2", IdOf(net.Nodeset, "anna"), IdOf(net.Nodeset, "cai")).Value, precision: 4);
        Assert.Equal(1f, net.GetEdge("l2", IdOf(net.Nodeset, "cai"), IdOf(net.Nodeset, "dan")).Value, precision: 4);
    }

    [Fact]
    public void ImportOneModeEdgelist_Labels_NoAddMissing_IgnoresUnknownLabels()
    {
        var ns = new Nodeset("ns");
        ns.AddNode(0);
        ns.AddNode(1);
        ns.DefineNodeAttribute("name", "string");
        ns.SetNodeAttribute(0, "name", "anna");
        ns.SetNodeAttribute(1, "name", "bo");
        var net = new Network("net", ns);
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        string path = WriteTempFile("anna\tbo", "anna\tunknown");

        var result = FileManager.ImportOneModeEdgeList(path, net, (LayerOneMode)net.Layers["l"], 0, 1, 2, false, '\t', false, "name");

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, net.Nodeset.Count);
        Assert.True(net.CheckEdgeExists("l", 0, 1).Value);
    }

    [Fact]
    public void ImportOneModeEdgelist_Labels_NonStringAttribute_Fails()
    {
        var net = EmptyNetwork();
        net.Nodeset.DefineNodeAttribute("label", "int");
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        string path = WriteTempFile("a\tb");

        var result = FileManager.ImportOneModeEdgeList(path, net, (LayerOneMode)net.Layers["l"], 0, 1, 2, false, '\t', true, "label");

        Assert.False(result.Success);
    }

    [Fact]
    public void ImportOneModeEdgelist_Labels_DuplicateExistingLabels_Fails()
    {
        var ns = new Nodeset("ns");
        ns.AddNode(0);
        ns.AddNode(1);
        ns.DefineNodeAttribute("label", "string");
        ns.SetNodeAttribute(0, "label", "same");
        ns.SetNodeAttribute(1, "label", "same");
        var net = new Network("net", ns);
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        string path = WriteTempFile("a\tb");

        var result = FileManager.ImportOneModeEdgeList(path, net, (LayerOneMode)net.Layers["l"], 0, 1, 2, false, '\t', true, "label");

        Assert.False(result.Success);
    }

    // ── 1-mode edgelist: numeric ids (unchanged behaviour) and filtering ──────

    [Fact]
    public void ImportOneModeEdgelist_NumericIds_StillWorks()
    {
        var net = EmptyNetwork();
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        string path = WriteTempFile("1\t2", "2\t3", "x\t4");

        var result = FileManager.ImportOneModeEdgeList(path, net, (LayerOneMode)net.Layers["l"], 0, 1, 2, false, '\t', true);

        Assert.True(result.Success, result.Message);
        Assert.Equal(3, net.Nodeset.Count);   // the 'x' line is skipped, and node 4 is not added
        Assert.True(net.CheckEdgeExists("l", 1, 2).Value);
        Assert.True(net.CheckEdgeExists("l", 2, 3).Value);
        Assert.False(net.Nodeset.GetNodeAttribute(1, "label").Success);
    }

    [Fact]
    public void ImportOneModeEdgelist_Filter_OnlyImportsMatchingLines()
    {
        var net = EmptyNetwork();
        net.AddLayerOneMode("friend", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("work", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        string path = WriteTempFile("from\tto\ttype", "1\t2\tfriend", "2\t3\twork", "3\t4\t\"friend\"", "1\t4\twork");

        FileManager.ImportOneModeEdgeList(path, net, (LayerOneMode)net.Layers["friend"], 0, 1, 2, true, '\t', true, null, 2, "friend");
        FileManager.ImportOneModeEdgeList(path, net, (LayerOneMode)net.Layers["work"], 0, 1, 2, true, '\t', true, null, 2, "work");

        Assert.True(net.CheckEdgeExists("friend", 1, 2).Value);
        Assert.True(net.CheckEdgeExists("friend", 3, 4).Value);
        Assert.False(net.CheckEdgeExists("friend", 2, 3).Value);
        Assert.True(net.CheckEdgeExists("work", 2, 3).Value);
        Assert.True(net.CheckEdgeExists("work", 1, 4).Value);
        Assert.False(net.CheckEdgeExists("work", 1, 2).Value);
    }

    [Fact]
    public void ImportOneModeEdgelist_FilterAndLabels_DoesNotAddNodesFromFilteredLines()
    {
        var net = EmptyNetwork();
        net.AddLayerOneMode("l", EdgeDirectionality.Directed, EdgeType.Valued, false);
        string path = WriteTempFile("a;b;1;x", "c;d;2;y", "a;c;3;x");

        FileManager.ImportOneModeEdgeList(path, net, (LayerOneMode)net.Layers["l"], 0, 1, 2, false, ';', true, "label", 3, "x");

        Assert.Equal(3, net.Nodeset.Count);   // 'd' only occurs on a filtered-out line
        Assert.Equal(3f, net.GetEdge("l", IdOf(net.Nodeset, "a"), IdOf(net.Nodeset, "c")).Value, precision: 4);
    }

    // ── 1-mode matrix with labels ─────────────────────────────────────────────

    [Fact]
    public void ImportOneModeMatrix_Labels_CreatesNodesAndEdges()
    {
        var net = EmptyNetwork();
        net.AddLayerOneMode("l", EdgeDirectionality.Directed, EdgeType.Valued, false);
        string path = WriteTempFile("\tx\ty\tz", "x\t0\t2\t0", "y\t0\t0\t3", "z\t1\t0\t0");

        var result = FileManager.ImportOneModeMatrix(path, net, (LayerOneMode)net.Layers["l"], '\t', true, "label");

        Assert.True(result.Success, result.Message);
        Assert.Equal(3, net.Nodeset.Count);
        uint x = IdOf(net.Nodeset, "x"), y = IdOf(net.Nodeset, "y"), z = IdOf(net.Nodeset, "z");
        Assert.Equal(2f, net.GetEdge("l", x, y).Value, precision: 4);
        Assert.Equal(3f, net.GetEdge("l", y, z).Value, precision: 4);
        Assert.Equal(1f, net.GetEdge("l", z, x).Value, precision: 4);
        Assert.False(net.CheckEdgeExists("l", y, x).Value);
    }

    [Fact]
    public void ImportOneModeMatrix_NumericIds_NonNumericHeader_Fails()
    {
        var net = EmptyNetwork();
        net.AddLayerOneMode("l", EdgeDirectionality.Directed, EdgeType.Binary, false);
        string path = WriteTempFile("\tx\ty", "x\t0\t1", "y\t1\t0");

        var result = FileManager.ImportOneModeMatrix(path, net, (LayerOneMode)net.Layers["l"], '\t', true);

        Assert.False(result.Success);
    }

    // ── 2-mode with labels and filter ─────────────────────────────────────────

    [Fact]
    public void ImportTwoModeEdgelist_LabelsAndFilter()
    {
        var net = EmptyNetwork();
        net.AddLayerTwoMode("clubs");
        string path = WriteTempFile("person\tclub\tyear", "anna\tchess\t2020", "bo\tchess\t2020", "cai\tgolf\t2021", "anna\tgolf\t2020");

        var result = FileManager.ImportTwoModeEdgeList(path, net, (LayerTwoMode)net.Layers["clubs"], 0, 1, true, '\t', true, "label", 2, "2020");

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, net.Nodeset.Count);
        var chess = net.GetHyperedgeNodes("clubs", "chess").Value!;
        Assert.Contains(IdOf(net.Nodeset, "anna"), chess);
        Assert.Contains(IdOf(net.Nodeset, "bo"), chess);
        var golf = net.GetHyperedgeNodes("clubs", "golf").Value!;
        Assert.Single(golf);
        Assert.Contains(IdOf(net.Nodeset, "anna"), golf);
    }

    [Fact]
    public void ImportTwoModeMatrix_Labels()
    {
        var net = EmptyNetwork();
        net.AddLayerTwoMode("clubs");
        string path = WriteTempFile("\tchess\tgolf", "anna\t1\t1", "bo\t1\t0", "cai\t0\t0");

        var result = FileManager.ImportTwoModeMatrix(path, net, (LayerTwoMode)net.Layers["clubs"], '\t', true, "label");

        Assert.True(result.Success, result.Message);
        Assert.Equal(3, net.Nodeset.Count);
        var chess = net.GetHyperedgeNodes("clubs", "chess").Value!;
        Assert.Equal(2, chess.Length);
        Assert.Contains(IdOf(net.Nodeset, "bo"), chess);
        var golf = net.GetHyperedgeNodes("clubs", "golf").Value!;
        Assert.Single(golf);
        Assert.Contains(IdOf(net.Nodeset, "anna"), golf);
    }
}
