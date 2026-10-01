using System.Xml.Linq;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using Threadle.Core.Utilities.Enums;

namespace Threadle.Tests;

/// <summary>
/// Tests for GraphML import (FileManager.ImportNetwork) and export (FileManager.ExportNetworkToFile).
/// </summary>
public class GraphmlTests : IDisposable
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private readonly List<string> _tempFiles = [];

    private string TempFile(string extension = ".graphml")
    {
        string path = Path.Combine(Path.GetTempPath(), $"threadle_graphml_test_{Guid.NewGuid()}{extension}");
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

    private static (Network Network, Nodeset Nodeset) Import(string path, string? layerAttr = "layer", string? weightAttr = "weight", string idAttr = "id")
    {
        var result = FileManager.ImportNetwork(path, ImportFormat.Graphml, layerAttr, weightAttr, idAttr);
        Assert.True(result.Success, result.Message);
        var network = (Network)result.Value!.MainStructure;
        var nodeset = (Nodeset)result.Value.AdditionalStructures["nodeset"];
        Assert.Same(nodeset, network.Nodeset);
        return (network, nodeset);
    }

    /// <summary>Returns the node id whose string attribute 'attr' equals 'value'.</summary>
    private static uint IdOf(Nodeset nodeset, string value, string attr = "id")
    {
        var ids = nodeset.NodeIdArray.Where(id =>
        {
            var r = nodeset.GetNodeAttributeString(id, attr);
            return r.Success && r.Value == value;
        }).ToList();
        Assert.Single(ids);
        return ids[0];
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

    private const string NetworkxStyle = """
        <?xml version='1.0' encoding='utf-8'?>
        <graphml xmlns="http://graphml.graphdrawing.org/xmlns" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <key id="d0" for="node" attr.name="age" attr.type="long"/>
          <key id="d1" for="node" attr.name="score" attr.type="double"/>
          <key id="d2" for="node" attr.name="member" attr.type="boolean"><default>false</default></key>
          <key id="d3" for="node" attr.name="name" attr.type="string"/>
          <key id="d4" for="edge" attr.name="weight" attr.type="double"/>
          <graph id="friends" edgedefault="undirected">
            <node id="alice"><data key="d0">31</data><data key="d1">0.5</data><data key="d2">true</data><data key="d3">Alice A.</data></node>
            <node id="bob"><data key="d0">29</data></node>
            <node id="carol"/>
            <edge source="alice" target="bob"><data key="d4">2.5</data></edge>
            <edge source="bob" target="carol"><data key="d4">1</data></edge>
          </graph>
        </graphml>
        """;

    [Fact]
    public void Import_StringIds_NodesGetNewIdsWithIdAttribute()
    {
        var (_, ns) = Import(WriteTempFile(NetworkxStyle));

        Assert.Equal(3, ns.Count);
        Assert.Equal(0u, IdOf(ns, "alice"));
        Assert.Equal(1u, IdOf(ns, "bob"));
        Assert.Equal(2u, IdOf(ns, "carol"));
    }

    [Fact]
    public void Import_NodeAttributes_TypesValuesAndDefaults()
    {
        var (_, ns) = Import(WriteTempFile(NetworkxStyle));
        uint alice = IdOf(ns, "alice"), bob = IdOf(ns, "bob"), carol = IdOf(ns, "carol");

        Assert.Equal(31, Attr(ns, alice, "age"));
        Assert.Equal(0.5f, Attr(ns, alice, "score"));
        Assert.Equal(true, Attr(ns, alice, "member"));
        Assert.Equal("Alice A.", Attr(ns, alice, "name"));
        Assert.Equal(29, Attr(ns, bob, "age"));
        Assert.Null(Attr(ns, bob, "score"));
        Assert.Equal(false, Attr(ns, bob, "member"));    // key default
        Assert.Equal(false, Attr(ns, carol, "member"));  // key default
    }

    [Fact]
    public void Import_UndirectedWeighted_SingleValuedLayerNamedAfterGraph()
    {
        var (net, ns) = Import(WriteTempFile(NetworkxStyle));

        Assert.Single(net.Layers);
        var layer = Assert.IsAssignableFrom<ILayerOneMode>(net.Layers["friends"]);
        Assert.False(layer.IsDirectional);
        Assert.True(layer.IsValued);
        uint alice = IdOf(ns, "alice"), bob = IdOf(ns, "bob"), carol = IdOf(ns, "carol");
        Assert.Equal(2.5f, net.GetEdge("friends", alice, bob).Value, precision: 4);
        Assert.Equal(2.5f, net.GetEdge("friends", bob, alice).Value, precision: 4);
        Assert.Equal(1f, net.GetEdge("friends", carol, bob).Value, precision: 4);
        Assert.False(net.CheckEdgeExists("friends", alice, carol).Value);
    }

    [Fact]
    public void Import_NumericIds_UsedAsNodeIds_BinaryDirected()
    {
        string path = WriteTempFile("""
            <graphml xmlns="http://graphml.graphdrawing.org/xmlns">
              <graph edgedefault="directed">
                <node id="10"/><node id="20"/><node id="30"/>
                <edge source="10" target="20"/>
                <edge source="30" target="10"/>
              </graph>
            </graphml>
            """);

        var (net, ns) = Import(path);

        Assert.Equal(new uint[] { 10, 20, 30 }, ns.NodeIdArray.OrderBy(i => i).ToArray());
        Assert.False(ns.GetNodeAttribute(10, "id").Success);
        var layer = Assert.IsAssignableFrom<ILayerOneMode>(net.Layers["edges"]);
        Assert.True(layer.IsDirectional);
        Assert.True(layer.IsBinary);
        Assert.True(net.CheckEdgeExists("edges", 10, 20).Value);
        Assert.False(net.CheckEdgeExists("edges", 20, 10).Value);
        Assert.True(net.CheckEdgeExists("edges", 30, 10).Value);
    }

    [Fact]
    public void Import_LayerAttribute_SplitsEdgesIntoLayers()
    {
        string path = WriteTempFile("""
            <graphml xmlns="http://graphml.graphdrawing.org/xmlns">
              <key id="t" for="edge" attr.name="type" attr.type="string"/>
              <key id="w" for="edge" attr.name="weight" attr.type="double"/>
              <graph id="g" edgedefault="directed">
                <node id="a"/><node id="b"/><node id="c"/>
                <edge source="a" target="b"><data key="t">chem</data><data key="w">3</data></edge>
                <edge source="b" target="c"><data key="t">chem</data><data key="w">4</data></edge>
                <edge source="a" target="c" directed="false"><data key="t">elec</data></edge>
                <edge source="c" target="c"><data key="t">elec</data></edge>
              </graph>
            </graphml>
            """);

        var (net, ns) = Import(path, layerAttr: "type");
        uint a = IdOf(ns, "a"), b = IdOf(ns, "b"), c = IdOf(ns, "c");

        Assert.Equal(2, net.Layers.Count);
        var chem = Assert.IsAssignableFrom<ILayerOneMode>(net.Layers["chem"]);
        Assert.True(chem.IsDirectional);
        Assert.True(chem.IsValued);
        Assert.False(chem.Selfties);
        Assert.Equal(4f, net.GetEdge("chem", b, c).Value, precision: 4);

        // Mixed directed/undirected edges: directed layer, undirected edge added in both directions
        var elec = Assert.IsAssignableFrom<ILayerOneMode>(net.Layers["elec"]);
        Assert.True(elec.IsDirectional);
        Assert.True(elec.IsBinary);
        Assert.True(elec.Selfties);
        Assert.True(net.CheckEdgeExists("elec", a, c).Value);
        Assert.True(net.CheckEdgeExists("elec", c, a).Value);
        Assert.True(net.CheckEdgeExists("elec", c, c).Value);
    }

    [Fact]
    public void Import_EmptyLayerAttr_AllEdgesInOneLayer()
    {
        string path = WriteTempFile("""
            <graphml xmlns="http://graphml.graphdrawing.org/xmlns">
              <key id="t" for="edge" attr.name="layer" attr.type="string"/>
              <graph edgedefault="undirected">
                <node id="1"/><node id="2"/><node id="3"/>
                <edge source="1" target="2"><data key="t">x</data></edge>
                <edge source="2" target="3"><data key="t">y</data></edge>
              </graph>
            </graphml>
            """);

        var (net, _) = Import(path, layerAttr: "");

        Assert.Single(net.Layers);
        Assert.True(net.CheckEdgeExists("edges", 1, 2).Value);
        Assert.True(net.CheckEdgeExists("edges", 2, 3).Value);
    }

    [Fact]
    public void Import_EdgesBeforeNodes_UndeclaredNodesAndYfilesData()
    {
        string path = WriteTempFile("""
            <graphml xmlns="http://graphml.graphdrawing.org/xmlns" xmlns:y="http://www.yworks.com/xml/graphml">
              <key id="g" for="node" yfiles.type="nodegraphics"/>
              <key id="l" for="node" attr.name="label" attr.type="string"/>
              <graph edgedefault="undirected">
                <edge source="n0" target="n1"/>
                <edge source="n1" target="n2"/>
                <node id="n0"><data key="g"><y:ShapeNode><y:Fill color="#FFCC00"/></y:ShapeNode></data><data key="l">zero</data></node>
                <node id="n1"><data key="l">one</data></node>
              </graph>
            </graphml>
            """);

        var (net, ns) = Import(path);

        Assert.Equal(3, ns.Count);   // n2 is only referenced by an edge
        uint n0 = IdOf(ns, "n0"), n1 = IdOf(ns, "n1"), n2 = IdOf(ns, "n2");
        Assert.Equal("zero", Attr(ns, n0, "label"));
        Assert.Equal("one", Attr(ns, n1, "label"));
        Assert.True(net.CheckEdgeExists("edges", n0, n1).Value);
        Assert.True(net.CheckEdgeExists("edges", n1, n2).Value);
        Assert.False(ns.GetNodeAttribute(n0, "g").Success);
    }

    [Fact]
    public void Import_Hyperedges_TwoModeLayer()
    {
        string path = WriteTempFile("""
            <graphml xmlns="http://graphml.graphdrawing.org/xmlns">
              <graph id="clubs" edgedefault="undirected">
                <node id="1"/><node id="2"/><node id="3"/>
                <hyperedge id="chess"><endpoint node="1"/><endpoint node="2"/></hyperedge>
                <hyperedge><endpoint node="2"/><endpoint node="3"/></hyperedge>
              </graph>
            </graphml>
            """);

        var (net, _) = Import(path);

        var layer = Assert.IsAssignableFrom<ILayerTwoMode>(net.Layers["clubs"]);
        Assert.Equal(2u, layer.NbrHyperedges);
        Assert.Equal(new uint[] { 1, 2 }, net.GetHyperedgeNodes("clubs", "chess").Value!.OrderBy(i => i).ToArray());
        Assert.Equal(new uint[] { 2, 3 }, net.GetHyperedgeNodes("clubs", "h1").Value!.OrderBy(i => i).ToArray());
    }

    [Fact]
    public void Import_IdAttributeClash_Fails()
    {
        string path = WriteTempFile("""
            <graphml xmlns="http://graphml.graphdrawing.org/xmlns">
              <key id="d0" for="node" attr.name="id" attr.type="string"/>
              <graph edgedefault="undirected"><node id="a"/><node id="b"/><edge source="a" target="b"/></graph>
            </graphml>
            """);

        Assert.False(FileManager.ImportNetwork(path, ImportFormat.Graphml).Success);
        var (_, ns) = Import(path, idAttr: "graphml_id");
        Assert.Equal(2, ns.Count);
    }

    [Fact]
    public void Import_NotGraphml_Fails()
    {
        string path = WriteTempFile("<html><body/></html>");
        Assert.False(FileManager.ImportNetwork(path, ImportFormat.Graphml).Success);
        Assert.False(FileManager.ImportNetwork(TempFile(), ImportFormat.Graphml).Success);   // missing file
    }

    // ── Export and round trip ─────────────────────────────────────────────────

    private static Network MakeMultilayerNetwork()
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 5; i++)
            ns.AddNode(i);
        ns.DefineNodeAttribute("age", "int");
        ns.DefineNodeAttribute("score", "float");
        ns.DefineNodeAttribute("member", "bool");
        ns.DefineNodeAttribute("name", "string");
        ns.SetNodeAttribute(1, "age", "40");
        ns.SetNodeAttribute(1, "score", "0.25");
        ns.SetNodeAttribute(1, "member", "true");
        ns.SetNodeAttribute(1, "name", "Ann & <Bo>");
        ns.SetNodeAttribute(2, "member", "false");

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
        net.AddHyperedge("clubs", "club2", new uint[] { 2, 5 });
        return net;
    }

    [Fact]
    public void Export_AllLayers_RoundTrip()
    {
        var net = MakeMultilayerNetwork();
        string path = TempFile();
        var exportResult = FileManager.ExportNetworkToFile(net, ExportFormat.Graphml, "", path);
        Assert.True(exportResult.Success, exportResult.Message);

        var (loaded, ns) = Import(path);

        // Nodes and attributes
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5 }, ns.NodeIdArray.OrderBy(i => i).ToArray());
        Assert.Equal(40, Attr(ns, 1, "age"));
        Assert.Equal(0.25f, Attr(ns, 1, "score"));
        Assert.Equal(true, Attr(ns, 1, "member"));
        Assert.Equal(false, Attr(ns, 2, "member"));
        Assert.Equal("Ann & <Bo>", Attr(ns, 1, "name"));
        Assert.Null(Attr(ns, 3, "age"));

        // Layers and their properties
        Assert.Equal(3, loaded.Layers.Count);
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

        Assert.IsAssignableFrom<ILayerTwoMode>(loaded.Layers["clubs"]);
        Assert.Equal(new uint[] { 1, 2, 3 }, loaded.GetHyperedgeNodes("clubs", "club1").Value!.OrderBy(i => i).ToArray());
        Assert.Equal(new uint[] { 2, 5 }, loaded.GetHyperedgeNodes("clubs", "club2").Value!.OrderBy(i => i).ToArray());
    }

    [Fact]
    public void Export_UndirectedEdgesWrittenOnce()
    {
        var net = MakeMultilayerNetwork();
        string path = TempFile();
        FileManager.ExportNetworkToFile(net, ExportFormat.Graphml, "trust", path);

        XNamespace ns = "http://graphml.graphdrawing.org/xmlns";
        var doc = XDocument.Load(path);
        var graph = doc.Root!.Element(ns + "graph")!;
        Assert.Equal("trust", graph.Attribute("id")!.Value);
        Assert.Equal("undirected", graph.Attribute("edgedefault")!.Value);
        Assert.Equal(3, graph.Elements(ns + "edge").Count());
        Assert.Equal(5, graph.Elements(ns + "node").Count());
        Assert.Empty(graph.Elements(ns + "hyperedge"));
    }

    [Fact]
    public void Export_SingleLayer_RoundTripKeepsLayerName()
    {
        var net = MakeMultilayerNetwork();
        string path = TempFile();
        FileManager.ExportNetworkToFile(net, ExportFormat.Graphml, "follows", path);

        var (loaded, _) = Import(path);

        Assert.Single(loaded.Layers);
        Assert.True(loaded.CheckEdgeExists("follows", 3, 1).Value);
        Assert.False(loaded.CheckEdgeExists("follows", 1, 3).Value);
    }

    [Fact]
    public void Export_UnknownLayer_Fails()
    {
        var net = MakeMultilayerNetwork();
        Assert.False(FileManager.ExportNetworkToFile(net, ExportFormat.Graphml, "nope", TempFile()).Success);
    }
}
