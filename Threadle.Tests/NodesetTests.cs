using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

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

    // ── DefineNodeAttribute ──────────────────────────────────────────────────

    [Fact]
    public void DefineNodeAttribute_IntType_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        var result = nodeset.DefineNodeAttribute("age", "int");
        Assert.True(result.Success);
    }

    [Fact]
    public void DefineNodeAttribute_FloatType_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        var result = nodeset.DefineNodeAttribute("score", "float");
        Assert.True(result.Success);
    }

    [Fact]
    public void DefineNodeAttribute_BoolType_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        var result = nodeset.DefineNodeAttribute("active", "bool");
        Assert.True(result.Success);
    }

    [Fact]
    public void DefineNodeAttribute_CharType_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        var result = nodeset.DefineNodeAttribute("gender", "char");
        Assert.True(result.Success);
    }

    [Fact]
    public void DefineNodeAttribute_DuplicateName_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.DefineNodeAttribute("age", "int");
        Assert.False(result.Success);
    }

    [Fact]
    public void DefineNodeAttribute_DuplicateName_HasCorrectErrorCode()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.DefineNodeAttribute("age", "float");
        Assert.Equal("AttributeAlreadyExists", result.Code);
    }

    [Fact]
    public void DefineNodeAttribute_EmptyName_Fails()
    {
        var nodeset = new Nodeset("ns");
        var result = nodeset.DefineNodeAttribute("", "int");
        Assert.False(result.Success);
    }

    [Fact]
    public void DefineNodeAttribute_InvalidType_Fails()
    {
        var nodeset = new Nodeset("ns");
        var result = nodeset.DefineNodeAttribute("age", "string");
        Assert.False(result.Success);
    }

    [Fact]
    public void DefineNodeAttribute_MultipleAttributes_AllSucceed()
    {
        var nodeset = new Nodeset("ns");
        Assert.True(nodeset.DefineNodeAttribute("age", "int").Success);
        Assert.True(nodeset.DefineNodeAttribute("score", "float").Success);
        Assert.True(nodeset.DefineNodeAttribute("active", "bool").Success);
        Assert.True(nodeset.DefineNodeAttribute("gender", "char").Success);
    }

    // ── UndefineNodeAttribute ────────────────────────────────────────────────

    [Fact]
    public void UndefineNodeAttribute_ExistingAttribute_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.UndefineNodeAttribute("age");
        Assert.True(result.Success);
    }

    [Fact]
    public void UndefineNodeAttribute_NonExistentAttribute_Fails()
    {
        var nodeset = new Nodeset("ns");
        var result = nodeset.UndefineNodeAttribute("ghost");
        Assert.False(result.Success);
    }

    [Fact]
    public void UndefineNodeAttribute_AfterUndefine_GetAttributeFails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "30");
        nodeset.UndefineNodeAttribute("age");
        var result = nodeset.GetNodeAttribute(1, "age");
        Assert.False(result.Success);
    }

    [Fact]
    public void UndefineNodeAttribute_AfterUndefine_CanBeRedefined()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.UndefineNodeAttribute("age");
        var result = nodeset.DefineNodeAttribute("age", "float");
        Assert.True(result.Success);
    }

    [Fact]
    public void UndefineNodeAttribute_RemovesValueFromAllNodes()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "10");
        nodeset.SetNodeAttribute(2, "age", "20");
        nodeset.UndefineNodeAttribute("age");
        Assert.False(nodeset.GetNodeAttribute(1, "age").Success);
        Assert.False(nodeset.GetNodeAttribute(2, "age").Success);
    }

    [Fact]
    public void UndefineNodeAttribute_NodeRemainsInNodeset()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "25");
        nodeset.UndefineNodeAttribute("age");
        Assert.Contains(1u, nodeset.NodeIdArray);
    }

    // ── SetNodeAttribute ─────────────────────────────────────────────────────

    [Fact]
    public void SetNodeAttribute_IntValue_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.SetNodeAttribute(1, "age", "42");
        Assert.True(result.Success);
    }

    [Fact]
    public void SetNodeAttribute_FloatValue_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("score", "float");
        var result = nodeset.SetNodeAttribute(1, "score", "3.14");
        Assert.True(result.Success);
    }

    [Fact]
    public void SetNodeAttribute_BoolValue_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("active", "bool");
        var result = nodeset.SetNodeAttribute(1, "active", "true");
        Assert.True(result.Success);
    }

    [Fact]
    public void SetNodeAttribute_CharValue_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("gender", "char");
        var result = nodeset.SetNodeAttribute(1, "gender", "F");
        Assert.True(result.Success);
    }

    [Fact]
    public void SetNodeAttribute_OverwritesExistingValue_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "10");
        var result = nodeset.SetNodeAttribute(1, "age", "99");
        Assert.True(result.Success);
    }

    [Fact]
    public void SetNodeAttribute_NonExistentNode_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.SetNodeAttribute(999, "age", "42");
        Assert.False(result.Success);
    }

    [Fact]
    public void SetNodeAttribute_NonExistentNode_HasCorrectErrorCode()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.SetNodeAttribute(999, "age", "42");
        Assert.Equal("NodeNotFound", result.Code);
    }

    [Fact]
    public void SetNodeAttribute_UndefinedAttribute_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        var result = nodeset.SetNodeAttribute(1, "age", "42");
        Assert.False(result.Success);
    }

    [Fact]
    public void SetNodeAttribute_UndefinedAttribute_HasCorrectErrorCode()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        var result = nodeset.SetNodeAttribute(1, "age", "42");
        Assert.Equal("AttributeUnknown", result.Code);
    }

    [Fact]
    public void SetNodeAttribute_InvalidIntFormat_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.SetNodeAttribute(1, "age", "notanumber");
        Assert.False(result.Success);
    }

    [Fact]
    public void SetNodeAttribute_InvalidFloatFormat_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("score", "float");
        var result = nodeset.SetNodeAttribute(1, "score", "notafloat");
        Assert.False(result.Success);
    }

    [Fact]
    public void SetNodeAttribute_InvalidBoolFormat_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("active", "bool");
        var result = nodeset.SetNodeAttribute(1, "active", "maybe");
        Assert.False(result.Success);
    }

    [Fact]
    public void SetNodeAttribute_CharValueTooLong_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("gender", "char");
        var result = nodeset.SetNodeAttribute(1, "gender", "AB");
        Assert.False(result.Success);
    }

    [Fact]
    public void SetNodeAttribute_InvalidIntFormat_HasCorrectErrorCode()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.SetNodeAttribute(1, "age", "notanumber");
        Assert.Equal("ParseAttributeValueError", result.Code);
    }

    // ── GetNodeAttribute ─────────────────────────────────────────────────────

    [Fact]
    public void GetNodeAttribute_AfterSetInt_ReturnsCorrectValue()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "42");
        var result = nodeset.GetNodeAttribute(1, "age");
        Assert.True(result.Success);
        var (nav, type) = result.Value;
        Assert.Equal(42, (int)nav.GetValue(type));
    }

    [Fact]
    public void GetNodeAttribute_AfterSetFloat_ReturnsCorrectValue()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("score", "float");
        nodeset.SetNodeAttribute(1, "score", "3.5");
        var result = nodeset.GetNodeAttribute(1, "score");
        Assert.True(result.Success);
        var (nav, type) = result.Value;
        Assert.Equal(3.5f, (float)nav.GetValue(type), 4);
    }

    [Fact]
    public void GetNodeAttribute_AfterSetBoolTrue_ReturnsTrue()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("active", "bool");
        nodeset.SetNodeAttribute(1, "active", "true");
        var result = nodeset.GetNodeAttribute(1, "active");
        Assert.True(result.Success);
        var (nav, type) = result.Value;
        Assert.Equal(true, (bool)nav.GetValue(type));
    }

    [Fact]
    public void GetNodeAttribute_AfterSetBoolFalse_ReturnsFalse()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("active", "bool");
        nodeset.SetNodeAttribute(1, "active", "false");
        var result = nodeset.GetNodeAttribute(1, "active");
        Assert.True(result.Success);
        var (nav, type) = result.Value;
        Assert.Equal(false, (bool)nav.GetValue(type));
    }

    [Fact]
    public void GetNodeAttribute_AfterSetChar_ReturnsCorrectValue()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("gender", "char");
        nodeset.SetNodeAttribute(1, "gender", "M");
        var result = nodeset.GetNodeAttribute(1, "gender");
        Assert.True(result.Success);
        var (nav, type) = result.Value;
        Assert.Equal('M', (char)nav.GetValue(type));
    }

    [Fact]
    public void GetNodeAttribute_AfterOverwrite_ReturnsNewValue()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "10");
        nodeset.SetNodeAttribute(1, "age", "99");
        var result = nodeset.GetNodeAttribute(1, "age");
        var (nav, type) = result.Value;
        Assert.Equal(99, (int)nav.GetValue(type));
    }

    [Fact]
    public void GetNodeAttribute_NonExistentNode_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.GetNodeAttribute(999, "age");
        Assert.False(result.Success);
        Assert.Equal("NodeNotFound", result.Code);
    }

    [Fact]
    public void GetNodeAttribute_UndefinedAttribute_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        var result = nodeset.GetNodeAttribute(1, "age");
        Assert.False(result.Success);
        Assert.Equal("AttributeUnknown", result.Code);
    }

    [Fact]
    public void GetNodeAttribute_DefinedButNotSet_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.GetNodeAttribute(1, "age");
        Assert.False(result.Success);
        Assert.Equal("AttributeNotSet", result.Code);
    }

    [Fact]
    public void GetNodeAttribute_ReturnsCorrectType()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "5");
        var result = nodeset.GetNodeAttribute(1, "age");
        Assert.Equal(NodeAttributeType.Int, result.Value.Type);
    }

    // ── GetMultipleNodeAttributes ────────────────────────────────────────────

    [Fact]
    public void GetMultipleNodeAttributes_AllHaveValue_ReturnsAll()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.AddNode(3);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "10");
        nodeset.SetNodeAttribute(2, "age", "20");
        nodeset.SetNodeAttribute(3, "age", "30");
        var result = nodeset.GetMultipleNodeAttributes(new uint[] { 1, 2, 3 }, "age");
        Assert.True(result.Success);
        Assert.Equal(3, result.Value!.Count);
        Assert.Equal(10, (int)result.Value[1]!);
        Assert.Equal(20, (int)result.Value[2]!);
        Assert.Equal(30, (int)result.Value[3]!);
    }

    [Fact]
    public void GetMultipleNodeAttributes_SomeNodesLackValue_ReturnsNullForMissing()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "10");
        // Node 2 has no value set
        var result = nodeset.GetMultipleNodeAttributes(new uint[] { 1, 2 }, "age");
        Assert.True(result.Success);
        Assert.Equal(10, (int)result.Value![1]!);
        Assert.Null(result.Value[2]);
    }

    [Fact]
    public void GetMultipleNodeAttributes_UndefinedAttribute_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        var result = nodeset.GetMultipleNodeAttributes(new uint[] { 1 }, "ghost");
        Assert.False(result.Success);
        Assert.Equal("AttributeUnknown", result.Code);
    }

    [Fact]
    public void GetMultipleNodeAttributes_NonExistentNodesSkipped()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "10");
        var result = nodeset.GetMultipleNodeAttributes(new uint[] { 1, 999 }, "age");
        Assert.True(result.Success);
        Assert.True(result.Value!.ContainsKey(1));
        Assert.False(result.Value.ContainsKey(999));
    }

    // ── RemoveNodeAttribute ──────────────────────────────────────────────────

    [Fact]
    public void RemoveNodeAttribute_ExistingAttribute_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "42");
        var result = nodeset.RemoveNodeAttribute(1, "age");
        Assert.True(result.Success);
    }

    [Fact]
    public void RemoveNodeAttribute_AfterRemove_GetFails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "42");
        nodeset.RemoveNodeAttribute(1, "age");
        var result = nodeset.GetNodeAttribute(1, "age");
        Assert.False(result.Success);
    }

    [Fact]
    public void RemoveNodeAttribute_NonExistentNode_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.RemoveNodeAttribute(999, "age");
        Assert.False(result.Success);
        Assert.Equal("NodeNotFound", result.Code);
    }

    [Fact]
    public void RemoveNodeAttribute_UndefinedAttribute_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        var result = nodeset.RemoveNodeAttribute(1, "ghost");
        Assert.False(result.Success);
        Assert.Equal("AttributeUnknown", result.Code);
    }

    [Fact]
    public void RemoveNodeAttribute_AttributeNotSetOnNode_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        // age is defined but never set on node 1
        var result = nodeset.RemoveNodeAttribute(1, "age");
        Assert.False(result.Success);
    }

    [Fact]
    public void RemoveNodeAttribute_NodeRemainsInNodeset()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "42");
        nodeset.RemoveNodeAttribute(1, "age");
        Assert.Contains(1u, nodeset.NodeIdArray);
    }

    [Fact]
    public void RemoveNodeAttribute_OnlyRemovesTargetAttribute_OtherAttributeIntact()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.DefineNodeAttribute("score", "float");
        nodeset.SetNodeAttribute(1, "age", "42");
        nodeset.SetNodeAttribute(1, "score", "9.5");
        nodeset.RemoveNodeAttribute(1, "age");
        var scoreResult = nodeset.GetNodeAttribute(1, "score");
        Assert.True(scoreResult.Success);
    }
}
