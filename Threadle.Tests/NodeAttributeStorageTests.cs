using System.Text;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Processing.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Tests;

/// <summary>
/// Tests for node attribute storage: dense attributes (flat per-node columns with a presence bitmap),
/// sparse attributes (compact per-node blobs), node slot recycling, propagation of the storage mode,
/// and persistence of the storage mode in binary and TSV files (including legacy binary files).
/// </summary>
public class NodeAttributeStorageTests : IDisposable
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private readonly List<string> _tempFiles = [];

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

    private static Nodeset MakeNodeset(int n = 5)
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= n; i++)
            ns.AddNode(i);
        return ns;
    }

    /// <summary>Returns the storage mode of an attribute, as reported in the Nodeset's Info.</summary>
    private static string StorageOf(Nodeset ns, string attrName)
    {
        var meta = (List<Dictionary<string, object>>)ns.Info["NodeAttributes"];
        return (string)meta.Single(d => (string)d["Name"] == attrName)["Storage"];
    }

    private static object? Value(Nodeset ns, uint nodeId, string attrName)
    {
        var r = ns.GetNodeAttribute(nodeId, attrName);
        if (!r.Success)
            return null;
        if (r.Value.Type == NodeAttributeType.String)
            return ns.GetNodeAttributeString(nodeId, attrName).Value;
        return r.Value.Value.GetValue(r.Value.Type);
    }

    private Nodeset SaveAndLoad(Nodeset ns, string extension)
    {
        string path = TempFile(extension);
        Assert.True(FileManager.Save(ns, path).Success);
        var load = FileManager.Load(path, "nodeset");
        Assert.True(load.Success, load.Message);
        return (Nodeset)load.Value!.MainStructure;
    }

    // ── Definition ────────────────────────────────────────────────────────────

    [Fact]
    public void Define_DefaultsToSparse()
    {
        var ns = MakeNodeset();
        Assert.True(ns.DefineNodeAttribute("a", "int").Success);
        Assert.Equal("Sparse", StorageOf(ns, "a"));
    }

    [Theory]
    [InlineData("dense", "Dense")]
    [InlineData("DENSE", "Dense")]
    [InlineData("sparse", "Sparse")]
    public void Define_WithStorage_IsRecorded(string storage, string expected)
    {
        var ns = MakeNodeset();
        Assert.True(ns.DefineNodeAttribute("a", "float", storage).Success);
        Assert.Equal(expected, StorageOf(ns, "a"));
    }

    [Fact]
    public void Define_InvalidStorage_Fails()
    {
        var ns = MakeNodeset();
        var r = ns.DefineNodeAttribute("a", "int", "columnar");
        Assert.False(r.Success);
        Assert.Equal("InvalidAttributeStorage", r.Code);
        Assert.Empty(ns.GetDefinedAttributeNames());
    }

    // ── Dense get/set/remove ──────────────────────────────────────────────────

    [Theory]
    [InlineData("int", "42", 42)]
    [InlineData("float", "1.5", 1.5f)]
    [InlineData("bool", "true", true)]
    [InlineData("char", "x", 'x')]
    [InlineData("string", "hello", "hello")]
    public void Dense_SetGet_AllTypes(string type, string valueStr, object expected)
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", type, "dense");
        Assert.True(ns.SetNodeAttribute(3, "a", valueStr).Success);
        Assert.Equal(expected, Value(ns, 3, "a"));
    }

    [Fact]
    public void Dense_UnsetNode_ReportsAttributeNotSet()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int", "dense");
        ns.SetNodeAttribute(1, "a", "7");
        var r = ns.GetNodeAttribute(2, "a");
        Assert.False(r.Success);
        Assert.Equal("AttributeNotSet", r.Code);
    }

    [Fact]
    public void Dense_ZeroValue_IsDistinctFromUnset()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int", "dense");
        ns.SetNodeAttribute(1, "a", "0");
        Assert.Equal(0, Value(ns, 1, "a"));
        Assert.Null(Value(ns, 2, "a"));
    }

    [Fact]
    public void Dense_Overwrite_ReplacesValue()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int", "dense");
        ns.SetNodeAttribute(1, "a", "1");
        ns.SetNodeAttribute(1, "a", "2");
        Assert.Equal(2, Value(ns, 1, "a"));
    }

    [Fact]
    public void Dense_RemoveAttr_UnsetsOnlyThatNode()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int", "dense");
        ns.SetNodeAttributes(null, "a", "5", null);
        Assert.True(ns.RemoveNodeAttribute(2, "a").Success);
        Assert.Null(Value(ns, 2, "a"));
        Assert.Equal(5, Value(ns, 1, "a"));
        Assert.Equal(5, Value(ns, 3, "a"));
        // Removing again: not set
        var again = ns.RemoveNodeAttribute(2, "a");
        Assert.False(again.Success);
        Assert.Equal("AttributeNotSet", again.Code);
    }

    [Fact]
    public void Dense_NodeAddedAfterDefinition_IsUnset_AndCanBeSet()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int", "dense");
        ns.SetNodeAttributes(null, "a", "1", null);
        // Add enough nodes to force the dense column to grow
        for (uint i = 100; i < 200; i++)
            ns.AddNode(i);
        Assert.Null(Value(ns, 150, "a"));
        ns.SetNodeAttribute(199, "a", "9");
        Assert.Equal(9, Value(ns, 199, "a"));
        Assert.Equal(1, Value(ns, 1, "a"));
    }

    [Fact]
    public void Dense_LargeNodeset_AllValuesCorrect()
    {
        var ns = new Nodeset("big", 10_000);
        ns.DefineNodeAttribute("a", "int", "dense");
        foreach (uint id in ns.NodeIdArray)
            ns.SetNodeAttribute(id, "a", (id * 3).ToString());
        foreach (uint id in ns.NodeIdArray)
            Assert.Equal((int)id * 3, Value(ns, id, "a"));
    }

    // ── Sparse blobs ──────────────────────────────────────────────────────────

    [Fact]
    public void Sparse_ManyAttributesPerNode_RemoveMiddleAndLast()
    {
        var ns = MakeNodeset();
        for (int a = 0; a < 10; a++)
        {
            ns.DefineNodeAttribute($"a{a}", "int");
            ns.SetNodeAttribute(1, $"a{a}", (a * 10).ToString());
        }
        // Overwrite one
        ns.SetNodeAttribute(1, "a3", "333");
        // Remove a middle one and the last one
        Assert.True(ns.RemoveNodeAttribute(1, "a5").Success);
        Assert.True(ns.RemoveNodeAttribute(1, "a9").Success);
        for (int a = 0; a < 10; a++)
        {
            object? expected = a switch { 3 => 333, 5 or 9 => null, _ => a * 10 };
            Assert.Equal(expected, Value(ns, 1, $"a{a}"));
        }
        // Remove all remaining: node still exists with no attributes
        for (int a = 0; a < 10; a++)
            ns.RemoveNodeAttribute(1, $"a{a}");
        Assert.Equal(5, ns.Count);
        for (int a = 0; a < 10; a++)
            Assert.Null(Value(ns, 1, $"a{a}"));
    }

    [Fact]
    public void Sparse_NegativeAndSpecialFloatValues_RoundTripInMemory()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("i", "int");
        ns.DefineNodeAttribute("f", "float");
        ns.SetNodeAttribute(1, "i", "-123456789");
        ns.SetNodeAttribute(1, "f", "NaN");
        ns.SetNodeAttribute(2, "f", "-0.0");
        Assert.Equal(-123456789, Value(ns, 1, "i"));
        Assert.True(float.IsNaN((float)Value(ns, 1, "f")!));
        Assert.True(float.IsNegative((float)Value(ns, 2, "f")!));
    }

    // ── Node removal and slot reuse ───────────────────────────────────────────

    [Fact]
    public void RemoveNode_ThenAdd_ReusedSlotHasNoStaleValues()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("s", "int");
        ns.DefineNodeAttribute("d", "int", "dense");
        ns.SetNodeAttribute(3, "s", "1");
        ns.SetNodeAttribute(3, "d", "2");
        Assert.True(ns.RemoveNode(3).Success);
        Assert.Equal(4, ns.Count);
        Assert.True(ns.AddNode(77).Success);
        Assert.Null(Value(ns, 77, "s"));
        Assert.Null(Value(ns, 77, "d"));
        // Re-adding the removed id also starts clean
        Assert.True(ns.AddNode(3).Success);
        Assert.Null(Value(ns, 3, "s"));
        Assert.Null(Value(ns, 3, "d"));
    }

    [Fact]
    public void RemoveNode_OtherNodesKeepValues()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("d", "int", "dense");
        foreach (uint id in ns.NodeIdArray)
            ns.SetNodeAttribute(id, "d", id.ToString());
        ns.RemoveNode(2);
        Assert.Equal([1u, 3u, 4u, 5u], ns.NodeIdArray);
        foreach (uint id in ns.NodeIdArray)
            Assert.Equal((int)id, Value(ns, id, "d"));
    }

    // ── Undefine and index recycling ──────────────────────────────────────────

    [Fact]
    public void Undefine_Dense_ThenRedefineRecycledIndexAsSparse_StartsEmpty()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("d", "int", "dense");
        ns.SetNodeAttributes(null, "d", "4", null);
        Assert.True(ns.UndefineNodeAttribute("d").Success);
        ns.DefineNodeAttribute("s", "int");   // gets the recycled index
        Assert.Equal("Sparse", StorageOf(ns, "s"));
        foreach (uint id in ns.NodeIdArray)
            Assert.Null(Value(ns, id, "s"));
        ns.SetNodeAttribute(1, "s", "8");
        Assert.Equal(8, Value(ns, 1, "s"));
    }

    [Fact]
    public void Undefine_Sparse_ThenRedefineRecycledIndexAsDense_StartsEmpty()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("keep", "int");
        ns.DefineNodeAttribute("s", "int");
        ns.SetNodeAttributes(null, "keep", "1", null);
        ns.SetNodeAttributes(null, "s", "2", null);
        Assert.True(ns.UndefineNodeAttribute("s").Success);
        ns.DefineNodeAttribute("d", "int", "dense");
        foreach (uint id in ns.NodeIdArray)
        {
            Assert.Null(Value(ns, id, "d"));
            Assert.Equal(1, Value(ns, id, "keep"));
        }
    }

    // ── Propagation of storage mode ───────────────────────────────────────────

    [Fact]
    public void Duplicate_InheritsStorageAndValues()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("d", "int", "dense");
        ns.DefineNodeAttribute("s", "int");
        ns.SetNodeAttribute(1, "d", "10");
        ns.SetNodeAttribute(2, "s", "20");
        Assert.True(ns.DuplicateNodeAttribute("d", "d2").Success);
        Assert.True(ns.DuplicateNodeAttribute("s", "s2").Success);
        Assert.Equal("Dense", StorageOf(ns, "d2"));
        Assert.Equal("Sparse", StorageOf(ns, "s2"));
        Assert.Equal(10, Value(ns, 1, "d2"));
        Assert.Null(Value(ns, 2, "d2"));
        Assert.Equal(20, Value(ns, 2, "s2"));
    }

    [Fact]
    public void Convert_InheritsStorage_SkippedValuesStayUnset()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("d", "string", "dense");
        ns.SetNodeAttribute(1, "d", "12");
        ns.SetNodeAttribute(2, "d", "abc");
        var r = ns.ConvertNodeAttributeType("d", "di", "int");
        Assert.True(r.Success);
        Assert.Equal("Dense", StorageOf(ns, "di"));
        Assert.Equal(12, Value(ns, 1, "di"));
        Assert.Null(Value(ns, 2, "di"));
        Assert.Null(Value(ns, 3, "di"));
    }

    [Fact]
    public void Merge_DenseInEitherSource_IsDenseInResult()
    {
        var ns1 = new Nodeset("a");
        var ns2 = new Nodeset("b");
        ns1.AddNode(1); ns1.AddNode(2);
        ns2.AddNode(2); ns2.AddNode(3);
        ns1.DefineNodeAttribute("x", "int", "dense");
        ns2.DefineNodeAttribute("x", "int");
        ns1.DefineNodeAttribute("y", "int");
        ns1.SetNodeAttribute(1, "x", "1");
        ns2.SetNodeAttribute(3, "x", "3");
        ns1.SetNodeAttribute(2, "y", "22");
        var r = Nodeset.Merge(ns1, ns2, "m");
        Assert.True(r.Success);
        var m = r.Value!;
        Assert.Equal("Dense", StorageOf(m, "x"));
        Assert.Equal("Sparse", StorageOf(m, "y"));
        Assert.Equal(1, Value(m, 1, "x"));
        Assert.Null(Value(m, 2, "x"));
        Assert.Equal(3, Value(m, 3, "x"));
        Assert.Equal(22, Value(m, 2, "y"));
    }

    [Fact]
    public void Filter_KeepsDenseAndSparseValues()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("d", "int", "dense");
        ns.DefineNodeAttribute("s", "string");
        foreach (uint id in ns.NodeIdArray)
            ns.SetNodeAttribute(id, "d", id.ToString());
        ns.SetNodeAttribute(4, "s", "four");
        var r = NodesetProcessor.Filter(ns, "d", ConditionType.ge, "3");
        Assert.True(r.Success);
        var f = r.Value!;
        Assert.Equal([3u, 4u, 5u], f.NodeIdArray);
        Assert.Equal("Dense", StorageOf(f, "d"));
        Assert.Equal(4, Value(f, 4, "d"));
        Assert.Equal("four", Value(f, 4, "s"));
        Assert.Null(Value(f, 3, "s"));
    }

    [Fact]
    public void AnalysisResult_IsStoredDense()
    {
        var ns = MakeNodeset(3);
        var net = new Network("net", ns);
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("layer", 1, 2);
        Assert.True(Analyses.DegreeCentralities(net, "layer", "deg").Success);
        Assert.Equal("Dense", StorageOf(ns, "deg"));
        Assert.Equal(1, Value(ns, 1, "deg"));
        Assert.Equal(0, Value(ns, 3, "deg"));
    }

    [Fact]
    public void AnalysisResult_ExistingSparseAttribute_KeepsSparse()
    {
        var ns = MakeNodeset(3);
        ns.DefineNodeAttribute("deg", "int");
        var net = new Network("net", ns);
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("layer", 1, 2);
        Assert.True(Analyses.DegreeCentralities(net, "layer", "deg").Success);
        Assert.Equal("Sparse", StorageOf(ns, "deg"));
        Assert.Equal(1, Value(ns, 2, "deg"));
    }

    [Fact]
    public void ImportNodeAttributes_NewAttribute_IsStoredSparse()
    {
        var ns = MakeNodeset();
        var attrs = new List<(string, NodeAttributeType, Dictionary<uint, string>)>
        {
            ("income", NodeAttributeType.Int, new() { [1] = "100", [3] = "300" })
        };
        Assert.True(NodesetProcessor.ImportNodeAttributes(ns, attrs).Success);
        Assert.Equal("Sparse", StorageOf(ns, "income"));
        Assert.Equal(300, Value(ns, 3, "income"));
        Assert.Null(Value(ns, 2, "income"));
    }

    [Fact]
    public void ImportNodeAttributes_ExistingDenseAttribute_KeepsDense()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("income", "int", "dense");
        var attrs = new List<(string, NodeAttributeType, Dictionary<uint, string>)>
        {
            ("income", NodeAttributeType.Int, new() { [2] = "200" })
        };
        Assert.True(NodesetProcessor.ImportNodeAttributes(ns, attrs).Success);
        Assert.Equal("Dense", StorageOf(ns, "income"));
        Assert.Equal(200, Value(ns, 2, "income"));
    }

    [Fact]
    public void GenerateAttr_IsStoredDense()
    {
        var ns = MakeNodeset();
        Assert.True(Generators.GenerateIntAttr(ns, "r", 1, 3).Success);
        Assert.Equal("Dense", StorageOf(ns, "r"));
        foreach (uint id in ns.NodeIdArray)
            Assert.InRange((int)Value(ns, id, "r")!, 1, 3);
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    /// <summary>A nodeset mixing sparse and dense attributes of all types, with gaps and special values.</summary>
    private static Nodeset MakeMixedNodeset()
    {
        var ns = new Nodeset("mixed");
        foreach (uint id in new uint[] { 5, 1, 1000, 70, 3 })
            ns.AddNode(id);
        ns.DefineNodeAttribute("s_int", "int");
        ns.DefineNodeAttribute("d_float", "float", "dense");
        ns.DefineNodeAttribute("s_str", "string");
        ns.DefineNodeAttribute("d_str", "string", "dense");
        ns.DefineNodeAttribute("d_bool", "bool", "dense");
        ns.DefineNodeAttribute("d_char", "char", "dense");
        ns.SetNodeAttribute(1, "s_int", "-5");
        ns.SetNodeAttribute(1000, "s_int", "7");
        ns.SetNodeAttribute(1, "d_float", "NaN");
        ns.SetNodeAttribute(3, "d_float", "-0.0");
        ns.SetNodeAttribute(70, "d_float", "2.25");
        ns.SetNodeAttribute(5, "s_str", "five");
        ns.SetNodeAttributes(null, "d_str", "same", null);
        ns.SetNodeAttribute(70, "d_str", "other");
        ns.SetNodeAttribute(3, "d_bool", "false");
        ns.SetNodeAttribute(5, "d_bool", "true");
        ns.SetNodeAttribute(1000, "d_char", "z");
        return ns;
    }

    private static void AssertSameAttributes(Nodeset expected, Nodeset actual)
    {
        Assert.Equal(expected.NodeIdArray, actual.NodeIdArray);
        foreach (string attr in expected.GetDefinedAttributeNames())
        {
            Assert.Equal(StorageOf(expected, attr), StorageOf(actual, attr));
            foreach (uint id in expected.NodeIdArray)
            {
                object? e = Value(expected, id, attr), a = Value(actual, id, attr);
                if (e is float ef && a is float af)
                    Assert.Equal(BitConverter.SingleToInt32Bits(ef), BitConverter.SingleToInt32Bits(af));
                else
                    Assert.Equal(e, a);
            }
        }
    }

    [Theory]
    [InlineData(".bin")]
    [InlineData(".bin.gz")]
    public void Binary_RoundTrip_PreservesStorageAndValues(string extension)
    {
        var ns = MakeMixedNodeset();
        AssertSameAttributes(ns, SaveAndLoad(ns, extension));
    }

    [Fact]
    public void Binary_RoundTrip_AfterNodeRemovalAndUndefine()
    {
        var ns = MakeMixedNodeset();
        ns.RemoveNode(70);
        ns.UndefineNodeAttribute("s_str");
        ns.AddNode(9);
        ns.SetNodeAttribute(9, "d_float", "9.5");
        AssertSameAttributes(ns, SaveAndLoad(ns, ".bin"));
    }

    [Theory]
    [InlineData(".tsv")]
    [InlineData(".tsv.gz")]
    public void Tsv_RoundTrip_PreservesStorageAndValues(string extension)
    {
        var ns = MakeMixedNodeset();
        AssertSameAttributes(ns, SaveAndLoad(ns, extension));
    }

    [Fact]
    public void Tsv_HeaderMarksDenseColumns()
    {
        var ns = MakeNodeset(2);
        ns.DefineNodeAttribute("s", "int");
        ns.DefineNodeAttribute("d", "int", "dense");
        string path = TempFile(".tsv");
        FileManager.Save(ns, path);
        string header = File.ReadLines(path).First();
        Assert.Equal("ns\ts:Int\td:Int:dense", header);
    }

    [Fact]
    public void Tsv_LegacyHeader_LoadsAsSparse()
    {
        string path = TempFile(".tsv");
        File.WriteAllText(path, "old\tage:Int\tname:String\n1\t30\tAnn\n2\t\tBo\n");
        var load = FileManager.Load(path, "nodeset");
        Assert.True(load.Success, load.Message);
        var ns = (Nodeset)load.Value!.MainStructure;
        Assert.Equal("Sparse", StorageOf(ns, "age"));
        Assert.Equal(30, Value(ns, 1, "age"));
        Assert.Null(Value(ns, 2, "age"));
        Assert.Equal("Bo", Value(ns, 2, "name"));
    }

    [Fact]
    public void Tsv_InvalidStorageInHeader_FailsToLoad()
    {
        string path = TempFile(".tsv");
        File.WriteAllText(path, "bad\tage:Int:columnar\n1\t30\n");
        Assert.False(FileManager.Load(path, "nodeset").Success);
    }

    /// <summary>
    /// Writes a nodeset in the version 3 binary layout (before storage modes existed): attribute
    /// definitions without storage byte, string pool, nodes without attributes, nodes with attributes.
    /// </summary>
    private static void WriteVersion3Nodeset(string path)
    {
        static void WriteStr(BinaryWriter w, string s)
        {
            byte[] b = Encoding.UTF8.GetBytes(s);
            w.Write((byte)b.Length);
            w.Write(b);
        }
        using var w = new BinaryWriter(File.Create(path));
        w.Write(Encoding.ASCII.GetBytes("TNDS"));
        w.Write((byte)3);
        WriteStr(w, "legacy");
        w.Write((byte)2);                       // two attributes
        WriteStr(w, "age"); w.Write((byte)NodeAttributeType.Int);
        WriteStr(w, "city"); w.Write((byte)NodeAttributeType.String);
        w.Write(1);                             // string pool: one entry
        WriteStr(w, "Umeå");
        w.Write(2);                             // two nodes without attributes
        w.Write(10u); w.Write(20u);
        w.Write(1);                             // one node with attributes
        w.Write(30u);
        w.Write((byte)2);
        w.Write((byte)0); w.Write(44);          // age = 44
        w.Write((byte)1); w.Write(0);           // city = pool[0]
    }

    [Fact]
    public void Binary_LegacyVersion3_LoadsAsSparse()
    {
        string path = TempFile(".bin");
        WriteVersion3Nodeset(path);
        var load = FileManager.Load(path, "nodeset");
        Assert.True(load.Success, load.Message);
        var ns = (Nodeset)load.Value!.MainStructure;
        Assert.Equal([10u, 20u, 30u], ns.NodeIdArray);
        Assert.Equal("Sparse", StorageOf(ns, "age"));
        Assert.Equal(44, Value(ns, 30, "age"));
        Assert.Equal("Umeå", Value(ns, 30, "city"));
        Assert.Null(Value(ns, 10, "age"));
        // And it re-saves (as version 4) and reloads identically
        AssertSameAttributes(ns, SaveAndLoad(ns, ".bin"));
    }

    // ── setattrstorage: in-place conversion ───────────────────────────────────

    [Fact]
    public void SetStorage_SparseToDense_PreservesValuesAndGaps()
    {
        var ns = MakeMixedNodeset();
        var before = MakeMixedNodeset();
        Assert.True(ns.SetNodeAttributeStorage("s_int", "dense").Success);
        Assert.True(ns.SetNodeAttributeStorage("s_str", "dense").Success);
        Assert.Equal("Dense", StorageOf(ns, "s_int"));
        Assert.Equal("Dense", StorageOf(ns, "s_str"));
        foreach (uint id in before.NodeIdArray)
            foreach (string attr in before.GetDefinedAttributeNames())
            {
                object? e = Value(before, id, attr), a = Value(ns, id, attr);
                if (e is float ef && a is float af)
                    Assert.Equal(BitConverter.SingleToInt32Bits(ef), BitConverter.SingleToInt32Bits(af));
                else
                    Assert.Equal(e, a);
            }
        Assert.True(ns.IsModified);
    }

    [Fact]
    public void SetStorage_DenseToSparse_PreservesValuesAndGaps()
    {
        var ns = MakeMixedNodeset();
        Assert.True(ns.SetNodeAttributeStorage("d_float", "sparse").Success);
        Assert.True(ns.SetNodeAttributeStorage("d_str", "sparse").Success);
        Assert.Equal("Sparse", StorageOf(ns, "d_float"));
        Assert.Equal("Sparse", StorageOf(ns, "d_str"));
        Assert.True(float.IsNaN((float)Value(ns, 1, "d_float")!));
        Assert.Equal(2.25f, Value(ns, 70, "d_float"));
        Assert.Null(Value(ns, 5, "d_float"));
        Assert.Equal("other", Value(ns, 70, "d_str"));
        Assert.Equal("same", Value(ns, 1, "d_str"));
        // Other sparse attributes on the same nodes are unaffected
        Assert.Equal(-5, Value(ns, 1, "s_int"));
        Assert.Equal("five", Value(ns, 5, "s_str"));
    }

    [Fact]
    public void SetStorage_RoundTrip_ThenSetAndRemoveStillWork()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int");
        ns.SetNodeAttribute(2, "a", "20");
        ns.SetNodeAttributeStorage("a", "dense");
        ns.SetNodeAttribute(3, "a", "30");
        ns.SetNodeAttributeStorage("a", "sparse");
        ns.SetNodeAttribute(4, "a", "40");
        Assert.True(ns.RemoveNodeAttribute(2, "a").Success);
        Assert.Null(Value(ns, 2, "a"));
        Assert.Equal(30, Value(ns, 3, "a"));
        Assert.Equal(40, Value(ns, 4, "a"));
    }

    [Fact]
    public void SetStorage_SameStorage_IsNoOp()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int");
        ns.IsModified = false;
        var r = ns.SetNodeAttributeStorage("a", "sparse");
        Assert.True(r.Success);
        Assert.Contains("already", r.Message);
        Assert.False(ns.IsModified);
    }

    [Fact]
    public void SetStorage_UnknownAttributeOrInvalidStorage_Fails()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int");
        Assert.Equal("AttributeUnknown", ns.SetNodeAttributeStorage("nope", "dense").Code);
        Assert.Equal("InvalidAttributeStorage", ns.SetNodeAttributeStorage("a", "columnar").Code);
        Assert.Equal("Sparse", StorageOf(ns, "a"));
    }

    [Fact]
    public void SetStorage_Auto_FullyPopulated_ChoosesDense()
    {
        var ns = new Nodeset("ns", 1000);
        ns.DefineNodeAttribute("other", "int");
        ns.DefineNodeAttribute("a", "float");
        ns.SetNodeAttributes(null, "other", "1", null);
        ns.SetNodeAttributes(null, "a", "1.5", null);
        var r = ns.SetNodeAttributeStorage("a", "auto");
        Assert.True(r.Success);
        Assert.Contains("auto: estimated", r.Message);
        Assert.Equal("Dense", StorageOf(ns, "a"));
        Assert.Equal(1.5f, Value(ns, 999, "a"));
    }

    [Fact]
    public void SetStorage_Auto_RareAttributeAmongOtherSparse_ChoosesSparse()
    {
        var ns = new Nodeset("ns", 1000);
        ns.DefineNodeAttribute("other", "int");
        ns.DefineNodeAttribute("a", "int", "dense");
        ns.SetNodeAttributes(null, "other", "1", null);
        for (uint id = 0; id < 50; id++)   // 5% fill
            ns.SetNodeAttribute(id, "a", "7");
        Assert.True(ns.SetNodeAttributeStorage("a", "auto").Success);
        Assert.Equal("Sparse", StorageOf(ns, "a"));
        Assert.Equal(7, Value(ns, 10, "a"));
        Assert.Null(Value(ns, 500, "a"));
    }

    [Fact]
    public void SetStorage_Auto_IsStable_SecondCallIsNoOp()
    {
        var ns = new Nodeset("ns", 1000);
        ns.DefineNodeAttribute("other", "int");
        ns.DefineNodeAttribute("a", "int");
        ns.SetNodeAttributes(null, "other", "1", null);
        ns.SetNodeAttributes(null, "a", "1", null);
        ns.SetNodeAttributeStorage("a", "auto");
        string first = StorageOf(ns, "a");
        var r = ns.SetNodeAttributeStorage("a", "auto");
        Assert.Contains("already", r.Message);
        Assert.Equal(first, StorageOf(ns, "a"));
    }

    [Fact]
    public void SetStorage_ConvertedAttribute_SurvivesBinaryRoundTrip()
    {
        var ns = MakeMixedNodeset();
        ns.SetNodeAttributeStorage("s_int", "dense");
        ns.SetNodeAttributeStorage("d_char", "sparse");
        AssertSameAttributes(ns, SaveAndLoad(ns, ".bin"));
    }

    // ── Modified flag: attribute value changes must mark the nodeset modified ──

    [Fact]
    public void SetAttr_MarksModified()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int");
        ns.IsModified = false;
        Assert.True(ns.SetNodeAttribute(1, "a", "5").Success);
        Assert.True(ns.IsModified);
    }

    [Fact]
    public void SetAttr_Failed_DoesNotMarkModified()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int");
        ns.IsModified = false;
        Assert.False(ns.SetNodeAttribute(1, "a", "notanumber").Success);
        Assert.False(ns.IsModified);
    }

    [Fact]
    public void SetAttrs_MarksModified()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int");
        ns.IsModified = false;
        Assert.True(ns.SetNodeAttributes(null, "a", "5", null).Success);
        Assert.True(ns.IsModified);
    }

    [Fact]
    public void RemoveAttr_MarksModified()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int");
        ns.SetNodeAttribute(1, "a", "5");
        ns.IsModified = false;
        Assert.True(ns.RemoveNodeAttribute(1, "a").Success);
        Assert.True(ns.IsModified);
    }

    [Fact]
    public void GenerateAttr_MarksModified()
    {
        var ns = MakeNodeset();
        ns.IsModified = false;
        Assert.True(Generators.GenerateIntAttr(ns, "g", 1, 3).Success);
        Assert.True(ns.IsModified);
    }

    [Fact]
    public void NetworkSave_AfterSetAttr_ResavesNodesetValues()
    {
        // Saving a network re-saves its nodeset only if the nodeset is marked modified,
        // so a value set with setattr() must survive a save of the network alone.
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("a", "int");
        ns.DefineNodeAttribute("d", "float", "dense");
        string nsPath = TempFile(".bin");
        Assert.True(FileManager.Save(ns, nsPath).Success);
        var net = new Network("net", ns);
        net.AddLayerOneMode("layer", EdgeDirectionality.Undirected, EdgeType.Binary, false);
        net.AddEdge("layer", 1, 2);
        string netPath = TempFile(".bin");
        Assert.True(FileManager.Save(net, netPath).Success);

        ns.SetNodeAttribute(3, "a", "33");
        ns.SetNodeAttributes(null, "d", "1.5", null);
        Generators.GenerateIntAttr(ns, "g", 7, 7);
        Assert.True(FileManager.Save(net, netPath).Success);

        var load = FileManager.Load(netPath, "network");
        Assert.True(load.Success, load.Message);
        var loadedNs = ((Network)load.Value!.MainStructure).Nodeset;
        Assert.Equal(33, Value(loadedNs, 3, "a"));
        Assert.Equal(1.5f, Value(loadedNs, 5, "d"));
        Assert.Equal(7, Value(loadedNs, 1, "g"));
        Assert.Equal("Dense", StorageOf(loadedNs, "d"));
    }

    // ── Memory estimate ───────────────────────────────────────────────────────

    [Fact]
    public void EstimatedBytes_DenseIsSmallerThanSparse_ForFullyPopulatedAttribute()
    {
        var sparse = new Nodeset("s", 10_000);
        var dense = new Nodeset("d", 10_000);
        sparse.DefineNodeAttribute("a", "float");
        dense.DefineNodeAttribute("a", "float", "dense");
        sparse.SetNodeAttributes(null, "a", "1.0", null);
        dense.SetNodeAttributes(null, "a", "1.0", null);
        Assert.True(dense.GetEstimatedBytes() < sparse.GetEstimatedBytes());
    }
}