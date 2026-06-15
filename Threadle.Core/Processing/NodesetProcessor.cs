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
            if (!sourceNodeset.NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out var attrIndex))
                return OperationResult<Nodeset>.Fail("AttributeUnknown", $"Attribute '{attrName}' not found in nodeset '{sourceNodeset.Name}'.");
            if ((attrValueStr == null || attrValueStr.Length == 0) && condition != ConditionType.notnull && condition != ConditionType.isnull)
                return OperationResult<Nodeset>.Fail("MissingAttributeValue", $"Attribute value must be set for '{condition}' condition.");
            if (!sourceNodeset.NodeAttributeDefinitionManager.TryGetAttributeType(attrIndex, out var attrType))
                return OperationResult<Nodeset>.Fail("AttributeTypeNotFound", $"No type found for attribute '{attrName}' in nodeset '{sourceNodeset.Name}': possibly corrupted.");
            Nodeset filtered = new Nodeset(sourceNodeset.Name + "_clone") { NodeAttributeDefinitionManager = sourceNodeset.NodeAttributeDefinitionManager.Clone() };
            foreach (string s in sourceNodeset.StringPool)
                filtered.GetOrAddStringToPool(s);

            foreach (uint nodeId in sourceNodeset.NodeIdArray)
            {
                var result = sourceNodeset.GetNodeAttribute(nodeId, attrName);
                bool matches = result.Success switch
                {
                    true => condition switch
                    {
                        ConditionType.notnull => true,  // Existing attribute counts as 'notnull'
                        _ => result.Value.Type == NodeAttributeType.String
                        ? Misc.EvaluateConditionString(sourceNodeset.GetStringFromPool((int)result.Value.Value.GetValue(NodeAttributeType.String)!), attrValueStr!, condition)
                        : Misc.EvaluateCondition(result.Value.Value, result.Value.Type, attrValueStr!, condition),
                    },
                    false => condition switch
                    {
                        ConditionType.ne => true,       // Missing attribute counts as "not equal"
                        ConditionType.isnull => true,   // Missing attribute counts as null
                        ConditionType.notnull => false, // Missing attribute is not not null
                        _ => false                      // For any other condition type, this is not a match
                    }
                };
                if (matches)
                    filtered._addNodeWithAttributes(nodeId, sourceNodeset.CloneNodeAttributeTuple(nodeId));
            }
            return OperationResult<Nodeset>.Ok(filtered);
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
