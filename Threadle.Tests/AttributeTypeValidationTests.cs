using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using Threadle.Core.Utilities.Enums;

namespace Threadle.Tests;

/// <summary>
/// Attribute values that do not fit their type (an integer outside the 32-bit Int range, text in a numeric
/// column, ...) are never silently dropped: importnodeattributes fails as a whole with the count and the
/// first offending value; loading a nodeset TSV with such a value fails with its line; setattr explains
/// the range; whole numbers too large to be exact as Float are accepted with a warning; a GraphML integer
/// key with out-of-range values is imported as String.
/// </summary>
public class AttributeTypeValidationTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    private string TempFile(string ext, string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"threadle_attrtype_test_{Guid.NewGuid()}{ext}");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    private static Nodeset MakeNodeset()
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 3; i++) ns.AddNode(i);
        return ns;
    }

    [Fact]
    public void ImportNodeAttributes_IntOverflow_FailsWithCountAndFirstValue_NothingChanged()
    {
        var ns = MakeNodeset();
        string path = TempFile(".txt", "id\ttract:int\tage:int\n1\t26161400100\t30\n2\t26161400200\t40\n3\t\t50\n4\t1\t60\n");
        var r = FileManager.ImportNodeAttributes(path, ns, addMissingNodes: true);
        Assert.False(r.Success);
        Assert.Equal("ParseAttributeValueError", r.Code);
        Assert.Contains("2 value(s) in column 'tract'", r.Message);
        Assert.Contains("26161400", r.Message);
        Assert.Contains("outside the range of Int", r.Message);
        // Nothing imported: no attribute defined, no node added
        Assert.False(ns.GetNodeAttribute(1, "age").Success);
        Assert.Equal(3, ns.Count);
    }

    [Fact]
    public void ImportNodeAttributes_TextInNumericColumn_Fails()
    {
        var ns = MakeNodeset();
        string path = TempFile(".txt", "id\tscore:float\n1\t0.5\n2\tn/a\n");
        var r = FileManager.ImportNodeAttributes(path, ns);
        Assert.False(r.Success);
        Assert.Contains("'n/a' is not a number", r.Message);
    }

    [Fact]
    public void ImportNodeAttributes_EmptyCells_AreMissingNotErrors()
    {
        var ns = MakeNodeset();
        string path = TempFile(".txt", "id\tage:int\n1\t30\n2\t\n3\t50\n");
        var r = FileManager.ImportNodeAttributes(path, ns);
        Assert.True(r.Success, r.Message);
        Assert.Equal(30, (int)ns.GetNodeAttribute(1, "age").Value.Value.GetValue(NodeAttributeType.Int)!);
        Assert.False(ns.GetNodeAttribute(2, "age").Success);
    }

    [Fact]
    public void ImportNodeAttributes_LargeWholeNumberAsFloat_ImportedWithWarning()
    {
        var ns = MakeNodeset();
        string path = TempFile(".txt", "id\ttract:float\tx:float\n1\t26161400100\t1.5\n2\t12\t2\n");
        var r = FileManager.ImportNodeAttributes(path, ns);
        Assert.True(r.Success, r.Message);
        Assert.Contains("Warning: 1 value(s) in column 'tract'", r.Message);
        Assert.DoesNotContain("column 'x'", r.Message);
        Assert.True(ns.GetNodeAttribute(1, "tract").Success);
    }

    [Fact]
    public void ImportNodeAttributes_ValuesForSkippedNodes_AreNotChecked()
    {
        // Node 9 is not in the nodeset and missing nodes are not added: its bad value is ignored
        var ns = MakeNodeset();
        string path = TempFile(".txt", "id\tage:int\n1\t30\n9\t26161400100\n");
        var r = FileManager.ImportNodeAttributes(path, ns, addMissingNodes: false);
        Assert.True(r.Success, r.Message);
    }

    [Fact]
    public void SetAttr_IntOverflow_ExplainsRange()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("tract", "int");
        var r = ns.SetNodeAttribute(1, "tract", "26161400100");
        Assert.False(r.Success);
        Assert.Contains("outside the range of Int", r.Message);
        var many = ns.SetNodeAttributes([1, 2], "tract", null, ["5", "99999999999"]);
        Assert.False(many.Success);
        Assert.Contains("outside the range of Int", many.Message);
        Assert.False(ns.GetNodeAttribute(1, "tract").Success); // all or nothing
    }

    [Fact]
    public void LoadNodesetTsv_ValueNotFittingType_FailsWithLine()
    {
        var ns = MakeNodeset();
        ns.DefineNodeAttribute("age", "int");
        ns.SetNodeAttribute(1, "age", "30");
        ns.SetNodeAttribute(2, "age", "40");
        string path = TempFile(".tsv", "");
        Assert.True(FileManager.Save(ns, path).Success);
        string text = File.ReadAllText(path).Replace("\t40", "\t26161400100");
        File.WriteAllText(path, text);
        var r = FileManager.Load(path, "nodeset");
        Assert.False(r.Success);
        Assert.Contains("node 2", r.Message);
        Assert.Contains("outside the range of Int", r.Message);
    }

    [Fact]
    public void GraphmlImport_LongValuesOutsideIntRange_ImportedAsString()
    {
        string graphml = """
            <?xml version='1.0' encoding='utf-8'?>
            <graphml xmlns="http://graphml.graphdrawing.org/xmlns">
              <key id="d0" for="node" attr.name="tract" attr.type="long"/>
              <key id="d1" for="node" attr.name="age" attr.type="long"/>
              <graph id="g" edgedefault="undirected">
                <node id="1"><data key="d0">26161400100</data><data key="d1">31</data></node>
                <node id="2"><data key="d0">26161400200</data><data key="d1">29</data></node>
                <edge source="1" target="2"/>
              </graph>
            </graphml>
            """;
        var r = FileManager.ImportNetwork(TempFile(".graphml", graphml), ImportFormat.Graphml, "layer", "weight", "id");
        Assert.True(r.Success, r.Message);
        var ns = (Nodeset)r.Value!.AdditionalStructures["nodeset"];
        Assert.Equal("26161400100", ns.GetNodeAttributeString(1, "tract").Value);
        Assert.Equal(NodeAttributeType.Int, ns.GetNodeAttribute(1, "age").Value.Type);
    }
}