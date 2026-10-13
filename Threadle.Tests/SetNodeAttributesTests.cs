using Threadle.Core.Model;

namespace Threadle.Tests;

public class SetNodeAttributesTests
{
    /// <summary>
    /// Nodes 1-4, with attributes 'state' (string) and 'age' (int); node 1 has state "R" and age 30.
    /// </summary>
    private static Nodeset MakeNodeset()
    {
        var ns = new Nodeset("ns");
        for (uint i = 1; i <= 4; i++) ns.AddNode(i);
        ns.DefineNodeAttribute("state", "string");
        ns.DefineNodeAttribute("age", "int");
        ns.SetNodeAttribute(1, "state", "R");
        ns.SetNodeAttribute(1, "age", "30");
        return ns;
    }

    private static string? State(Nodeset ns, uint nodeId)
    {
        var result = ns.GetNodeAttribute(nodeId, "state");
        return result.Success ? ns.StringPool[(int)result.Value.Value.GetValue(result.Value.Type)!] : null;
    }

    private static int? Age(Nodeset ns, uint nodeId)
    {
        var result = ns.GetNodeAttribute(nodeId, "age");
        return result.Success ? (int)result.Value.Value.GetValue(result.Value.Type)! : null;
    }

    [Fact]
    public void SetNodeAttributes_CommonValue_SetsListedNodes()
    {
        var ns = MakeNodeset();
        var result = ns.SetNodeAttributes([2, 3], "state", "I", null);
        Assert.True(result.Success);
        Assert.Contains("2 node(s)", result.Message);
        Assert.Equal("I", State(ns, 2));
        Assert.Equal("I", State(ns, 3));
        Assert.Equal("R", State(ns, 1));
        Assert.Null(State(ns, 4));
    }

    [Fact]
    public void SetNodeAttributes_NoNodes_SetsAllNodes()
    {
        var ns = MakeNodeset();
        var result = ns.SetNodeAttributes(null, "state", "S", null);
        Assert.True(result.Success);
        Assert.Contains("4 node(s)", result.Message);
        for (uint i = 1; i <= 4; i++)
            Assert.Equal("S", State(ns, i));
        // Other attributes are untouched
        Assert.Equal(30, Age(ns, 1));
    }

    [Fact]
    public void SetNodeAttributes_PerNodeValues_SetsEachNode()
    {
        var ns = MakeNodeset();
        var result = ns.SetNodeAttributes([4, 2], "age", null, ["40", "20"]);
        Assert.True(result.Success);
        Assert.Equal(40, Age(ns, 4));
        Assert.Equal(20, Age(ns, 2));
        Assert.Null(Age(ns, 3));
    }

    [Fact]
    public void SetNodeAttributes_DuplicateNodes_LastValueWins()
    {
        var ns = MakeNodeset();
        var result = ns.SetNodeAttributes([2, 3, 2], "state", null, ["I", "S", "R"]);
        Assert.True(result.Success);
        Assert.Contains("2 node(s)", result.Message);
        Assert.Equal("R", State(ns, 2));
        Assert.Equal("S", State(ns, 3));
    }

    [Fact]
    public void SetNodeAttributes_UnknownNode_FailsAndChangesNothing()
    {
        var ns = MakeNodeset();
        var result = ns.SetNodeAttributes([2, 99], "state", "I", null);
        Assert.False(result.Success);
        Assert.Equal("NodeNotFound", result.Code);
        Assert.Null(State(ns, 2));
    }

    [Fact]
    public void SetNodeAttributes_UnparsableValue_FailsAndChangesNothing()
    {
        var ns = MakeNodeset();
        var result = ns.SetNodeAttributes([1, 2, 3], "age", null, ["41", "notanint", "43"]);
        Assert.False(result.Success);
        Assert.Equal("ParseAttributeValueError", result.Code);
        Assert.Equal(30, Age(ns, 1));
        Assert.Null(Age(ns, 2));
    }

    [Fact]
    public void SetNodeAttributes_FailedCall_LeavesStringPoolUnchanged()
    {
        var ns = MakeNodeset();
        int poolSize = ns.StringPool.Count();
        ns.SetNodeAttributes([2, 99], "state", "neverstored", null);
        Assert.Equal(poolSize, ns.StringPool.Count());
    }

    [Fact]
    public void SetNodeAttributes_UnknownAttribute_Fails()
    {
        var result = MakeNodeset().SetNodeAttributes([1], "ghost", "x", null);
        Assert.False(result.Success);
        Assert.Equal("AttributeUnknown", result.Code);
    }

    [Fact]
    public void SetNodeAttributes_LengthMismatch_Fails()
    {
        var ns = MakeNodeset();
        var result = ns.SetNodeAttributes([2, 3], "state", null, ["I"]);
        Assert.False(result.Success);
        Assert.Equal("LengthMismatch", result.Code);
        Assert.Null(State(ns, 2));
    }

    [Fact]
    public void SetNodeAttributes_PerNodeValuesWithoutNodes_Fails()
    {
        var result = MakeNodeset().SetNodeAttributes(null, "state", null, ["I", "S", "R", "S"]);
        Assert.False(result.Success);
        Assert.Equal("MissingNodes", result.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetNodeAttributes_BothOrNeitherValueKind_Fails(bool both)
    {
        var result = MakeNodeset().SetNodeAttributes([1], "state", both ? "I" : null, both ? ["I"] : null);
        Assert.False(result.Success);
        Assert.Equal("InvalidArguments", result.Code);
    }
}
