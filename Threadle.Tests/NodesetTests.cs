using Threadle.Core.Model;

namespace Threadle.Tests;

public class NodesetTests
{
    // ── Construction ────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_Empty_HasZeroNodes()
    {
        var nodeset = new Nodeset("ns");
        Assert.Equal(0, nodeset.Count);
    }

    [Fact]
    public void Constructor_WithCount_CreatesCorrectNumberOfNodes()
    {
        var nodeset = new Nodeset("ns", 5);
        Assert.Equal(5, nodeset.Count);
    }

    [Fact]
    public void Constructor_WithCount_NodeIdsStartAtZero()
    {
        var nodeset = new Nodeset("ns", 3);
        uint[] ids = nodeset.NodeIdArray;
        Assert.Contains(0u, ids);
        Assert.Contains(1u, ids);
        Assert.Contains(2u, ids);
    }

    // ── AddNode ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddNode_NewId_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        var result = nodeset.AddNode(42);
        Assert.True(result.Success);
    }

    [Fact]
    public void AddNode_NewId_IncreasesCount()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        Assert.Equal(2, nodeset.Count);
    }

    [Fact]
    public void AddNode_DuplicateId_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(10);
        var result = nodeset.AddNode(10);
        Assert.False(result.Success);
    }

    [Fact]
    public void AddNode_DuplicateId_CountUnchanged()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(10);
        nodeset.AddNode(10);
        Assert.Equal(1, nodeset.Count);
    }

    // ── RemoveNode ───────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveNode_ExistingId_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(5);
        var result = nodeset.RemoveNode(5);
        Assert.True(result.Success);
    }

    [Fact]
    public void RemoveNode_ExistingId_DecreasesCount()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(5);
        nodeset.RemoveNode(5);
        Assert.Equal(0, nodeset.Count);
    }

    [Fact]
    public void RemoveNode_NonExistentId_Fails()
    {
        var nodeset = new Nodeset("ns");
        var result = nodeset.RemoveNode(999);
        Assert.False(result.Success);
    }

    // ── Contains ────────────────────────────────────────────────────────────────

    [Fact]
    public void Contains_AfterAdd_ReturnsTrue()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(7);
        Assert.True(nodeset.NodeIdArray.Contains(7u));
    }

    [Fact]
    public void Contains_AfterRemove_ReturnsFalse()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(7);
        nodeset.RemoveNode(7);
        Assert.DoesNotContain(7u, nodeset.NodeIdArray);
    }

    // ── NodeIdArray ──────────────────────────────────────────────────────────────

    [Fact]
    public void NodeIdArray_IsSorted()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(30);
        nodeset.AddNode(10);
        nodeset.AddNode(20);
        uint[] ids = nodeset.NodeIdArray;
        Assert.Equal(ids.OrderBy(x => x).ToArray(), ids);
    }

    [Fact]
    public void NodeIdArray_Empty_ReturnsEmptyArray()
    {
        var nodeset = new Nodeset("ns");
        Assert.Empty(nodeset.NodeIdArray);
    }

    // ── GetNodeIdByIndex ─────────────────────────────────────────────────────────

    [Fact]
    public void GetNodeIdByIndex_ValidIndex_ReturnsId()
    {
        var nodeset = new Nodeset("ns", 3);  // nodes 0,1,2
        uint? id = nodeset.GetNodeIdByIndex(0);
        Assert.NotNull(id);
    }

    [Fact]
    public void GetNodeIdByIndex_OutOfRange_ReturnsNull()
    {
        var nodeset = new Nodeset("ns", 2);  // nodes 0,1
        uint? id = nodeset.GetNodeIdByIndex(99);
        Assert.Null(id);
    }

    // ── IsModified ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsModified_AfterAddNode_IsTrue()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        Assert.True(nodeset.IsModified);
    }

    [Fact]
    public void IsModified_FreshFromCountConstructor_IsFalse()
    {
        var nodeset = new Nodeset("ns", 3);
        Assert.False(nodeset.IsModified);
    }
}
