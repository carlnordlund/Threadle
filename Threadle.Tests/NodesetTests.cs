using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Processing.Enums;
using Threadle.Core.Utilities;

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

    // ── NodeIdArray cache invalidation ───────────────────────────────────────────
    // NodeIdArray always caches its result (_nodeIdCache): every mutation that changes
    // nodeset membership must invalidate that cache, or a prior read leaves NodeIdArray
    // silently stale.

    [Fact]
    public void NodeIdArray_WithCacheActive_ReflectsNodeAddedAfterFirstRead()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        _ = nodeset.NodeIdArray; // populate the cache

        nodeset.AddNode(2);

        Assert.Equal(2, nodeset.NodeIdArray.Length);
        Assert.Contains(2u, nodeset.NodeIdArray);
    }

    [Fact]
    public void NodeIdArray_WithCacheActive_ReflectsNodeRemovedAfterFirstRead()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        _ = nodeset.NodeIdArray; // populate the cache

        nodeset.RemoveNode(2);

        Assert.Equal(1, nodeset.NodeIdArray.Length);
        Assert.DoesNotContain(2u, nodeset.NodeIdArray);
    }

    [Fact]
    public void NodeIdArray_WithCacheActive_UnaffectedByAttributeOnlyChanges()
    {
        // Setting/removing a node attribute moves a node between the "with" and "without
        // attributes" internal buckets without changing nodeset membership, so it must not
        // require (and must not break) cache correctness either.
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        _ = nodeset.NodeIdArray; // populate the cache

        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "30");

        Assert.Equal(2, nodeset.NodeIdArray.Length);
        Assert.Contains(1u, nodeset.NodeIdArray);
        Assert.Contains(2u, nodeset.NodeIdArray);
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
        var result = nodeset.DefineNodeAttribute("age", "text");
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

    // ── RenameNodeAttribute ──────────────────────────────────────────────────

    [Fact]
    public void RenameNodeAttribute_NewName_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.RenameNodeAttribute("age", "years");
        Assert.True(result.Success);
    }

    [Fact]
    public void RenameNodeAttribute_NewName_OldNameNoLongerLooksUpValue()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "30");
        nodeset.RenameNodeAttribute("age", "years");

        Assert.False(nodeset.GetNodeAttribute(1, "age").Success);
    }

    [Fact]
    public void RenameNodeAttribute_NewName_ValuePreservedUnderNewName()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "30");
        nodeset.RenameNodeAttribute("age", "years");

        var result = nodeset.GetNodeAttribute(1, "years");
        Assert.True(result.Success);
        Assert.Equal(30, result.Value.Value.GetValue(result.Value.Type));
    }

    [Fact]
    public void RenameNodeAttribute_NonExistentAttribute_Fails()
    {
        var nodeset = new Nodeset("ns");
        var result = nodeset.RenameNodeAttribute("ghost", "whatever");
        Assert.False(result.Success);
    }

    [Fact]
    public void RenameNodeAttribute_NameAlreadyExists_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.DefineNodeAttribute("years", "int");
        var result = nodeset.RenameNodeAttribute("age", "years");
        Assert.False(result.Success);
    }

    [Fact]
    public void RenameNodeAttribute_SameName_SucceedsAsNoOp()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.RenameNodeAttribute("age", "age");
        Assert.True(result.Success);
    }

    [Fact]
    public void RenameNodeAttribute_EmptyNewName_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.RenameNodeAttribute("age", "");
        Assert.False(result.Success);
    }

    [Fact]
    public void RenameNodeAttribute_AfterRename_CanRedefineOldName()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.RenameNodeAttribute("age", "years");
        var result = nodeset.DefineNodeAttribute("age", "string");
        Assert.True(result.Success);
    }

    // ── ConvertNodeAttributeType ──────────────────────────────────────────────

    [Fact]
    public void ConvertNodeAttributeType_StringToInt_ValidValueConverts()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "string");
        nodeset.SetNodeAttribute(1, "age", "30");

        var result = nodeset.ConvertNodeAttributeType("age", "age_int", "int");

        Assert.True(result.Success, result.Message);
        var r = nodeset.GetNodeAttribute(1, "age_int");
        Assert.True(r.Success);
        Assert.Equal(30, (int)r.Value.Value.GetValue(r.Value.Type)!);
    }

    [Fact]
    public void ConvertNodeAttributeType_StringToInt_UnparseableValueIsSkippedNotFailed()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.DefineNodeAttribute("age", "string");
        nodeset.SetNodeAttribute(1, "age", "30");
        nodeset.SetNodeAttribute(2, "age", "NA");

        var result = nodeset.ConvertNodeAttributeType("age", "age_int", "int");

        Assert.True(result.Success, result.Message);
        Assert.True(nodeset.GetNodeAttribute(1, "age_int").Success);
        Assert.False(nodeset.GetNodeAttribute(2, "age_int").Success);
    }

    [Fact]
    public void ConvertNodeAttributeType_StringToFloat_NaNConvertsAsRealValue()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("score", "string");
        nodeset.SetNodeAttribute(1, "score", "NaN");

        var result = nodeset.ConvertNodeAttributeType("score", "score_float", "float");

        Assert.True(result.Success, result.Message);
        var r = nodeset.GetNodeAttribute(1, "score_float");
        Assert.True(r.Success);
        Assert.True(float.IsNaN((float)r.Value.Value.GetValue(r.Value.Type)!));
    }

    [Fact]
    public void ConvertNodeAttributeType_StringToBool_MismatchedStringIsSkipped()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.DefineNodeAttribute("active", "string");
        nodeset.SetNodeAttribute(1, "active", "true");
        nodeset.SetNodeAttribute(2, "active", "yes"); // not "true"/"false"

        var result = nodeset.ConvertNodeAttributeType("active", "active_bool", "bool");

        Assert.True(result.Success, result.Message);
        var r1 = nodeset.GetNodeAttribute(1, "active_bool");
        Assert.True(r1.Success);
        Assert.True((bool)r1.Value.Value.GetValue(r1.Value.Type)!);
        Assert.False(nodeset.GetNodeAttribute(2, "active_bool").Success);
    }

    [Fact]
    public void ConvertNodeAttributeType_StringToChar_MultiCharStringIsSkipped()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.DefineNodeAttribute("grade", "string");
        nodeset.SetNodeAttribute(1, "grade", "A");
        nodeset.SetNodeAttribute(2, "grade", "AB");

        var result = nodeset.ConvertNodeAttributeType("grade", "grade_char", "char");

        Assert.True(result.Success, result.Message);
        var r1 = nodeset.GetNodeAttribute(1, "grade_char");
        Assert.True(r1.Success);
        Assert.Equal('A', (char)r1.Value.Value.GetValue(r1.Value.Type)!);
        Assert.False(nodeset.GetNodeAttribute(2, "grade_char").Success);
    }

    [Fact]
    public void ConvertNodeAttributeType_IntToString_AlwaysSucceeds()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "30");

        var result = nodeset.ConvertNodeAttributeType("age", "age_str", "string");

        Assert.True(result.Success, result.Message);
        Assert.Equal("30", nodeset.GetNodeAttributeString(1, "age_str").Value);
    }

    [Fact]
    public void ConvertNodeAttributeType_NodeWithNoSourceValue_GetsNoTargetValueEither()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.DefineNodeAttribute("age", "string");
        nodeset.SetNodeAttribute(1, "age", "30");
        // node 2 has no value for "age" at all

        var result = nodeset.ConvertNodeAttributeType("age", "age_int", "int");

        Assert.True(result.Success, result.Message);
        Assert.False(nodeset.GetNodeAttribute(2, "age_int").Success);
    }

    [Fact]
    public void ConvertNodeAttributeType_OriginalAttributeUnaffected()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "string");
        nodeset.SetNodeAttribute(1, "age", "30");

        nodeset.ConvertNodeAttributeType("age", "age_int", "int");

        Assert.Equal("30", nodeset.GetNodeAttributeString(1, "age").Value);
    }

    [Fact]
    public void ConvertNodeAttributeType_NonExistentSourceAttribute_Fails()
    {
        var nodeset = new Nodeset("ns");
        var result = nodeset.ConvertNodeAttributeType("ghost", "whatever", "int");
        Assert.False(result.Success);
    }

    [Fact]
    public void ConvertNodeAttributeType_TargetNameAlreadyExists_FailsAndCreatesNothing()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "string");
        nodeset.DefineNodeAttribute("age_int", "int");

        var result = nodeset.ConvertNodeAttributeType("age", "age_int", "int");

        Assert.False(result.Success);
    }

    [Fact]
    public void ConvertNodeAttributeType_UnrecognizedTargetType_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "string");

        var result = nodeset.ConvertNodeAttributeType("age", "age_int", "notatype");

        Assert.False(result.Success);
        Assert.False(nodeset.GetDefinedAttributeNames().Contains("age_int"));
    }

    // ── Merge ───────────────────────────────────────────────────────────────

    [Fact]
    public void Merge_NodeIds_UnionOfBoth()
    {
        var ns1 = new Nodeset("ns1");
        ns1.AddNode(1);
        ns1.AddNode(2);
        var ns2 = new Nodeset("ns2");
        ns2.AddNode(2);
        ns2.AddNode(3);

        var result = Nodeset.Merge(ns1, ns2, "merged");
        Assert.True(result.Success);
        Assert.Equal(3, result.Value!.Count);
        Assert.Contains(1u, result.Value!.NodeIdArray);
        Assert.Contains(2u, result.Value!.NodeIdArray);
        Assert.Contains(3u, result.Value!.NodeIdArray);
    }

    [Fact]
    public void Merge_DisjointAttributes_BothPresentInResult()
    {
        var ns1 = new Nodeset("ns1");
        ns1.AddNode(1);
        ns1.DefineNodeAttribute("gender", "string");
        ns1.SetNodeAttribute(1, "gender", "F");
        var ns2 = new Nodeset("ns2");
        ns2.AddNode(1);
        ns2.DefineNodeAttribute("income2024", "int");
        ns2.SetNodeAttribute(1, "income2024", "500");

        var result = Nodeset.Merge(ns1, ns2, "merged");
        Assert.True(result.Success);
        Assert.Equal("F", result.Value!.GetNodeAttributeString(1, "gender").Value);
        Assert.Equal(500, result.Value!.GetNodeAttribute(1, "income2024").Value.Value.GetValue(NodeAttributeType.Int));
    }

    [Fact]
    public void Merge_AttributeTypeMismatch_Fails()
    {
        var ns1 = new Nodeset("ns1");
        ns1.DefineNodeAttribute("age", "int");
        var ns2 = new Nodeset("ns2");
        ns2.DefineNodeAttribute("age", "string");

        var result = Nodeset.Merge(ns1, ns2, "merged");
        Assert.False(result.Success);
    }

    [Fact]
    public void Merge_ValueOnlyInOneSource_NoConflict()
    {
        var ns1 = new Nodeset("ns1");
        ns1.AddNode(1);
        ns1.DefineNodeAttribute("gender", "string");
        ns1.SetNodeAttribute(1, "gender", "F");
        var ns2 = new Nodeset("ns2");
        ns2.AddNode(1);
        ns2.DefineNodeAttribute("gender", "string");
        // node 1 in ns2 has no value set for gender

        var result = Nodeset.Merge(ns1, ns2, "merged");
        Assert.True(result.Success);
        Assert.Equal("F", result.Value!.GetNodeAttributeString(1, "gender").Value);
    }

    [Fact]
    public void Merge_BothSourcesAgree_NoConflict()
    {
        var ns1 = new Nodeset("ns1");
        ns1.AddNode(1);
        ns1.DefineNodeAttribute("birthyear", "int");
        ns1.SetNodeAttribute(1, "birthyear", "1990");
        var ns2 = new Nodeset("ns2");
        ns2.AddNode(1);
        ns2.DefineNodeAttribute("birthyear", "int");
        ns2.SetNodeAttribute(1, "birthyear", "1990");

        var result = Nodeset.Merge(ns1, ns2, "merged");
        Assert.True(result.Success);
        Assert.Equal(1990, result.Value!.GetNodeAttribute(1, "birthyear").Value.Value.GetValue(NodeAttributeType.Int));
    }

    [Fact]
    public void Merge_ConflictingValues_DefaultFails()
    {
        var ns1 = new Nodeset("ns1");
        ns1.AddNode(1);
        ns1.DefineNodeAttribute("birthyear", "int");
        ns1.SetNodeAttribute(1, "birthyear", "1990");
        var ns2 = new Nodeset("ns2");
        ns2.AddNode(1);
        ns2.DefineNodeAttribute("birthyear", "int");
        ns2.SetNodeAttribute(1, "birthyear", "1991");

        var result = Nodeset.Merge(ns1, ns2, "merged");
        Assert.False(result.Success);
    }

    [Fact]
    public void Merge_ConflictingValues_KeepFirstUsesFirstSource()
    {
        var ns1 = new Nodeset("ns1");
        ns1.AddNode(1);
        ns1.DefineNodeAttribute("birthyear", "int");
        ns1.SetNodeAttribute(1, "birthyear", "1990");
        var ns2 = new Nodeset("ns2");
        ns2.AddNode(1);
        ns2.DefineNodeAttribute("birthyear", "int");
        ns2.SetNodeAttribute(1, "birthyear", "1991");

        var result = Nodeset.Merge(ns1, ns2, "merged", AttributeConflictPolicy.KeepFirst);
        Assert.True(result.Success);
        Assert.Equal(1990, result.Value!.GetNodeAttribute(1, "birthyear").Value.Value.GetValue(NodeAttributeType.Int));
    }

    [Fact]
    public void Merge_ConflictingValues_KeepLastUsesSecondSource()
    {
        var ns1 = new Nodeset("ns1");
        ns1.AddNode(1);
        ns1.DefineNodeAttribute("birthyear", "int");
        ns1.SetNodeAttribute(1, "birthyear", "1990");
        var ns2 = new Nodeset("ns2");
        ns2.AddNode(1);
        ns2.DefineNodeAttribute("birthyear", "int");
        ns2.SetNodeAttribute(1, "birthyear", "1991");

        var result = Nodeset.Merge(ns1, ns2, "merged", AttributeConflictPolicy.KeepLast);
        Assert.True(result.Success);
        Assert.Equal(1991, result.Value!.GetNodeAttribute(1, "birthyear").Value.Value.GetValue(NodeAttributeType.Int));
    }

    [Fact]
    public void Merge_ConflictingStringValues_KeepLastUsesSecondSourcesString()
    {
        var ns1 = new Nodeset("ns1");
        ns1.AddNode(1);
        ns1.DefineNodeAttribute("city", "string");
        ns1.SetNodeAttribute(1, "city", "Malmo");
        var ns2 = new Nodeset("ns2");
        ns2.AddNode(1);
        ns2.DefineNodeAttribute("city", "string");
        ns2.SetNodeAttribute(1, "city", "Lund");

        var result = Nodeset.Merge(ns1, ns2, "merged", AttributeConflictPolicy.KeepLast);
        Assert.True(result.Success);
        Assert.Equal("Lund", result.Value!.GetNodeAttributeString(1, "city").Value);
    }

    [Fact]
    public void Merge_ResultHasGivenName()
    {
        var ns1 = new Nodeset("ns1");
        var ns2 = new Nodeset("ns2");
        var result = Nodeset.Merge(ns1, ns2, "merged");
        Assert.Equal("merged", result.Value!.Name);
    }

    [Fact]
    public void Merge_FloatZeroAndNegativeZero_AreNotAConflict()
    {
        // 0.0 and -0.0 have different bit patterns but are the same value: must not be flagged
        // as a value conflict even under the default onvalueconflict='fail' policy.
        var ns1 = new Nodeset("ns1");
        ns1.AddNode(1);
        ns1.DefineNodeAttribute("score", "float");
        ns1.SetNodeAttribute(1, "score", "0.0");
        var ns2 = new Nodeset("ns2");
        ns2.AddNode(1);
        ns2.DefineNodeAttribute("score", "float");
        ns2.SetNodeAttribute(1, "score", "-0.0");

        var result = Nodeset.Merge(ns1, ns2, "merged");
        Assert.True(result.Success);
    }

    // ── DuplicateNodeAttribute ────────────────────────────────────────────────

    [Fact]
    public void DuplicateNodeAttribute_NewName_Succeeds()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.DuplicateNodeAttribute("age", "years");
        Assert.True(result.Success);
    }

    [Fact]
    public void DuplicateNodeAttribute_OriginalAndCopyBothExist()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "30");
        nodeset.DuplicateNodeAttribute("age", "years");

        Assert.True(nodeset.GetNodeAttribute(1, "age").Success);
        var result = nodeset.GetNodeAttribute(1, "years");
        Assert.True(result.Success);
        Assert.Equal(30, result.Value.Value.GetValue(result.Value.Type));
    }

    [Fact]
    public void DuplicateNodeAttribute_NodeWithoutValueSet_HasNoValueUnderNewNameEither()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "30");
        nodeset.DuplicateNodeAttribute("age", "years");

        Assert.False(nodeset.GetNodeAttribute(2, "years").Success);
    }

    [Fact]
    public void DuplicateNodeAttribute_EditingCopyDoesNotAffectOriginal()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "30");
        nodeset.DuplicateNodeAttribute("age", "years");
        nodeset.SetNodeAttribute(1, "years", "99");

        var original = nodeset.GetNodeAttribute(1, "age");
        Assert.Equal(30, original.Value.Value.GetValue(original.Value.Type));
    }

    [Fact]
    public void DuplicateNodeAttribute_NonExistentAttribute_Fails()
    {
        var nodeset = new Nodeset("ns");
        var result = nodeset.DuplicateNodeAttribute("ghost", "whatever");
        Assert.False(result.Success);
    }

    [Fact]
    public void DuplicateNodeAttribute_NameAlreadyExists_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.DefineNodeAttribute("years", "int");
        var result = nodeset.DuplicateNodeAttribute("age", "years");
        Assert.False(result.Success);
    }

    [Fact]
    public void DuplicateNodeAttribute_SameNameAsOriginal_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.DuplicateNodeAttribute("age", "age");
        Assert.False(result.Success);
    }

    [Fact]
    public void DuplicateNodeAttribute_EmptyNewName_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.DefineNodeAttribute("age", "int");
        var result = nodeset.DuplicateNodeAttribute("age", "");
        Assert.False(result.Success);
    }

    // ── GetNodeAttributesTable ────────────────────────────────────────────────

    [Fact]
    public void GetNodeAttributesTable_SpecificAttributes_ReturnsOneColumnPerAttribute()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.AddNode(2);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.DefineNodeAttribute("city", "string");
        nodeset.SetNodeAttribute(1, "age", "30");
        nodeset.SetNodeAttribute(1, "city", "Malmo");
        nodeset.SetNodeAttribute(2, "age", "40");

        var result = nodeset.GetNodeAttributesTable([1, 2], ["age", "city"]);
        Assert.True(result.Success);
        Assert.Equal(30, result.Value!["age"][0]);
        Assert.Equal(40, result.Value!["age"][1]);
        Assert.Equal("Malmo", result.Value!["city"][0]);
        Assert.Null(result.Value!["city"][1]);
    }

    [Fact]
    public void GetNodeAttributesTable_NoAttrNames_IncludesAllDefinedAttributes()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.DefineNodeAttribute("city", "string");
        nodeset.SetNodeAttribute(1, "age", "30");

        var result = nodeset.GetNodeAttributesTable([1], null);
        Assert.True(result.Success);
        Assert.True(result.Value!.ContainsKey("age"));
        Assert.True(result.Value!.ContainsKey("city"));
    }

    [Fact]
    public void GetNodeAttributesTable_NonExistentNode_ReturnsNullsNotFailure()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        nodeset.DefineNodeAttribute("age", "int");
        nodeset.SetNodeAttribute(1, "age", "30");

        var result = nodeset.GetNodeAttributesTable([1, 999], ["age"]);
        Assert.True(result.Success);
        Assert.Equal(30, result.Value!["age"][0]);
        Assert.Null(result.Value!["age"][1]);
    }

    [Fact]
    public void GetNodeAttributesTable_UnknownAttribute_Fails()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(1);
        var result = nodeset.GetNodeAttributesTable([1], ["ghost"]);
        Assert.False(result.Success);
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

    // ── Constructor edge cases ───────────────────────────────────────────────

    [Fact]
    public void Constructor_WithCountZero_HasZeroNodes()
    {
        var nodeset = new Nodeset("ns", 0);
        Assert.Equal(0, nodeset.Count);
        Assert.Empty(nodeset.NodeIdArray);
    }

    [Fact]
    public void Constructor_WithCountOne_HasSingleNode()
    {
        var nodeset = new Nodeset("ns", 1);
        Assert.Equal(1, nodeset.Count);
        Assert.Single(nodeset.NodeIdArray);
    }

    // ── GetNodeIdByIndex edge cases ──────────────────────────────────────────

    [Fact]
    public void GetNodeIdByIndex_EmptyNodeset_ReturnsNull()
    {
        var nodeset = new Nodeset("ns", 0);
        Assert.Null(nodeset.GetNodeIdByIndex(0));
    }

    [Fact]
    public void GetNodeIdByIndex_SingleNode_IndexZeroReturnsId()
    {
        var nodeset = new Nodeset("ns");
        nodeset.AddNode(42);
        uint? id = nodeset.GetNodeIdByIndex(0);
        Assert.Equal(42u, id);
    }

    // ── NodeAttributeDefinitionManager.Clone correctness ────────────────────
    // These tests exercise the Clone() path through NodesetProcessor.Filter(),
    // which assigns NodeAttributeDefinitionManager = sourceNodeset.NodeAttributeDefinitionManager.Clone()

    [Fact]
    public void Clone_PreservesExistingAttributes_NewAttributeGetsDistinctIndex()
    {
        // Build a nodeset with two attributes so _nextIndex is at least 2
        var source = new Nodeset("src");
        source.AddNode(1);
        source.AddNode(2);
        source.DefineNodeAttribute("age", "int");
        source.DefineNodeAttribute("score", "float");
        source.SetNodeAttribute(1, "age", "10");
        source.SetNodeAttribute(2, "age", "20");
        source.SetNodeAttribute(1, "score", "1.5");
        source.SetNodeAttribute(2, "score", "2.5");

        // Filter produces a clone of the NodeAttributeDefinitionManager
        var filterResult = NodesetProcessor.Filter(source, "age", ConditionType.ge, "15");
        Assert.True(filterResult.Success);
        var clone = filterResult.Value!;

        // The existing attributes should work on the clone
        Assert.True(clone.DefineNodeAttribute("weight", "float").Success);

        // Most critically: "weight" must not collide with "age" (index 0) or "score" (index 1).
        // If _nextIndex was not copied, "weight" would receive index 0 — same as "age" — breaking reads.
        // We verify by defining then setting and reading back all three attributes on a new node.
        clone.AddNode(99);
        Assert.True(clone.SetNodeAttribute(99, "age", "25").Success);
        Assert.True(clone.SetNodeAttribute(99, "score", "3.0").Success);
        Assert.True(clone.SetNodeAttribute(99, "weight", "70").Success);

        var ageResult = clone.GetNodeAttribute(99, "age");
        var scoreResult = clone.GetNodeAttribute(99, "score");
        var weightResult = clone.GetNodeAttribute(99, "weight");
        Assert.True(ageResult.Success);
        Assert.True(scoreResult.Success);
        Assert.True(weightResult.Success);

        Assert.Equal(25, (int)ageResult.Value.Value.GetValue(ageResult.Value.Type)!);
        Assert.Equal(3.0f, (float)scoreResult.Value.Value.GetValue(scoreResult.Value.Type)!, 4);
        Assert.Equal(70.0f, (float)weightResult.Value.Value.GetValue(weightResult.Value.Type)!, 4);
    }

    [Fact]
    public void Clone_WithRecycledIndex_NewAttributeReusesThatIndex_WithoutCollision()
    {
        // Define two attributes then undefine the first: its index goes onto the recycle stack.
        var source = new Nodeset("src");
        source.AddNode(1);
        source.DefineNodeAttribute("old", "int");
        source.DefineNodeAttribute("keep", "bool");
        source.SetNodeAttribute(1, "keep", "true");
        source.UndefineNodeAttribute("old");   // index 0 is now recycled

        // Produce a clone via Filter (isnull on "keep" matches nodes that lack the attribute — none here, but that's fine)
        var filterResult = NodesetProcessor.Filter(source, "keep", ConditionType.notnull);
        Assert.True(filterResult.Success);
        var clone = filterResult.Value!;

        // Define a new attribute on the clone — should reuse the recycled index (0) without issue
        Assert.True(clone.DefineNodeAttribute("fresh", "char").Success);

        // Verify "keep" and "fresh" are independently readable
        clone.AddNode(2);
        clone.SetNodeAttribute(2, "keep", "false");
        clone.SetNodeAttribute(2, "fresh", "X");

        var keepResult = clone.GetNodeAttribute(2, "keep");
        var freshResult = clone.GetNodeAttribute(2, "fresh");
        Assert.True(keepResult.Success);
        Assert.True(freshResult.Success);
        Assert.Equal(false, (bool)keepResult.Value.Value.GetValue(keepResult.Value.Type)!);
        Assert.Equal('X', (char)freshResult.Value.Value.GetValue(freshResult.Value.Type)!);
    }

    [Fact]
    public void Clone_CanDefineSecondAttributeAfterFirstOnClone()
    {
        // Minimal case: one attribute defined, filter, then add another attribute to the clone.
        // _nextIndex must be 1 (not 0) on the clone for the second define to get a fresh index.
        var source = new Nodeset("src");
        source.AddNode(1);
        source.DefineNodeAttribute("x", "int");
        // node 1 has no value for "x" → isnull matches it
        var filterResult = NodesetProcessor.Filter(source, "x", ConditionType.isnull);
        Assert.True(filterResult.Success);
        var clone = filterResult.Value!;

        // "y" must receive index 1, not 0 (which is already taken by "x")
        Assert.True(clone.DefineNodeAttribute("y", "float").Success);

        clone.SetNodeAttribute(1, "x", "5");
        clone.SetNodeAttribute(1, "y", "1.5");

        var xAttr = clone.GetNodeAttribute(1, "x");
        var yAttr = clone.GetNodeAttribute(1, "y");
        Assert.True(xAttr.Success);
        Assert.True(yAttr.Success);
        Assert.Equal(5, (int)xAttr.Value.Value.GetValue(xAttr.Value.Type)!);
        Assert.Equal(1.5f, (float)yAttr.Value.Value.GetValue(yAttr.Value.Type)!, 4);
    }

    // ── String node attributes (in-memory) ──────────────────────────────────────

    [Fact]
    public void StringAttr_Define_Succeeds()
    {
        var ns = new Nodeset("ns", 3);
        var result = ns.DefineNodeAttribute("occupation", "string");
        Assert.True(result.Success);
    }

    [Fact]
    public void StringAttr_SetAndGet_ReturnsCorrectValue()
    {
        var ns = new Nodeset("ns", 3);
        ns.DefineNodeAttribute("occupation", "string");
        ns.SetNodeAttribute(1u, "occupation", "doctor");
        var result = ns.GetNodeAttributeString(1u, "occupation");
        Assert.True(result.Success);
        Assert.Equal("doctor", result.Value);
    }

    [Fact]
    public void StringAttr_PoolDeduplication_SameStringSharesIndex()
    {
        var ns = new Nodeset("ns", 4);
        ns.DefineNodeAttribute("occupation", "string");
        ns.SetNodeAttribute(1u, "occupation", "doctor");
        ns.SetNodeAttribute(2u, "occupation", "doctor");
        ns.SetNodeAttribute(3u, "occupation", "engineer");
        // Pool should contain exactly 2 entries
        Assert.Equal(2, ns.StringPool.Count);
    }

    [Fact]
    public void StringAttr_MultipleValues_AllResolveCorrectly()
    {
        var ns = new Nodeset("ns", 4);
        ns.DefineNodeAttribute("occupation", "string");
        ns.SetNodeAttribute(1u, "occupation", "doctor");
        ns.SetNodeAttribute(2u, "occupation", "engineer");
        ns.SetNodeAttribute(3u, "occupation", "doctor");
        Assert.Equal("doctor", ns.GetNodeAttributeString(1u, "occupation").Value);
        Assert.Equal("engineer", ns.GetNodeAttributeString(2u, "occupation").Value);
        Assert.Equal("doctor", ns.GetNodeAttributeString(3u, "occupation").Value);
    }

    [Fact]
    public void StringAttr_Reassign_ReturnsNewValue()
    {
        var ns = new Nodeset("ns", 1);
        ns.AddNode(1u);
        ns.DefineNodeAttribute("occupation", "string");
        ns.SetNodeAttribute(1u, "occupation", "engineer");
        ns.SetNodeAttribute(1u, "occupation", "doctor");
        Assert.Equal("doctor", ns.GetNodeAttributeString(1u, "occupation").Value);
    }

    [Fact]
    public void StringAttr_GetNodeAttributeString_WrongType_Fails()
    {
        var ns = new Nodeset("ns", 1);
        ns.AddNode(1u);
        ns.DefineNodeAttribute("age", "int");
        ns.SetNodeAttribute(1u, "age", "42");
        var result = ns.GetNodeAttributeString(1u, "age");
        Assert.False(result.Success);
    }

    [Fact]
    public void StringAttr_GetNodeAttributeString_MissingAttr_Fails()
    {
        var ns = new Nodeset("ns", 1);
        ns.AddNode(1u);
        ns.DefineNodeAttribute("occupation", "string");
        // Attribute defined but not set on node 1
        var result = ns.GetNodeAttributeString(1u, "occupation");
        Assert.False(result.Success);
    }

    [Fact]
    public void StringAttr_GetMultipleNodeAttributes_ReturnsStrings()
    {
        var ns = new Nodeset("ns", 4);
        ns.DefineNodeAttribute("country", "string");
        ns.SetNodeAttribute(1u, "country", "Sweden");
        ns.SetNodeAttribute(2u, "country", "Norway");
        ns.SetNodeAttribute(3u, "country", "Sweden");
        var result = ns.GetMultipleNodeAttributes([1u, 2u, 3u], "country");
        Assert.True(result.Success);
        Assert.Equal("Sweden", result.Value![1u]);
        Assert.Equal("Norway", result.Value![2u]);
        Assert.Equal("Sweden", result.Value![3u]);
    }

    [Fact]
    public void StringAttr_MixedWithOtherTypes_BothWork()
    {
        var ns = new Nodeset("ns", 2);
        ns.DefineNodeAttribute("occupation", "string");
        ns.DefineNodeAttribute("age", "int");
        ns.SetNodeAttribute(1u, "occupation", "doctor");
        ns.SetNodeAttribute(1u, "age", "45");
        Assert.Equal("doctor", ns.GetNodeAttributeString(1u, "occupation").Value);
        var ageResult = ns.GetNodeAttribute(1u, "age");
        Assert.Equal(45, (int)ageResult.Value.Value.GetValue(NodeAttributeType.Int)!);
    }

    [Fact]
    public void StringAttr_Undefine_RemovesFromAllNodes()
    {
        var ns = new Nodeset("ns", 2);
        ns.DefineNodeAttribute("occupation", "string");
        ns.SetNodeAttribute(1u, "occupation", "doctor");
        ns.SetNodeAttribute(2u, "occupation", "engineer");
        ns.UndefineNodeAttribute("occupation");
        Assert.False(ns.GetNodeAttributeString(1u, "occupation").Success);
        Assert.False(ns.GetNodeAttributeString(2u, "occupation").Success);
    }
}