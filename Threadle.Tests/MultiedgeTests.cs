using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// Multiedges are never allowed: the public AddEdge always refuses a tie that already exists (there is no
/// longer a 'blockmultiedges' setting); edgelist and matrix imports and TSV loading add ties without a
/// per-edge check and remove duplicates once at the end, keeping the first occurrence (and its value, on
/// both edgesets of the tie); a self-tie on a layer without self-ties is skipped on every route.
/// </summary>
public class MultiedgeTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    private string TempFile(string ext, params string[] lines)
    {
        string path = Path.Combine(Path.GetTempPath(), $"threadle_multiedge_test_{Guid.NewGuid()}{ext}");
        if (lines.Length > 0) File.WriteAllLines(path, lines);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    private static Network MakeNetwork(int n = 5)
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= n; i++) ns.AddNode(i);
        return new Network("net", ns);
    }

    private static int Degree(Network net, string layer, uint node, EdgeTraversal t = EdgeTraversal.Both)
        => ((ILayerOneMode)net.Layers[layer]).GetNodeAlters(node, t).Length;

    private static float Value(Network net, string layer, uint a, uint b) => net.GetEdge(layer, a, b).Value;

    // ── Public AddEdge: always refuses an existing tie ──────────────────────────

    [Theory]
    [InlineData(EdgeType.Binary)]
    [InlineData(EdgeType.Valued)]
    public void AddEdge_Undirected_DuplicateInEitherOrder_IsRefused(EdgeType type)
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, type, false);
        Assert.True(net.AddEdge("l", 1, 2, 3).Success);
        Assert.False(net.AddEdge("l", 1, 2, 4).Success);
        Assert.False(net.AddEdge("l", 2, 1, 5).Success);
        Assert.Equal(1, Degree(net, "l", 1));
        Assert.Equal(1, Degree(net, "l", 2));
        if (type == EdgeType.Valued)
            Assert.Equal(3f, Value(net, "l", 2, 1));
    }

    [Theory]
    [InlineData(EdgeType.Binary)]
    [InlineData(EdgeType.Valued)]
    public void AddEdge_Directed_DuplicateRefused_ReverseAllowed(EdgeType type)
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("l", EdgeDirectionality.Directed, type, false);
        Assert.True(net.AddEdge("l", 1, 2, 3).Success);
        Assert.False(net.AddEdge("l", 1, 2, 4).Success);
        Assert.True(net.AddEdge("l", 2, 1, 5).Success);
        Assert.Equal(1, Degree(net, "l", 1, EdgeTraversal.Out));
        Assert.Equal(1, Degree(net, "l", 2, EdgeTraversal.In));
        if (type == EdgeType.Valued)
        {
            Assert.Equal(3f, Value(net, "l", 1, 2));
            Assert.Equal(5f, Value(net, "l", 2, 1));
        }
    }

    [Fact]
    public void BlockMultiedgesSetting_NoLongerExists()
    {
        var r = UserSettings.Set("blockmultiedges", "false");
        Assert.False(r.Success);
        Assert.Equal("SettingNotFound", r.Code);
    }

    // ── Edgelist import: duplicates silently dropped, first occurrence kept ─────

    [Fact]
    public void EdgelistImport_Undirected_BothDirectionsListed_GivesOneTie_FirstValueKept()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Valued, false);
        string path = TempFile(".txt", "1\t2\t5", "2\t1\t9", "2\t3\t1", "2\t3\t7");
        Assert.True(FileManager.ImportOneModeEdgeList(path, net, (LayerOneMode)net.Layers["l"], 0, 1, 2, false, '\t', false).Success);
        Assert.Equal(2, Degree(net, "l", 2));
        Assert.Equal(1, Degree(net, "l", 1));
        Assert.Equal(5f, Value(net, "l", 1, 2));
        Assert.Equal(5f, Value(net, "l", 2, 1));
        Assert.Equal(1f, Value(net, "l", 3, 2));
    }

    [Fact]
    public void EdgelistImport_Directed_DuplicateRows_FirstValueKept_OnBothSides()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("l", EdgeDirectionality.Directed, EdgeType.Valued, false);
        string path = TempFile(".txt", "1\t2\t5", "1\t2\t9", "1\t3\t2", "1\t2\t4");
        Assert.True(FileManager.ImportOneModeEdgeList(path, net, (LayerOneMode)net.Layers["l"], 0, 1, 2, false, '\t', false).Success);
        Assert.Equal(2, Degree(net, "l", 1, EdgeTraversal.Out));
        Assert.Equal(1, Degree(net, "l", 2, EdgeTraversal.In));
        Assert.Equal(5f, Value(net, "l", 1, 2));
        // The inbound copy at node 2 keeps the same (first) value: packing rebuilds from outbound,
        // so check the mutable layer's inbound view through a transpose-like query instead
        var layer = (LayerOneMode)net.Layers["l"];
        var (inAlters, inValues) = layer.GetNodeAltersWithWeights(2, EdgeTraversal.In);
        Assert.Equal(1, inAlters.Length);
        Assert.Equal(5f, inValues.Span[0]);
    }

    [Theory]
    [InlineData(EdgeDirectionality.Directed)]
    [InlineData(EdgeDirectionality.Undirected)]
    public void EdgelistImport_ManyDuplicates_FirstValueKeptEverywhere(EdgeDirectionality dir)
    {
        // Node 1 tied to 60 partners, each tie listed three times with different values, in
        // interleaved order: long neighbour lists, where an unstable sort would keep arbitrary copies
        var net = MakeNetwork(61);
        net.AddLayerOneMode("l", dir, EdgeType.Valued, false);
        var lines = new List<string>();
        for (int copy = 0; copy < 3; copy++)
            for (uint p = 2; p <= 61; p++)
                lines.Add($"1\t{p}\t{p + 1000 * copy}");
        Assert.True(FileManager.ImportOneModeEdgeList(TempFile(".txt", [.. lines]), net, (LayerOneMode)net.Layers["l"], 0, 1, 2, false, '\t', false).Success);
        var layer = (LayerOneMode)net.Layers["l"];
        Assert.Equal(60, Degree(net, "l", 1, EdgeTraversal.Out));
        for (uint p = 2; p <= 61; p++)
        {
            Assert.Equal((float)p, Value(net, "l", 1, p));
            var (alters, values) = layer.GetNodeAltersWithWeights(p, dir == EdgeDirectionality.Directed ? EdgeTraversal.In : EdgeTraversal.Both);
            Assert.Equal(1, alters.Length);
            Assert.Equal((float)p, values.Span[0]);
        }
    }

    [Fact]
    public void EdgelistImport_SelfTie_SkippedOnLayerWithoutSelfties()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddLayerOneMode("s", EdgeDirectionality.Undirected, EdgeType.Binary, true);
        string path = TempFile(".txt", "1\t1", "1\t2");
        Assert.True(FileManager.ImportOneModeEdgeList(path, net, (LayerOneMode)net.Layers["l"], 0, 1, -1, false, '\t', false).Success);
        Assert.True(FileManager.ImportOneModeEdgeList(path, net, (LayerOneMode)net.Layers["s"], 0, 1, -1, false, '\t', false).Success);
        Assert.False(net.CheckEdgeExists("l", 1, 1).Value);
        Assert.True(net.CheckEdgeExists("l", 1, 2).Value);
        Assert.True(net.CheckEdgeExists("s", 1, 1).Value);
    }

    // ── Matrix import ──────────────────────────────────────────────────────────

    [Fact]
    public void MatrixImport_RepeatedHeader_FirstValueKept_DiagonalSkippedWithoutSelfties()
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("l", EdgeDirectionality.Directed, EdgeType.Valued, false);
        // Node 1 appears twice as a row/column: 1->2 is given as 5 (first) and 9 (second)
        string path = TempFile(".txt",
            "x\t1\t2\t1",
            "1\t3\t5\t0",
            "2\t0\t0\t6",
            "1\t0\t9\t0");
        Assert.True(FileManager.ImportOneModeMatrix(path, net, (LayerOneMode)net.Layers["l"], '\t', false).Success);
        Assert.False(net.CheckEdgeExists("l", 1, 1).Value);
        Assert.Equal(5f, Value(net, "l", 1, 2));
        Assert.Equal(6f, Value(net, "l", 2, 1));
        Assert.Equal(1, Degree(net, "l", 1, EdgeTraversal.Out));
    }

    [Fact]
    public void MatrixImport_MissingNodes_SkippedOrAdded()
    {
        string[] lines = ["x\t1\t9", "1\t0\t2", "9\t3\t0"];
        var a = MakeNetwork();
        a.AddLayerOneMode("l", EdgeDirectionality.Directed, EdgeType.Valued, false);
        Assert.True(FileManager.ImportOneModeMatrix(TempFile(".txt", lines), a, (LayerOneMode)a.Layers["l"], '\t', false).Success);
        Assert.DoesNotContain(9u, a.Nodeset.NodeIdArray);
        Assert.Equal(0, Degree(a, "l", 1));

        var b = MakeNetwork();
        b.AddLayerOneMode("l", EdgeDirectionality.Directed, EdgeType.Valued, false);
        Assert.True(FileManager.ImportOneModeMatrix(TempFile(".txt", lines), b, (LayerOneMode)b.Layers["l"], '\t', true).Success);
        Assert.Contains(9u, b.Nodeset.NodeIdArray);
        Assert.Equal(2f, Value(b, "l", 1, 9));
        Assert.Equal(3f, Value(b, "l", 9, 1));
    }

    // ── TSV: no per-edge check, hand-edited duplicates removed at the end ──────

    [Theory]
    [InlineData(EdgeType.Binary)]
    [InlineData(EdgeType.Valued)]
    public void TsvLoad_HandEditedDuplicate_IsRemoved_FirstValueKept(EdgeType type)
    {
        var net = MakeNetwork();
        net.AddLayerOneMode("l", EdgeDirectionality.Undirected, type, false);
        net.AddEdge("l", 1, 2, 5);
        net.AddEdge("l", 2, 3, 1);
        FileManager.Save(net.Nodeset, TempFile(".tsv"));
        string nsPath = _tempFiles[^1];
        net.Nodeset.Filepath = nsPath;
        string path = TempFile(".tsv");
        Assert.True(FileManager.Save(net, path).Success);

        // Hand edit: list 1-2 again, from node 2, with a different value
        var lines = File.ReadAllLines(path).ToList();
        lines.Add(type == EdgeType.Binary ? "2\t1" : "2\t1;9");
        File.WriteAllLines(path, lines);

        var loaded = (Network)FileManager.Load(path, "network").Value!.MainStructure;
        Assert.Equal(1, Degree(loaded, "l", 1));
        Assert.Equal(2, Degree(loaded, "l", 2));
        if (type == EdgeType.Valued)
        {
            Assert.Equal(5f, Value(loaded, "l", 1, 2));
            Assert.Equal(5f, Value(loaded, "l", 2, 1));
        }
    }
}