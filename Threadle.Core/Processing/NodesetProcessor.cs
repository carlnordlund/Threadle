using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Processing
{
    /// <summary>
    /// Various methods for processing Nodeset structures
    /// </summary>
    public static class NodesetProcessor
    {
        #region Methods (public)
        /// <summary>
        /// Filters the nodes in the specified <see cref="Nodeset"/> based on the value of a given attribute and a
        /// specified condition.
        /// </summary>
        /// <remarks>The method evaluates each node in the <paramref name="sourceNodeset"/> against the
        /// specified attribute and condition.  Nodes that satisfy the condition are included in the resulting filtered
        /// <see cref="Nodeset"/>.  If the attribute specified by <paramref name="attrName"/> does not exist in the
        /// <paramref name="sourceNodeset"/>,  the method returns an error result with the code "AttributeUnknown".  If
        /// <paramref name="attrValueStr"/> is not provided (null or empty) and the condition is not <see
        /// cref="ConditionType.notnull"/>  or <see cref="ConditionType.isnull"/>, the method returns an error result
        /// with the code "AttributeValueNotFound".</remarks>
        /// <param name="sourceNodeset">The source <see cref="Nodeset"/> to filter.</param>
        /// <param name="attrName">The name of the attribute to evaluate for filtering.</param>
        /// <param name="condition">The condition to apply when evaluating the attribute value.</param>
        /// <param name="attrValueStr">The value to compare against the attribute value, if applicable. This parameter is optional and defaults to
        /// an empty string.</param>
        /// <returns>An <see cref="OperationResult{T}"/> containing the filtered <see cref="Nodeset"/> if the operation succeeds;
        /// otherwise, an error result indicating the failure reason.</returns>
        public static OperationResult<Nodeset> Filter(Nodeset sourceNodeset, string attrName, ConditionType condition, string attrValueStr = "")
        {
            if (ValidateCondition(sourceNodeset, attrName, condition, attrValueStr) is OperationResult { Success: false } validation)
                return OperationResult<Nodeset>.Fail(validation);
            Nodeset filtered = new Nodeset(sourceNodeset.Name + "_clone") { NodeAttributeDefinitionManager = sourceNodeset.NodeAttributeDefinitionManager.Clone() };
            foreach (string s in sourceNodeset.StringPool)
                filtered.GetOrAddStringToPool(s);

            foreach (uint nodeId in sourceNodeset.NodeIdArray)
            {
                if (MatchesCondition(sourceNodeset, nodeId, attrName, condition, attrValueStr))
                    filtered._addNodeWithAttributes(nodeId, sourceNodeset.CloneNodeAttributeTuple(nodeId));
            }
            return OperationResult<Nodeset>.Ok(filtered);
        }

        /// <summary>
        /// Checks that a condition on a node attribute (as used by <see cref="Filter"/>) can be evaluated: the
        /// attribute must exist and have a type, and a reference value must be given unless the condition is
        /// <see cref="ConditionType.isnull"/> or <see cref="ConditionType.notnull"/>.
        /// </summary>
        /// <param name="nodeset">The <see cref="Nodeset"/> holding the attribute.</param>
        /// <param name="attrName">The name of the attribute.</param>
        /// <param name="condition">The condition to apply.</param>
        /// <param name="attrValueStr">The reference value, if applicable.</param>
        /// <returns>An <see cref="OperationResult"/> that fails with the same codes as <see cref="Filter"/>.</returns>
        public static OperationResult ValidateCondition(Nodeset nodeset, string attrName, ConditionType condition, string? attrValueStr)
        {
            if (!nodeset.NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out var attrIndex))
                return OperationResult.Fail("AttributeUnknown", $"Attribute '{attrName}' not found in nodeset '{nodeset.Name}'.");
            if ((attrValueStr == null || attrValueStr.Length == 0) && condition != ConditionType.notnull && condition != ConditionType.isnull)
                return OperationResult.Fail("MissingAttributeValue", $"Attribute value must be set for '{condition}' condition.");
            if (!nodeset.NodeAttributeDefinitionManager.TryGetAttributeType(attrIndex, out _))
                return OperationResult.Fail("AttributeTypeNotFound", $"No type found for attribute '{attrName}' in nodeset '{nodeset.Name}': possibly corrupted.");
            return OperationResult.Ok();
        }

        /// <summary>
        /// Evaluates whether a node fulfills a condition on one of its attributes, with the semantics of
        /// <see cref="Filter"/>: a missing attribute counts as null, thus fulfilling 'ne' and 'isnull' only.
        /// Assumes that the condition has been checked with <see cref="ValidateCondition"/>.
        /// </summary>
        /// <param name="nodeset">The <see cref="Nodeset"/> holding the node and its attributes.</param>
        /// <param name="nodeId">The node id.</param>
        /// <param name="attrName">The name of the attribute.</param>
        /// <param name="condition">The condition to apply.</param>
        /// <param name="attrValueStr">The reference value, if applicable.</param>
        /// <returns>True if the node fulfills the condition, otherwise false.</returns>
        public static bool MatchesCondition(Nodeset nodeset, uint nodeId, string attrName, ConditionType condition, string attrValueStr)
        {
            var result = nodeset.GetNodeAttribute(nodeId, attrName);
            return result.Success switch
            {
                true => condition switch
                {
                    ConditionType.notnull => true,  // Existing attribute counts as 'notnull'
                    _ => result.Value.Type == NodeAttributeType.String
                    ? Misc.EvaluateConditionString(nodeset.GetStringFromPool((int)result.Value.Value.GetValue(NodeAttributeType.String)!), attrValueStr, condition)
                    : Misc.EvaluateCondition(result.Value.Value, result.Value.Type, attrValueStr, condition),
                },
                false => condition switch
                {
                    ConditionType.ne => true,       // Missing attribute counts as "not equal"
                    ConditionType.isnull => true,   // Missing attribute counts as null
                    ConditionType.notnull => false, // Missing attribute is not not null
                    _ => false                      // For any other condition type, this is not a match
                }
            };
        }

        /// <summary>
        /// Imports node attributes from structured attribute data into an existing nodeset.
        /// For each attribute, the Values dictionary maps node id to its string-encoded value.
        /// Node ids not present in the nodeset are skipped (left-join semantics) unless
        /// <paramref name="addMissingNodes"/> is true, in which case they are added.
        /// File I/O and format parsing are handled by the caller (e.g. FileManager).
        /// </summary>
        public static OperationResult ImportNodeAttributes(
            Nodeset nodeset,
            IReadOnlyList<(string Name, NodeAttributeType Type, Dictionary<uint, string> Values)> attributes,
            bool addMissingNodes = false)
        {
            if (attributes.Count == 0)
                return OperationResult.Fail("NoAttributes", "No attributes provided.");

            HashSet<uint> allNodeIds = [];
            foreach (var attr in attributes)
                foreach (uint id in attr.Values.Keys)
                    allNodeIds.Add(id);

            int rowsImported = 0, rowsSkipped = 0;
            HashSet<uint>? ineligible = null;

            foreach (uint nodeId in allNodeIds)
            {
                if (nodeset.Contains(nodeId))
                    rowsImported++;
                else if (addMissingNodes)
                {
                    nodeset.AddNode(nodeId);
                    rowsImported++;
                }
                else
                {
                    (ineligible ??= []).Add(nodeId);
                    rowsSkipped++;
                }
            }

            foreach (var (name, type, values) in attributes)
            {
                Dictionary<uint, string> toImport = ineligible == null
                    ? values
                    : values.Where(kvp => !ineligible.Contains(kvp.Key))
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                OperationResult result = nodeset.DefineAndSetNodeAttributeValues(name, toImport, type);
                if (!result.Success)
                    return result;
            }

            string msg = $"Imported {attributes.Count} attribute(s) for {rowsImported} nodes into nodeset '{nodeset.Name}'."
                + (rowsSkipped > 0 ? $" {rowsSkipped} rows skipped (node ids not found)." : string.Empty);
            return OperationResult.Ok(msg);
        }

        #endregion
    }
}
