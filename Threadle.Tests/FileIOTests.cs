using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// Round-trip tests for FileManager: save a structure to a temp file, reload it,
/// and verify that nodes, layers, edges, and attributes survive serialization.
/// Temp files are cleaned up automatically via IDisposable helpers.
/// </summary>
public class FileIOTests : IDisposable
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    // Track temp files created during tests so they are deleted on Dispose.
    private readonly List<string> _tempFiles = [];

    /// <summary>Returns a unique temp filepath with the given extension (e.g. ".tsv").</summary>
    private string TempFile(string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), $"threadle_test_{Guid.NewGuid()}{extension}");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f))
                File.Delete(f);
    }

    /// <summary>Creates a small nodeset with nodes 1–5.</summary>
    private static Nodeset MakeNodeset()
    {
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 5; i++)
            ns.AddNode(i);
        return ns;
    }

    /// <summary>Creates a small nodeset with an undirected binary layer and a couple of edges.</summary>
    private static Network MakeNetwork(Nodeset nodeset)
    {
        var net = new Network("test-net", nodeset);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.AddEdge("friends", 2, 3);
        return net;
    }

    // ── Nodeset save/load (TSV) ───────────────────────────────────────────────

    [Fact]
    public void SaveLoadNodeset_Tsv_Succeeds()
    {
        var ns = MakeNodeset();
        string path = TempFile(".tsv");

        var saveResult = FileManager.Save(ns, path);
        Assert.True(saveResult.Success);

        var loadResult = FileManager.Load(path, "nodeset");
        Assert.True(loadResult.Success);
    }

    [Fact]
    public void SaveLoadNodeset_Tsv_NodeCountPreserved()
    {
        var ns = MakeNodeset();
        string path = TempFile(".tsv");
        FileManager.Save(ns, path);

        var loadResult = FileManager.Load(path, "nodeset");
        var loaded = (Nodeset)loadResult.Value!.MainStructure;
        Assert.Equal(ns.NodeIdArray.Length, loaded.NodeIdArray.Length);
    }

    [Fact]
    public void SaveLoadNodeset_Tsv_NodeIdsPreserved()
    {
        var ns = MakeNodeset();
        string path = TempFile(".tsv");
        FileManager.Save(ns, path);

        var loadResult = FileManager.Load(path, "nodeset");
        var loaded = (Nodeset)loadResult.Value!.MainStructure;
        foreach (uint id in ns.NodeIdArray)
            Assert.Contains(id, loaded.NodeIdArray);
    }

    // ── Nodeset save/load (BIN) ───────────────────────────────────────────────

    [Fact]
    public void SaveLoadNodeset_Bin_Succeeds()
    {
        var ns = MakeNodeset();
        string path = TempFile(".bin");

        var saveResult = FileManager.Save(ns, path);
        Assert.True(saveResult.Success);

        var loadResult = FileManager.Load(path, "nodeset");
        Assert.True(loadResult.Success);
    }

    [Fact]
    public void SaveLoadNodeset_Bin_NodeIdsPreserved()
    {
        var ns = MakeNodeset();
        string path = TempFile(".bin");
        FileManager.Save(ns, path);

        var loadResult = FileManager.Load(path, "nodeset");
        var loaded = (Nodeset)loadResult.Value!.MainStructure;
        foreach (uint id in ns.NodeIdArray)
            Assert.Contains(id, loaded.NodeIdArray);
    }

    // ── Nodeset save/load (TSV.GZ) ────────────────────────────────────────────

    [Fact]
    public void SaveLoadNodeset_TsvGz_NodeIdsPreserved()
    {
        var ns = MakeNodeset();
        string path = TempFile(".tsv.gz");
        FileManager.Save(ns, path);

        var loadResult = FileManager.Load(path, "nodeset");
        Assert.True(loadResult.Success);
        var loaded = (Nodeset)loadResult.Value!.MainStructure;
        foreach (uint id in ns.NodeIdArray)
            Assert.Contains(id, loaded.NodeIdArray);
    }

    // ── Nodeset attributes round-trip (TSV) ────────────────────────────────────

    [Fact]
    public void SaveLoadNodeset_IntAttribute_Preserved()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("age", "int");
        ns.SetNodeAttribute(1, "age", "42");
        ns.SetNodeAttribute(2, "age", "17");

        string path = TempFile(".tsv");
        FileManager.Save(ns, path);

        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;
        var r1 = loaded.GetNodeAttribute(1, "age");
        Assert.True(r1.Success);
        var (nav1, type1) = r1.Value;
        Assert.Equal(42, (int)nav1.GetValue(type1));

        var r2 = loaded.GetNodeAttribute(2, "age");
        var (nav2, type2) = r2.Value;
        Assert.Equal(17, (int)nav2.GetValue(type2));
    }

    [Fact]
    public void SaveLoadNodeset_FloatAttribute_Preserved()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("score", "float");
        ns.SetNodeAttribute(1, "score", "3.14");

        string path = TempFile(".tsv");
        FileManager.Save(ns, path);

        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;
        var r = loaded.GetNodeAttribute(1, "score");
        Assert.True(r.Success);
        var (nav, type) = r.Value;
        Assert.Equal(3.14f, (float)nav.GetValue(type), precision: 4);
    }

    [Fact]
    public void SaveLoadNodeset_BoolAttribute_Preserved()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("active", "bool");
        ns.SetNodeAttribute(1, "active", "true");
        ns.SetNodeAttribute(2, "active", "false");

        string path = TempFile(".tsv");
        FileManager.Save(ns, path);

        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        var r1 = loaded.GetNodeAttribute(1, "active");
        var (nav1, type1) = r1.Value;
        Assert.True((bool)nav1.GetValue(type1));

        var r2 = loaded.GetNodeAttribute(2, "active");
        var (nav2, type2) = r2.Value;
        Assert.False((bool)nav2.GetValue(type2));
    }

    [Fact]
    public void SaveLoadNodeset_CharAttribute_Preserved()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("gender", "char");
        ns.SetNodeAttribute(1, "gender", "m");
        ns.SetNodeAttribute(2, "gender", "f");

        string path = TempFile(".tsv");
        FileManager.Save(ns, path);

        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        var r1 = loaded.GetNodeAttribute(1, "gender");
        var (nav1, type1) = r1.Value;
        Assert.Equal('m', (char)nav1.GetValue(type1));

        var r2 = loaded.GetNodeAttribute(2, "gender");
        var (nav2, type2) = r2.Value;
        Assert.Equal('f', (char)nav2.GetValue(type2));
    }

    [Fact]
    public void SaveLoadNodeset_MultipleAttributes_AllPreserved()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("age", "int");
        ns.DefineNodeAttribute("score", "float");
        ns.DefineNodeAttribute("active", "bool");
        ns.SetNodeAttribute(1, "age", "30");
        ns.SetNodeAttribute(1, "score", "9.5");
        ns.SetNodeAttribute(1, "active", "true");

        string path = TempFile(".tsv");
        FileManager.Save(ns, path);

        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        var (navAge, tAge) = loaded.GetNodeAttribute(1, "age").Value;
        Assert.Equal(30, (int)navAge.GetValue(tAge));

        var (navScore, tScore) = loaded.GetNodeAttribute(1, "score").Value;
        Assert.Equal(9.5f, (float)navScore.GetValue(tScore), precision: 4);

        var (navActive, tActive) = loaded.GetNodeAttribute(1, "active").Value;
        Assert.True((bool)navActive.GetValue(tActive));
    }

    // ── Attributes round-trip (BIN) ────────────────────────────────────────────

    [Fact]
    public void SaveLoadNodeset_Bin_IntAttribute_Preserved()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("score", "int");
        ns.SetNodeAttribute(3, "score", "99");

        string path = TempFile(".bin");
        FileManager.Save(ns, path);

        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;
        var (nav, type) = loaded.GetNodeAttribute(3, "score").Value;
        Assert.Equal(99, (int)nav.GetValue(type));
    }

    [Fact]
    public void SaveLoadNodeset_Bin_FloatAttribute_Preserved()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("score", "float");
        ns.SetNodeAttribute(1, "score", "3.14");

        string path = TempFile(".bin");
        FileManager.Save(ns, path);

        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;
        var r = loaded.GetNodeAttribute(1, "score");
        Assert.True(r.Success);
        var (nav, type) = r.Value;
        Assert.Equal(3.14f, (float)nav.GetValue(type), precision: 4);
    }

    [Fact]
    public void SaveLoadNodeset_Bin_BoolAttribute_Preserved()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("active", "bool");
        ns.SetNodeAttribute(1, "active", "true");
        ns.SetNodeAttribute(2, "active", "false");

        string path = TempFile(".bin");
        FileManager.Save(ns, path);

        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        var r1 = loaded.GetNodeAttribute(1, "active");
        Assert.True(r1.Success);
        var (nav1, type1) = r1.Value;
        Assert.True((bool)nav1.GetValue(type1));

        var r2 = loaded.GetNodeAttribute(2, "active");
        var (nav2, type2) = r2.Value;
        Assert.False((bool)nav2.GetValue(type2));
    }

    [Fact]
    public void SaveLoadNodeset_Bin_CharAttribute_Preserved()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("gender", "char");
        ns.SetNodeAttribute(1, "gender", "m");
        ns.SetNodeAttribute(2, "gender", "f");

        string path = TempFile(".bin");
        FileManager.Save(ns, path);

        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        var r1 = loaded.GetNodeAttribute(1, "gender");
        Assert.True(r1.Success);
        var (nav1, type1) = r1.Value;
        Assert.Equal('m', (char)nav1.GetValue(type1));

        var r2 = loaded.GetNodeAttribute(2, "gender");
        var (nav2, type2) = r2.Value;
        Assert.Equal('f', (char)nav2.GetValue(type2));
    }

    [Fact]
    public void SaveLoadNodeset_Bin_MultipleAttributes_AllPreserved()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("age", "int");
        ns.DefineNodeAttribute("score", "float");
        ns.DefineNodeAttribute("active", "bool");
        ns.SetNodeAttribute(1, "age", "30");
        ns.SetNodeAttribute(1, "score", "9.5");
        ns.SetNodeAttribute(1, "active", "true");

        string path = TempFile(".bin");
        FileManager.Save(ns, path);

        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        var (navAge, tAge) = loaded.GetNodeAttribute(1, "age").Value;
        Assert.Equal(30, (int)navAge.GetValue(tAge));

        var (navScore, tScore) = loaded.GetNodeAttribute(1, "score").Value;
        Assert.Equal(9.5f, (float)navScore.GetValue(tScore), precision: 4);

        var (navActive, tActive) = loaded.GetNodeAttribute(1, "active").Value;
        Assert.True((bool)navActive.GetValue(tActive));
    }

    // ── Network save/load (TSV) ────────────────────────────────────────────────

    [Fact]
    public void SaveLoadNetwork_Tsv_Succeeds()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = MakeNetwork(ns);
        string netPath = TempFile(".tsv");

        var saveResult = FileManager.Save(net, netPath);
        Assert.True(saveResult.Success);

        var loadResult = FileManager.Load(netPath, "network");
        Assert.True(loadResult.Success);
    }

    [Fact]
    public void SaveLoadNetwork_Tsv_LayerPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = MakeNetwork(ns);
        string netPath = TempFile(".tsv");
        FileManager.Save(net, netPath);

        var loadResult = FileManager.Load(netPath, "network");
        var loadedNet = (Network)loadResult.Value!.MainStructure;
        Assert.True(loadedNet.Layers.ContainsKey("friends"));
    }

    [Fact]
    public void SaveLoadNetwork_Tsv_EdgesPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = MakeNetwork(ns);
        string netPath = TempFile(".tsv");
        FileManager.Save(net, netPath);

        var loadResult = FileManager.Load(netPath, "network");
        var loadedNet = (Network)loadResult.Value!.MainStructure;
        var edgeCheck = loadedNet.CheckEdgeExists("friends", 1, 2);
        Assert.True(edgeCheck.Success);
        Assert.True(edgeCheck.Value);
    }

    // ── Network save/load (BIN) ────────────────────────────────────────────────

    [Fact]
    public void SaveLoadNetwork_Bin_EdgesPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = MakeNetwork(ns);
        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadResult = FileManager.Load(netPath, "network");
        Assert.True(loadResult.Success);
        var loadedNet = (Network)loadResult.Value!.MainStructure;
        var edgeCheck = loadedNet.CheckEdgeExists("friends", 1, 2);
        Assert.True(edgeCheck.Value);
    }

    // ── Network requires nodeset saved first ───────────────────────────────────

    [Fact]
    public void SaveNetwork_WithoutSavedNodeset_Fails()
    {
        var ns = MakeNodeset();  // NOT saved to file — Filepath is null
        var net = MakeNetwork(ns);
        string netPath = TempFile(".tsv");

        var saveResult = FileManager.Save(net, netPath);
        Assert.False(saveResult.Success);
        Assert.Equal("ConstraintUnsavedNodeset", saveResult.Code);
    }

    // ── Error cases ────────────────────────────────────────────────────────────

    [Fact]
    public void Save_EmptyFilepath_Fails()
    {
        var ns = MakeNodeset();
        var result = FileManager.Save(ns, "");
        Assert.False(result.Success);
        Assert.Equal("MissingFilepath", result.Code);
    }

    [Fact]
    public void Load_NonExistentFile_Fails()
    {
        string path = Path.Combine(Path.GetTempPath(), $"threadle_nofile_{Guid.NewGuid()}.tsv");
        var result = FileManager.Load(path, "nodeset");
        Assert.False(result.Success);
    }

    [Fact]
    public void Load_UnknownStructureType_Fails()
    {
        var ns = MakeNodeset();
        string path = TempFile(".tsv");
        FileManager.Save(ns, path);

        var result = FileManager.Load(path, "bogustype");
        Assert.False(result.Success);
        Assert.Equal("IOLoadError", result.Code);
    }

    [Fact]
    public void Load_UnsupportedExtension_Fails()
    {
        // Write a dummy file with an unsupported extension
        string path = Path.Combine(Path.GetTempPath(), $"threadle_test_{Guid.NewGuid()}.xyz");
        _tempFiles.Add(path);
        File.WriteAllText(path, "dummy");

        var result = FileManager.Load(path, "nodeset");
        Assert.False(result.Success);
    }

    // ── Valued layer round-trip ────────────────────────────────────────────────

    [Fact]
    public void SaveLoadNetwork_ValuedLayer_EdgeValuePreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("weighted", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("weighted", 1, 2, value: 2.5f);

        string netPath = TempFile(".tsv");
        FileManager.Save(net, netPath);

        var loadResult = FileManager.Load(netPath, "network");
        var loadedNet = (Network)loadResult.Value!.MainStructure;
        var edgeVal = loadedNet.GetEdge("weighted", 1, 2);
        Assert.True(edgeVal.Success);
        Assert.Equal(2.5f, edgeVal.Value, precision: 4);
    }

    // ── Binary network: multiple 1-mode layers ────────────────────────────────

    [Fact]
    public void SaveLoadNetwork_Bin_MultipleOneModes_AllLayersPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.AddLayerOneMode("follows", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("follows", 3, 4);
        net.AddLayerOneMode("trust", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("trust", 1, 3, value: 0.8f);

        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        Assert.True(loadedNet.CheckEdgeExists("friends", 1, 2).Value);
        Assert.True(loadedNet.CheckEdgeExists("follows", 3, 4).Value);
        Assert.False(loadedNet.CheckEdgeExists("follows", 4, 3).Value);  // directed
        Assert.Equal(0.8f, loadedNet.GetEdge("trust", 1, 3).Value, precision: 4);
    }

    // ── Binary network: 2-mode layer ──────────────────────────────────────────

    [Fact]
    public void SaveLoadNetwork_Bin_TwoModeLayer_HyperedgesPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "club1", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "club2", new uint[] { 2, 4 });

        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        Assert.True(loadedNet.Layers.ContainsKey("clubs"));

        var nodes1 = loadedNet.GetHyperedgeNodes("clubs", "club1");
        Assert.True(nodes1.Success);
        Assert.Contains(1u, nodes1.Value);
        Assert.Contains(2u, nodes1.Value);
        Assert.Contains(3u, nodes1.Value);

        var nodes2 = loadedNet.GetHyperedgeNodes("clubs", "club2");
        Assert.True(nodes2.Success);
        Assert.Contains(2u, nodes2.Value);
        Assert.Contains(4u, nodes2.Value);
    }

    [Fact]
    public void SaveLoadNetwork_Bin_TwoModeLayer_NodeHyperedgesPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "club1", new uint[] { 1, 2 });
        net.AddHyperedge("clubs", "club2", new uint[] { 2, 3 });

        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        // Node 2 belongs to both clubs
        var hyperedges = loadedNet.GetNodeHyperedges("clubs", 2);
        Assert.True(hyperedges.Success);
        Assert.Contains("club1", hyperedges.Value);
        Assert.Contains("club2", hyperedges.Value);

        // Node 1 belongs only to club1
        var hyperedges1 = loadedNet.GetNodeHyperedges("clubs", 1);
        Assert.Single(hyperedges1.Value);
        Assert.Contains("club1", hyperedges1.Value);
    }

    // ── Binary network: mixed 1-mode and 2-mode layers ─────────────────────────

    [Fact]
    public void SaveLoadNetwork_Bin_MixedLayers_BothPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.AddEdge("friends", 3, 4);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "clubA", new uint[] { 1, 3, 5 });

        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        Assert.True(loadedNet.Layers.ContainsKey("friends"));
        Assert.True(loadedNet.Layers.ContainsKey("clubs"));
        Assert.True(loadedNet.CheckEdgeExists("friends", 1, 2).Value);
        Assert.True(loadedNet.CheckEdgeExists("friends", 3, 4).Value);
        Assert.False(loadedNet.CheckEdgeExists("friends", 1, 3).Value);

        var clubNodes = loadedNet.GetHyperedgeNodes("clubs", "clubA");
        Assert.True(clubNodes.Success);
        Assert.Contains(1u, clubNodes.Value);
        Assert.Contains(3u, clubNodes.Value);
        Assert.Contains(5u, clubNodes.Value);
    }

    // ── Binary network: load with packLayers ──────────────────────────────────

    [Fact]
    public void SaveLoadNetwork_Bin_PackLayers_LayerIsStatic()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = MakeNetwork(ns);
        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network", packLayers: true).Value!.MainStructure;

        Assert.True(loadedNet.Layers["friends"].IsStatic);
    }

    [Fact]
    public void SaveLoadNetwork_Bin_PackLayers_EdgesStillQueryable()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = MakeNetwork(ns);
        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network", packLayers: true).Value!.MainStructure;

        Assert.True(loadedNet.CheckEdgeExists("friends", 1, 2).Value);
        Assert.True(loadedNet.CheckEdgeExists("friends", 2, 3).Value);
        Assert.False(loadedNet.CheckEdgeExists("friends", 1, 3).Value);
    }

    // ── Directed layer round-trip ──────────────────────────────────────────────

    [Fact]
    public void SaveLoadNetwork_DirectedLayer_DirectionalityPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("follows", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("follows", 1, 2);   // 1→2 only

        string netPath = TempFile(".tsv");
        FileManager.Save(net, netPath);

        var loadResult = FileManager.Load(netPath, "network");
        var loadedNet = (Network)loadResult.Value!.MainStructure;

        var forward = loadedNet.CheckEdgeExists("follows", 1, 2);
        Assert.True(forward.Value);   // 1→2 exists

        var reverse = loadedNet.CheckEdgeExists("follows", 2, 1);
        Assert.False(reverse.Value);  // 2→1 does not exist
    }

    // ── Save/load with pre-packed (static) layers — TSV ───────────────────────

    [Fact]
    public void SaveLoadNetwork_Tsv_Packed_OneModeUndirectedBinary_EdgesPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.AddEdge("friends", 2, 3);
        net.AddEdge("friends", 4, 5);
        net.Pack("friends");

        string netPath = TempFile(".tsv");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        Assert.True(loadedNet.CheckEdgeExists("friends", 1, 2).Value);
        Assert.True(loadedNet.CheckEdgeExists("friends", 2, 3).Value);
        Assert.True(loadedNet.CheckEdgeExists("friends", 4, 5).Value);
        Assert.False(loadedNet.CheckEdgeExists("friends", 1, 3).Value);
    }

    [Fact]
    public void SaveLoadNetwork_Tsv_Packed_OneModeDirectedBinary_DirectionalityPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("follows", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("follows", 1, 2);
        net.AddEdge("follows", 3, 1);
        net.Pack("follows");

        string netPath = TempFile(".tsv");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        Assert.True(loadedNet.CheckEdgeExists("follows", 1, 2).Value);
        Assert.False(loadedNet.CheckEdgeExists("follows", 2, 1).Value);  // directed
        Assert.True(loadedNet.CheckEdgeExists("follows", 3, 1).Value);
        Assert.False(loadedNet.CheckEdgeExists("follows", 1, 3).Value);
    }

    [Fact]
    public void SaveLoadNetwork_Tsv_Packed_OneModeValued_EdgeValuesPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("trust", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("trust", 1, 2, value: 0.5f);
        net.AddEdge("trust", 2, 3, value: 1.5f);
        net.Pack("trust");

        string netPath = TempFile(".tsv");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        Assert.Equal(0.5f, loadedNet.GetEdge("trust", 1, 2).Value, precision: 4);
        Assert.Equal(1.5f, loadedNet.GetEdge("trust", 2, 3).Value, precision: 4);
    }

    [Fact]
    public void SaveLoadNetwork_Tsv_Packed_TwoMode_HyperedgesPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "club1", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "club2", new uint[] { 2, 4 });
        net.Pack("clubs");

        string netPath = TempFile(".tsv");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        var nodes1 = loadedNet.GetHyperedgeNodes("clubs", "club1");
        Assert.True(nodes1.Success);
        Assert.Contains(1u, nodes1.Value);
        Assert.Contains(2u, nodes1.Value);
        Assert.Contains(3u, nodes1.Value);

        var nodes2 = loadedNet.GetHyperedgeNodes("clubs", "club2");
        Assert.True(nodes2.Success);
        Assert.Contains(2u, nodes2.Value);
        Assert.Contains(4u, nodes2.Value);
    }

    [Fact]
    public void SaveLoadNetwork_Tsv_Packed_MixedLayers_AllPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.AddEdge("friends", 3, 4);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "clubA", new uint[] { 1, 3, 5 });
        net.Pack(null);  // pack all

        string netPath = TempFile(".tsv");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        Assert.True(loadedNet.CheckEdgeExists("friends", 1, 2).Value);
        Assert.True(loadedNet.CheckEdgeExists("friends", 3, 4).Value);
        Assert.False(loadedNet.CheckEdgeExists("friends", 1, 3).Value);

        var clubNodes = loadedNet.GetHyperedgeNodes("clubs", "clubA");
        Assert.True(clubNodes.Success);
        Assert.Contains(1u, clubNodes.Value);
        Assert.Contains(3u, clubNodes.Value);
        Assert.Contains(5u, clubNodes.Value);
    }

    // ── Save/load with pre-packed (static) layers — BIN ───────────────────────

    [Fact]
    public void SaveLoadNetwork_Bin_Packed_OneModeUndirectedBinary_EdgesPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.AddEdge("friends", 2, 3);
        net.AddEdge("friends", 4, 5);
        net.Pack("friends");

        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        Assert.True(loadedNet.CheckEdgeExists("friends", 1, 2).Value);
        Assert.True(loadedNet.CheckEdgeExists("friends", 2, 3).Value);
        Assert.True(loadedNet.CheckEdgeExists("friends", 4, 5).Value);
        Assert.False(loadedNet.CheckEdgeExists("friends", 1, 3).Value);
    }

    [Fact]
    public void SaveLoadNetwork_Bin_Packed_OneModeDirectedBinary_DirectionalityPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("follows", EdgeDirectionality.Directed, EdgeType.Binary, false);
        net.AddEdge("follows", 1, 2);
        net.AddEdge("follows", 3, 1);
        net.Pack("follows");

        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        Assert.True(loadedNet.CheckEdgeExists("follows", 1, 2).Value);
        Assert.False(loadedNet.CheckEdgeExists("follows", 2, 1).Value);  // directed
        Assert.True(loadedNet.CheckEdgeExists("follows", 3, 1).Value);
        Assert.False(loadedNet.CheckEdgeExists("follows", 1, 3).Value);
    }

    [Fact]
    public void SaveLoadNetwork_Bin_Packed_OneModeValued_EdgeValuesPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("trust", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("trust", 1, 2, value: 0.5f);
        net.AddEdge("trust", 2, 3, value: 1.5f);
        net.Pack("trust");

        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        Assert.Equal(0.5f, loadedNet.GetEdge("trust", 1, 2).Value, precision: 4);
        Assert.Equal(1.5f, loadedNet.GetEdge("trust", 2, 3).Value, precision: 4);
    }

    [Fact]
    public void SaveLoadNetwork_Bin_Packed_TwoMode_HyperedgesPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "club1", new uint[] { 1, 2, 3 });
        net.AddHyperedge("clubs", "club2", new uint[] { 2, 4 });
        net.Pack("clubs");

        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        var nodes1 = loadedNet.GetHyperedgeNodes("clubs", "club1");
        Assert.True(nodes1.Success);
        Assert.Contains(1u, nodes1.Value);
        Assert.Contains(2u, nodes1.Value);
        Assert.Contains(3u, nodes1.Value);

        var nodes2 = loadedNet.GetHyperedgeNodes("clubs", "club2");
        Assert.True(nodes2.Success);
        Assert.Contains(2u, nodes2.Value);
        Assert.Contains(4u, nodes2.Value);
    }

    [Fact]
    public void SaveLoadNetwork_Bin_Packed_MixedLayers_AllPreserved()
    {
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.AddEdge("friends", 3, 4);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "clubA", new uint[] { 1, 3, 5 });
        net.Pack(null);  // pack all

        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        Assert.True(loadedNet.CheckEdgeExists("friends", 1, 2).Value);
        Assert.True(loadedNet.CheckEdgeExists("friends", 3, 4).Value);
        Assert.False(loadedNet.CheckEdgeExists("friends", 1, 3).Value);

        var clubNodes = loadedNet.GetHyperedgeNodes("clubs", "clubA");
        Assert.True(clubNodes.Success);
        Assert.Contains(1u, clubNodes.Value);
        Assert.Contains(3u, clubNodes.Value);
        Assert.Contains(5u, clubNodes.Value);
    }

    // ── Export edgelist: static layers ────────────────────────────────────────

    [Fact]
    public void ExportLayerEdgelist_StaticOneModeUndirectedBinary_WritesCorrectRows()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        net.AddEdge("friends", 3, 4);
        net.Pack("friends");

        string path = TempFile(".csv");
        var result = FileManager.ExportLayerEdgelist(net.Layers["friends"], path, ',', false);

        Assert.True(result.Success);
        var lines = File.ReadAllLines(path);
        // Undirected edges are exported once each
        Assert.Equal(2, lines.Length);
        Assert.Contains(lines, l => l == "1,2");
        Assert.Contains(lines, l => l == "3,4");
    }

    [Fact]
    public void ExportLayerEdgelist_StaticOneModeValued_WritesThreeColumns()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("trust", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        net.AddEdge("trust", 1, 2, 0.8f);
        net.Pack("trust");

        string path = TempFile(".csv");
        var result = FileManager.ExportLayerEdgelist(net.Layers["trust"], path, ',', false);

        Assert.True(result.Success);
        var lines = File.ReadAllLines(path);
        // Undirected edges are exported once each
        Assert.Single(lines);
        Assert.All(lines, l =>
        {
            var parts = l.Split(',');
            Assert.Equal(3, parts.Length);
            Assert.Equal(0.8f, float.Parse(parts[2]));
        });
    }

    [Fact]
    public void ExportLayerEdgelist_StaticTwoMode_WritesNodeAffiliationRows()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("clubs");
        net.AddHyperedge("clubs", "A", new uint[] { 1, 2 });
        net.AddHyperedge("clubs", "B", new uint[] { 3 });
        net.Pack("clubs");

        string path = TempFile(".csv");
        var result = FileManager.ExportLayerEdgelist(net.Layers["clubs"], path, ',', false);

        Assert.True(result.Success);
        var lines = File.ReadAllLines(path);
        Assert.Equal(3, lines.Length);
        Assert.Contains(lines, l => l == "1,A");
        Assert.Contains(lines, l => l == "2,A");
        Assert.Contains(lines, l => l == "3,B");
    }

    [Fact]
    public void ExportLayerEdgelist_StaticAndDynamic_OneModeProduceIdenticalOutput()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerOneMode("dyn", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("packed", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        foreach (var name in new[] { "dyn", "packed" })
        {
            net.AddEdge(name, 1, 2);
            net.AddEdge(name, 3, 4);
        }
        net.Pack("packed");

        string pathDyn = TempFile(".csv");
        string pathPacked = TempFile(".csv");
        FileManager.ExportLayerEdgelist(net.Layers["dyn"], pathDyn, ',', false);
        FileManager.ExportLayerEdgelist(net.Layers["packed"], pathPacked, ',', false);

        var linesDyn = File.ReadAllLines(pathDyn).OrderBy(x => x).ToArray();
        var linesPacked = File.ReadAllLines(pathPacked).OrderBy(x => x).ToArray();
        Assert.Equal(linesDyn, linesPacked);
    }

    [Fact]
    public void ExportLayerEdgelist_StaticAndDynamic_TwoModeProduceIdenticalOutput()
    {
        var ns = MakeNodeset();
        var net = new Network("net", ns);
        net.AddLayerTwoMode("dyn");
        net.AddLayerTwoMode("packed");
        foreach (var name in new[] { "dyn", "packed" })
        {
            net.AddHyperedge(name, "A", new uint[] { 1, 2 });
            net.AddHyperedge(name, "B", new uint[] { 3 });
        }
        net.Pack("packed");

        string pathDyn = TempFile(".csv");
        string pathPacked = TempFile(".csv");
        FileManager.ExportLayerEdgelist(net.Layers["dyn"], pathDyn, ',', false);
        FileManager.ExportLayerEdgelist(net.Layers["packed"], pathPacked, ',', false);

        var linesDyn = File.ReadAllLines(pathDyn).OrderBy(x => x).ToArray();
        var linesPacked = File.ReadAllLines(pathPacked).OrderBy(x => x).ToArray();
        Assert.Equal(linesDyn, linesPacked);
    }

    // ── Layer count > 255 (version 2 binary format) ───────────────────────────

    [Fact]
    public void SaveLoadNetwork_Bin_MoreThan255Layers_AllLayersPresent()
    {
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 3; i++)
            ns.AddNode(i);

        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        const int layerCount = 300;
        for (int i = 0; i < layerCount; i++)
        {
            string layerName = $"layer{i}";
            net.AddLayerOneMode(layerName, EdgeDirectionality.Undirected, EdgeType.Binary, false);
            net.AddEdge(layerName, 1, 2);
        }

        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        Assert.Equal(layerCount, loadedNet.Layers.Count);
        for (int i = 0; i < layerCount; i++)
            Assert.True(loadedNet.Layers.ContainsKey($"layer{i}"));
    }

    [Fact]
    public void SaveLoadNetwork_Bin_MoreThan255Layers_EdgesPreserved()
    {
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 3; i++)
            ns.AddNode(i);

        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        for (int i = 0; i < 300; i++)
        {
            string layerName = $"layer{i}";
            net.AddLayerOneMode(layerName, EdgeDirectionality.Undirected, EdgeType.Binary, false);
            net.AddEdge(layerName, 1, 2);
        }

        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        var loadedNet = (Network)FileManager.Load(netPath, "network").Value!.MainStructure;

        // Spot-check first, middle, and last layers
        Assert.True(loadedNet.CheckEdgeExists("layer0", 1, 2).Value);
        Assert.True(loadedNet.CheckEdgeExists("layer150", 1, 2).Value);
        Assert.True(loadedNet.CheckEdgeExists("layer299", 1, 2).Value);
    }

    // ── Version 1 binary format backward compatibility ─────────────────────────

    /// <summary>
    /// Constructs a hand-crafted version 1 binary network file (layer count stored as a single
    /// byte) and verifies that the reader can still load it correctly.
    /// </summary>
    [Fact]
    public void LoadNetwork_Bin_Version1Format_BackwardCompatible()
    {
        // Save a nodeset so the network loader can find it
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 5; i++)
            ns.AddNode(i);
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);
        string nsFilename = Path.GetFileName(nsPath);

        // Manually write a version 1 binary network file
        string netPath = TempFile(".bin");
        using (var ms = new MemoryStream())
        using (var w = new System.IO.BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            // Magic "TNTW"
            w.Write(System.Text.Encoding.ASCII.GetBytes("TNTW"));
            // Version 1
            w.Write((byte)1);
            // Network name
            V1WriteString(w, "v1-net");
            // Nodeset filename
            V1WriteString(w, nsFilename);
            // Layer count as BYTE (version 1 format — the key backward-compat difference)
            w.Write((byte)2);

            // Layer "alpha": undirected binary, ego=1, alter=2
            V1WriteString(w, "alpha");
            w.Write((byte)1);      // mode = 1-mode
            w.Write((byte)1);      // directionality = Undirected
            w.Write((byte)0);      // EdgeType = Binary
            w.Write(false);        // selfties
            w.Write(1);            // nbrEdgesets
            w.Write((uint)1);      // egoId
            w.Write(1);            // nbrAlters
            w.Write((uint)2);      // alterId

            // Layer "beta": undirected binary, ego=3, alter=4
            V1WriteString(w, "beta");
            w.Write((byte)1);
            w.Write((byte)1);
            w.Write((byte)0);
            w.Write(false);
            w.Write(1);
            w.Write((uint)3);
            w.Write(1);
            w.Write((uint)4);

            File.WriteAllBytes(netPath, ms.ToArray());
        }

        var loadResult = FileManager.Load(netPath, "network");
        Assert.True(loadResult.Success);
        var loadedNet = (Network)loadResult.Value!.MainStructure;

        Assert.Equal(2, loadedNet.Layers.Count);
        Assert.True(loadedNet.CheckEdgeExists("alpha", 1, 2).Value);
        Assert.True(loadedNet.CheckEdgeExists("beta", 3, 4).Value);
    }

    /// <summary>
    /// Helper: writes a string with a 1-byte length prefix in the Threadle binary format.
    /// </summary>
    private static void V1WriteString(System.IO.BinaryWriter writer, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        writer.Write((byte)bytes.Length);
        writer.Write(bytes);
    }

    // ── String attributes – TSV round-trip ───────────────────────────────────────

    [Fact]
    public void SaveLoadNodeset_Tsv_StringAttr_ValuesPreserved()
    {
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 3; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("occupation", "string");
        ns.SetNodeAttribute(1u, "occupation", "doctor");
        ns.SetNodeAttribute(2u, "occupation", "engineer");
        ns.SetNodeAttribute(3u, "occupation", "doctor");

        string path = TempFile(".tsv");
        FileManager.Save(ns, path);
        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        Assert.Equal("doctor",   loaded.GetNodeAttributeString(1u, "occupation").Value);
        Assert.Equal("engineer", loaded.GetNodeAttributeString(2u, "occupation").Value);
        Assert.Equal("doctor",   loaded.GetNodeAttributeString(3u, "occupation").Value);
    }

    [Fact]
    public void SaveLoadNodeset_Tsv_StringAttr_PoolDeduplicatedAfterLoad()
    {
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 4; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("country", "string");
        ns.SetNodeAttribute(1u, "country", "Sweden");
        ns.SetNodeAttribute(2u, "country", "Norway");
        ns.SetNodeAttribute(3u, "country", "Sweden");
        ns.SetNodeAttribute(4u, "country", "Norway");

        string path = TempFile(".tsv");
        FileManager.Save(ns, path);
        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        Assert.Equal(2, loaded.StringPool.Count);
    }

    [Fact]
    public void SaveLoadNodeset_Tsv_StringAttr_MissingValueNotLoaded()
    {
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 2; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("occupation", "string");
        ns.SetNodeAttribute(1u, "occupation", "doctor");
        // node 2 has no occupation set

        string path = TempFile(".tsv");
        FileManager.Save(ns, path);
        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        Assert.True(loaded.GetNodeAttributeString(1u, "occupation").Success);
        Assert.False(loaded.GetNodeAttributeString(2u, "occupation").Success);
    }

    [Fact]
    public void SaveLoadNodeset_Tsv_StringAttr_MixedWithOtherTypes()
    {
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 2; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("occupation", "string");
        ns.DefineNodeAttribute("age", "int");
        ns.SetNodeAttribute(1u, "occupation", "doctor");
        ns.SetNodeAttribute(1u, "age", "45");
        ns.SetNodeAttribute(2u, "occupation", "engineer");
        ns.SetNodeAttribute(2u, "age", "32");

        string path = TempFile(".tsv");
        FileManager.Save(ns, path);
        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        Assert.Equal("doctor",   loaded.GetNodeAttributeString(1u, "occupation").Value);
        Assert.Equal("engineer", loaded.GetNodeAttributeString(2u, "occupation").Value);
        var age1 = loaded.GetNodeAttribute(1u, "age");
        Assert.Equal(45, (int)age1.Value.Value.GetValue(NodeAttributeType.Int)!);
    }

    // ── String attributes – Binary round-trip ────────────────────────────────────

    [Fact]
    public void SaveLoadNodeset_Bin_StringAttr_ValuesPreserved()
    {
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 3; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("occupation", "string");
        ns.SetNodeAttribute(1u, "occupation", "doctor");
        ns.SetNodeAttribute(2u, "occupation", "engineer");
        ns.SetNodeAttribute(3u, "occupation", "doctor");

        string path = TempFile(".bin");
        FileManager.Save(ns, path);
        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        Assert.Equal("doctor",   loaded.GetNodeAttributeString(1u, "occupation").Value);
        Assert.Equal("engineer", loaded.GetNodeAttributeString(2u, "occupation").Value);
        Assert.Equal("doctor",   loaded.GetNodeAttributeString(3u, "occupation").Value);
    }

    [Fact]
    public void SaveLoadNodeset_Bin_StringAttr_PoolPreservedExactly()
    {
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 3; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("country", "string");
        ns.SetNodeAttribute(1u, "country", "Sweden");
        ns.SetNodeAttribute(2u, "country", "Norway");
        ns.SetNodeAttribute(3u, "country", "Sweden");

        string path = TempFile(".bin");
        FileManager.Save(ns, path);
        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        Assert.Equal(2, loaded.StringPool.Count);
        Assert.Contains("Sweden", loaded.StringPool);
        Assert.Contains("Norway", loaded.StringPool);
    }

    [Fact]
    public void SaveLoadNodeset_Bin_NoStringAttr_EmptyPoolRoundTrips()
    {
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 3; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("age", "int");
        ns.SetNodeAttribute(1u, "age", "30");

        string path = TempFile(".bin");
        FileManager.Save(ns, path);
        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        Assert.Equal(0, loaded.StringPool.Count);
        var age = loaded.GetNodeAttribute(1u, "age");
        Assert.Equal(30, (int)age.Value.Value.GetValue(NodeAttributeType.Int)!);
    }

    [Fact]
    public void SaveLoadNodeset_Bin_StringAttr_MixedWithOtherTypes()
    {
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 2; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("occupation", "string");
        ns.DefineNodeAttribute("active", "bool");
        ns.SetNodeAttribute(1u, "occupation", "doctor");
        ns.SetNodeAttribute(1u, "active", "true");
        ns.SetNodeAttribute(2u, "occupation", "engineer");
        ns.SetNodeAttribute(2u, "active", "false");

        string path = TempFile(".bin");
        FileManager.Save(ns, path);
        var loaded = (Nodeset)FileManager.Load(path, "nodeset").Value!.MainStructure;

        Assert.Equal("doctor",   loaded.GetNodeAttributeString(1u, "occupation").Value);
        Assert.Equal("engineer", loaded.GetNodeAttributeString(2u, "occupation").Value);
        var active1 = loaded.GetNodeAttribute(1u, "active");
        Assert.Equal(true, (bool)active1.Value.Value.GetValue(NodeAttributeType.Bool)!);
    }

    // ── Cross-format: nodeset .bin + network .tsv ────────────────────────────────

    [Fact]
    public void SaveLoadNetwork_BinNodeset_TsvNetwork_EdgesPreserved()
    {
        // Nodeset saved as .bin, network saved as .tsv — load must use correct serializers
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = MakeNetwork(ns);
        string netPath = TempFile(".tsv");
        var saveResult = FileManager.Save(net, netPath);
        Assert.True(saveResult.Success, $"Save failed: {saveResult.Message}");

        var loadResult = FileManager.Load(netPath, "network");
        Assert.True(loadResult.Success, $"Load failed: {loadResult.Message}");
        var loadedNet = (Network)loadResult.Value!.MainStructure;

        Assert.True(loadedNet.CheckEdgeExists("friends", 1, 2).Value);
        Assert.True(loadedNet.CheckEdgeExists("friends", 2, 3).Value);
    }

    [Fact]
    public void SaveLoadNetwork_BinNodeset_TsvNetwork_NodesetStillBin()
    {
        // After saving the network as .tsv, the nodeset must still be saved using BIN format
        // (i.e. the cross-format fix must not corrupt the nodeset file with TSV content)
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = MakeNetwork(ns);
        string netPath = TempFile(".tsv");
        FileManager.Save(net, netPath);

        // Read the raw bytes of the nodeset file — must start with the BIN magic "TNDS"
        byte[] magic = new byte[4];
        using (var fs = File.OpenRead(nsPath))
            fs.Read(magic, 0, 4);
        Assert.Equal("TNDS", System.Text.Encoding.ASCII.GetString(magic));
    }

    [Fact]
    public void SaveLoadNetwork_BinNodeset_TsvNetwork_ModifiedNodesetResavedAsBin()
    {
        // If the nodeset is modified before the network save, SaveNetwork must still
        // re-save the nodeset using its own format (.bin), not the network format (.tsv)
        var ns = MakeNodeset();
        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = MakeNetwork(ns);
        // Modify the nodeset so IsModified = true
        ns.AddNode(99u);
        Assert.True(ns.IsModified);

        string netPath = TempFile(".tsv");
        var saveResult = FileManager.Save(net, netPath);
        Assert.True(saveResult.Success, $"Save failed: {saveResult.Message}");

        // The nodeset file must still be readable as BIN (magic bytes intact)
        byte[] magic = new byte[4];
        using (var fs = File.OpenRead(nsPath))
            fs.Read(magic, 0, 4);
        Assert.Equal("TNDS", System.Text.Encoding.ASCII.GetString(magic));

        // Loading the network must yield the updated nodeset (node 99 present)
        var loadResult = FileManager.Load(netPath, "network");
        Assert.True(loadResult.Success);
        var loadedNet = (Network)loadResult.Value!.MainStructure;
        Assert.Contains(99u, loadedNet.Nodeset.NodeIdArray);
    }

    [Fact]
    public void SaveLoadNetwork_BinNodeset_TsvNetwork_StringAttrPreserved()
    {
        // String node attributes must survive the cross-format round-trip
        var ns = new Nodeset("test-ns");
        for (uint i = 1; i <= 3; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("country", "string");
        ns.SetNodeAttribute(1u, "country", "Sweden");
        ns.SetNodeAttribute(2u, "country", "Norway");
        ns.SetNodeAttribute(3u, "country", "Sweden");

        string nsPath = TempFile(".bin");
        FileManager.Save(ns, nsPath);

        var net = new Network("test-net", ns);
        net.AddLayerOneMode("friends", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("friends", 1, 2);
        string netPath = TempFile(".tsv");
        FileManager.Save(net, netPath);

        var loadResult = FileManager.Load(netPath, "network");
        Assert.True(loadResult.Success);
        var loadedNet = (Network)loadResult.Value!.MainStructure;
        Assert.Equal("Sweden", loadedNet.Nodeset.GetNodeAttributeString(1u, "country").Value);
        Assert.Equal("Norway", loadedNet.Nodeset.GetNodeAttributeString(2u, "country").Value);
        Assert.Equal("Sweden", loadedNet.Nodeset.GetNodeAttributeString(3u, "country").Value);
    }

    // ── Cross-format: nodeset .tsv + network .bin ────────────────────────────────

    [Fact]
    public void SaveLoadNetwork_TsvNodeset_BinNetwork_EdgesPreserved()
    {
        // Nodeset saved as .tsv, network saved as .bin
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = MakeNetwork(ns);
        string netPath = TempFile(".bin");
        var saveResult = FileManager.Save(net, netPath);
        Assert.True(saveResult.Success, $"Save failed: {saveResult.Message}");

        var loadResult = FileManager.Load(netPath, "network");
        Assert.True(loadResult.Success, $"Load failed: {loadResult.Message}");
        var loadedNet = (Network)loadResult.Value!.MainStructure;

        Assert.True(loadedNet.CheckEdgeExists("friends", 1, 2).Value);
        Assert.True(loadedNet.CheckEdgeExists("friends", 2, 3).Value);
    }

    [Fact]
    public void SaveLoadNetwork_TsvNodeset_BinNetwork_NodesetStillTsv()
    {
        // After saving the network as .bin, the nodeset file must remain TSV (not corrupted to BIN)
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = MakeNetwork(ns);
        string netPath = TempFile(".bin");
        FileManager.Save(net, netPath);

        // A TSV nodeset file must start with the nodeset name (text), not binary magic bytes
        string firstLine = File.ReadLines(nsPath).First();
        Assert.Equal("test-ns", firstLine.Split('\t')[0]);
    }

    [Fact]
    public void SaveLoadNetwork_TsvNodeset_BinNetwork_ModifiedNodesetResavedAsTsv()
    {
        // Modify nodeset after initial save; SaveNetwork (BIN) must re-save nodeset as TSV
        var ns = MakeNodeset();
        string nsPath = TempFile(".tsv");
        FileManager.Save(ns, nsPath);

        var net = MakeNetwork(ns);
        ns.AddNode(99u);
        Assert.True(ns.IsModified);

        string netPath = TempFile(".bin");
        var saveResult = FileManager.Save(net, netPath);
        Assert.True(saveResult.Success, $"Save failed: {saveResult.Message}");

        // Nodeset file must still be valid TSV (first column of first line is nodeset name)
        string firstLine = File.ReadLines(nsPath).First();
        Assert.Equal("test-ns", firstLine.Split('\t')[0]);

        // Loading the network must yield the updated nodeset (node 99 present)
        var loadResult = FileManager.Load(netPath, "network");
        Assert.True(loadResult.Success);
        var loadedNet = (Network)loadResult.Value!.MainStructure;
        Assert.Contains(99u, loadedNet.Nodeset.NodeIdArray);
    }

    // ── Generators: re-running on a non-empty layer must clear first ──────────────

    [Fact]
    public void GenerateErdosRenyi_OnNonEmptyLayer_ClearsFirst()
    {
        // Running ER generator twice must produce the same edge count as once, not accumulate
        var ns = new Nodeset("ns", 20);
        var net = new Network("net", ns);
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);

        Core.Processing.Generators.GenerateErdosRenyiLayer(net, "layer", 1.0);
        ulong edgesFirst = ((ILayerOneMode)net.Layers["layer"]).NbrEdges;
        Assert.True(edgesFirst > 0);

        Core.Processing.Generators.GenerateErdosRenyiLayer(net, "layer", 1.0);
        ulong edgesSecond = ((ILayerOneMode)net.Layers["layer"]).NbrEdges;

        Assert.Equal(edgesFirst, edgesSecond);
    }

    [Fact]
    public void GenerateBarabasiAlbert_OnNonEmptyLayer_ClearsFirst()
    {
        // Running BA generator twice on the same layer must not accumulate edges
        var ns = new Nodeset("ns", 20);
        var net = new Network("net", ns);
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);

        Core.Processing.Generators.GenerateBarabasiAlbertLayer(net, "layer", 2);
        ulong edgesFirst = ((ILayerOneMode)net.Layers["layer"]).NbrEdges;
        Assert.True(edgesFirst > 0);

        Core.Processing.Generators.GenerateBarabasiAlbertLayer(net, "layer", 2);
        ulong edgesSecond = ((ILayerOneMode)net.Layers["layer"]).NbrEdges;

        Assert.Equal(edgesFirst, edgesSecond);
    }

    [Fact]
    public void GenerateRandomTwoMode_OnNonEmptyLayer_ClearsFirst()
    {
        // Running 2-mode generator twice must produce identical hyperedge count, not accumulated
        var ns = new Nodeset("ns", 15);
        var net = new Network("net", ns);
        net.AddLayerTwoMode("layer");

        Core.Processing.Generators.GenerateRandomTwoModeLayer(net, "layer", 5, 3);
        uint hyperedgesFirst = ((Core.Model.LayerTwoMode)net.Layers["layer"]).NbrHyperedges;
        Assert.True(hyperedgesFirst > 0);

        Core.Processing.Generators.GenerateRandomTwoModeLayer(net, "layer", 5, 3);
        uint hyperedgesSecond = ((Core.Model.LayerTwoMode)net.Layers["layer"]).NbrHyperedges;

        Assert.Equal(hyperedgesFirst, hyperedgesSecond);
    }
}
