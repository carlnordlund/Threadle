using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using Threadle.Core.Utilities.Enums;

namespace Threadle.Tests;

/// <summary>
/// Tests for Pajek .net import (FileManager.ImportNetwork) and export (FileManager.ExportNetworkToFile).
/// </summary>
public class PajekTests : IDisposable
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private readonly List<string> _tempFiles = [];

    private string TempFile(string extension = ".net")
    {
        string path = Path.Combine(Path.GetTempPath(), $"threadle_pajek_test_{Guid.NewGuid()}{extension}");
        _tempFiles.Add(path);
        return path;
    }

    private string WriteTempFile(string content)
    {
        string path = TempFile();
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f))
                File.Delete(f);
    }

    private static (Network Network, Nodeset Nodeset) Import(string path, string? labelAttr = "label")
    {
        var result = FileManager.ImportNetwork(path, ImportFormat.Pajek, labelAttr: labelAttr);
        Assert.True(result.Success, result.Message);
        var network = (Network)result.Value!.MainStructure;
        var nodeset = (Nodeset)result.Value.AdditionalStructures["nodeset"];
        Assert.Same(nodeset, network.Nodeset);
        return (network, nodeset);
    }

    private static object? Attr(Nodeset nodeset, uint nodeId, string attr)
    {
        var r = nodeset.GetNodeAttribute(nodeId, attr);
        if (!r.Success)
            return null;
        var (value, type) = r.Value;
        return type == NodeAttributeType.String ? nodeset.GetNodeAttributeString(nodeId, attr).Value : value.GetValue(type);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    [Fact]
    public void Import_BasicArcs_CreatesDirectedLayerWithEdges()
    {
        const string content = """
            *Vertices 3
            1 "Alice"
            2 "Bob"
            3 "Carol"
            *Arcs
            1 2
            2 3
            """;
        var (net, ns) = Import(WriteTempFile(content));

        Assert.Equal(3, ns.Count);
        Assert.Single(net.Layers);
        var layer = Assert.IsAssignableFrom<ILayerOneMode>(net.Layers.Values.First());
        Assert.True(layer.IsDirectional);
        Assert.True(layer.IsBinary);
        Assert.True(net.CheckEdgeExists(layer.Name, 1, 2).Value);
        Assert.False(net.CheckEdgeExists(layer.Name, 2, 1).Value);
        Assert.True(net.CheckEdgeExists(layer.Name, 2, 3).Value);
    }

    [Fact]
    public void Import_VertexLabels_StoredInLabelAttribute()
    {
        const string content = """
            *Vertices 2
            1 "Alice"
            2 "Bob"
            *Edges
            1 2
            """;
        var (_, ns) = Import(WriteTempFile(content));

        Assert.Equal("Alice", Attr(ns, 1, "label"));
        Assert.Equal("Bob", Attr(ns, 2, "label"));
    }

    [Fact]
    public void Import_LabelAttrEmpty_DiscardsLabels()
    {
        const string content = """
            *Vertices 2
            1 "Alice"
            2 "Bob"
            *Edges
            1 2
            """;
        var (_, ns) = Import(WriteTempFile(content), labelAttr: "");

        Assert.Null(Attr(ns, 1, "label"));
    }

    [Fact]
    public void Import_WeightedEdges_LayerIsValued()
    {
        const string content = """
            *Vertices 3
            1 "A"
            2 "B"
            3 "C"
            *Arcs
            1 2 2.5
            2 3 1
            """;
        var (net, _) = Import(WriteTempFile(content));

        var layer = Assert.IsAssignableFrom<ILayerOneMode>(net.Layers.Values.First());
        Assert.True(layer.IsValued);
        Assert.Equal(2.5f, net.GetEdge(layer.Name, 1, 2).Value, precision: 4);
    }

    [Fact]
    public void Import_SelfLoop_SetsSelftiesTrue()
    {
        const string content = """
            *Vertices 2
            1 "A"
            2 "B"
            *Edges
            1 1
            1 2
            """;
        var (net, _) = Import(WriteTempFile(content));

        var layer = Assert.IsAssignableFrom<ILayerOneMode>(net.Layers.Values.First());
        Assert.False(layer.IsDirectional);
        Assert.True(layer.Selfties);
        Assert.True(net.CheckEdgeExists(layer.Name, 1, 1).Value);
    }

    [Fact]
    public void Import_MultipleNamedSections_CreatesOneLayerPerSection()
    {
        const string content = """
            *Vertices 4
            1 "Alice"
            2 "Bob"
            3 "Carol"
            4 "Dave"
            *Arcs :1 "reports_to"
            1 2
            2 3
            *Edges :2 "friends"
            1 2
            3 4
            """;
        var (net, _) = Import(WriteTempFile(content));

        Assert.Equal(2, net.Layers.Count);
        var reportsTo = Assert.IsAssignableFrom<ILayerOneMode>(net.Layers["reports_to"]);
        Assert.True(reportsTo.IsDirectional);
        var friends = Assert.IsAssignableFrom<ILayerOneMode>(net.Layers["friends"]);
        Assert.False(friends.IsDirectional);
        Assert.True(net.CheckEdgeExists("friends", 3, 4).Value);
    }

    [Fact]
    public void Import_UnknownSection_IsSkippedWithoutAffectingLaterEdges()
    {
        // *Partition isn't a recognized section: its body lines ('1 1', '2 2') look like edge
        // lines but must be ignored, not misread as edges of a dangling section.
        const string content = """
            *Vertices 2
            1 "A"
            2 "B"
            *Partition some_partition
            1 1
            2 2
            *Arcs
            1 2
            """;
        var (net, _) = Import(WriteTempFile(content));

        var layer = Assert.IsAssignableFrom<ILayerOneMode>(net.Layers.Values.First());
        Assert.Equal(1ul, layer.NbrEdges);
        Assert.True(net.CheckEdgeExists(layer.Name, 1, 2).Value);
    }

    [Fact]
    public void Import_ArcslistSection_Fails()
    {
        const string content = """
            *Vertices 2
            1 "A"
            2 "B"
            *Arcslist
            1 2
            """;
        var result = FileManager.ImportNetwork(WriteTempFile(content), ImportFormat.Pajek);
        Assert.False(result.Success);
    }

    [Fact]
    public void Import_MissingVerticesSection_Fails()
    {
        const string content = """
            *Arcs
            1 2
            """;
        var result = FileManager.ImportNetwork(WriteTempFile(content), ImportFormat.Pajek);
        Assert.False(result.Success);
    }

    [Fact]
    public void Import_MissingFile_Fails()
    {
        var result = FileManager.ImportNetwork(TempFile(), ImportFormat.Pajek);
        Assert.False(result.Success);
    }

    // ── Export and round trip ────────────────────────────────────────────────

    private static Network MakeMultilayerNetwork()
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 5; i++)
            ns.AddNode(i);

        var net = new Network("multi", ns);
        net.AddLayerOneMode("follows", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("follows", 1, 2);
        net.AddEdge("follows", 3, 1);
        net.AddLayerOneMode("trust", EdgeDirectionality.Undirected, EdgeType.Valued, true);
        net.AddEdge("trust", 1, 2, value: 0.5f);
        net.AddEdge("trust", 2, 4, value: 1.5f);
        net.AddEdge("trust", 4, 4, value: 2f);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "club1", new uint[] { 1, 2, 3 });
        return net;
    }

    [Fact]
    public void Export_AllLayers_RoundTrip()
    {
        var net = MakeMultilayerNetwork();
        string path = TempFile();
        var exportResult = FileManager.ExportNetworkToFile(net, ExportFormat.Pajek, "", path);
        Assert.True(exportResult.Success, exportResult.Message);

        var (loaded, ns) = Import(path);

        // Nodes: Pajek .net always renumbers to a dense 1..N range, so only the count round-trips.
        Assert.Equal(5, ns.Count);

        // Two-mode layer is skipped; only the two 1-mode layers come back.
        Assert.Equal(2, loaded.Layers.Count);
        var follows = Assert.IsAssignableFrom<ILayerOneMode>(loaded.Layers["follows"]);
        Assert.True(follows.IsDirectional);
        Assert.True(follows.IsBinary);
        Assert.True(loaded.CheckEdgeExists("follows", 1, 2).Value);
        Assert.False(loaded.CheckEdgeExists("follows", 2, 1).Value);
        Assert.True(loaded.CheckEdgeExists("follows", 3, 1).Value);

        var trust = Assert.IsAssignableFrom<ILayerOneMode>(loaded.Layers["trust"]);
        Assert.False(trust.IsDirectional);
        Assert.True(trust.IsValued);
        Assert.True(trust.Selfties);
        Assert.Equal(((ILayerOneMode)net.Layers["trust"]).NbrEdges, trust.NbrEdges);
        Assert.Equal(0.5f, loaded.GetEdge("trust", 2, 1).Value, precision: 4);
        Assert.Equal(1.5f, loaded.GetEdge("trust", 2, 4).Value, precision: 4);
        Assert.Equal(2f, loaded.GetEdge("trust", 4, 4).Value, precision: 4);
    }

    [Fact]
    public void Export_OriginalNodeId_WrittenAsLabel()
    {
        var net = MakeMultilayerNetwork();
        string path = TempFile();
        FileManager.ExportNetworkToFile(net, ExportFormat.Pajek, "", path);

        var (_, ns) = Import(path);
        // Pajek ids are a fresh dense 1..5 sequence, but the original Threadle id (1..5, unchanged
        // here since the source ids were already dense from 1) is recoverable via the label.
        var originalIds = ns.NodeIdArray.Select(id => Attr(ns, id, "label")).OrderBy(l => l).ToArray();
        Assert.Equal(new object?[] { "1", "2", "3", "4", "5" }, originalIds);
    }

    [Fact]
    public void Export_TwoModeOnlyLayer_StillExportsVerticesButNoEdgeSection()
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 3; i++)
            ns.AddNode(i);
        var net = new Network("twomode_only", ns);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "club1", new uint[] { 1, 2 });
        string path = TempFile();

        var exportResult = FileManager.ExportNetworkToFile(net, ExportFormat.Pajek, "", path);
        Assert.True(exportResult.Success, exportResult.Message);

        string[] lines = File.ReadAllLines(path);
        Assert.Equal("*Vertices 3", lines[0]);
        Assert.DoesNotContain(lines, l => l.StartsWith("*Arcs") || l.StartsWith("*Edges"));
    }

    [Fact]
    public void Export_MultipleOneModeLayers_UsesNamedSections()
    {
        var net = MakeMultilayerNetwork();
        string path = TempFile();
        FileManager.ExportNetworkToFile(net, ExportFormat.Pajek, "", path);

        string[] lines = File.ReadAllLines(path);
        Assert.Contains(lines, l => l.StartsWith("*Arcs :") && l.Contains("\"follows\""));
        Assert.Contains(lines, l => l.StartsWith("*Edges :") && l.Contains("\"trust\""));
    }

    [Fact]
    public void Export_SingleOneModeLayer_OmitsSectionSuffix()
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 3; i++)
            ns.AddNode(i);
        var net = new Network("single", ns);
        net.AddLayerOneMode("onlylayer", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("onlylayer", 1, 2);
        string path = TempFile();

        FileManager.ExportNetworkToFile(net, ExportFormat.Pajek, "", path);

        string[] lines = File.ReadAllLines(path);
        Assert.Contains("*Arcs", lines);
    }

    [Fact]
    public void Export_LayerNameIgnored_WholeNetworkStillExported()
    {
        var net = MakeMultilayerNetwork();
        string path = TempFile();

        // Pajek export is always whole-network: an explicit (and here nonexistent) layername
        // argument is simply ignored rather than causing a failure.
        var exportResult = FileManager.ExportNetworkToFile(net, ExportFormat.Pajek, "nonexistent-layer", path);
        Assert.True(exportResult.Success, exportResult.Message);

        var (loaded, _) = Import(path);
        Assert.Equal(2, loaded.Layers.Count);
    }
}