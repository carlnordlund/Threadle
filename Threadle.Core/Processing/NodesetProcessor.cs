using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Threadle.Core.Model;
using Threadle.Core.Processing.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Processing
{
    /// <summary>
    /// Various methods for processing Nodesetstructures
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
        /// <paramref name="sourceNodeset"/>,  the method returns an error result with the code "AttributeNotFound".  If
        /// <paramref name="attrValue"/> is not provided (null or empty) and the condition is not <see
        /// cref="ConditionType.notnull"/>  or <see cref="ConditionType.isnull"/>, the method returns an error result
        /// with the code "AttributeValueNotFound".</remarks>
        /// <param name="sourceNodeset">The source <see cref="Nodeset"/> to filter.</param>
        /// <param name="attrName">The name of the attribute to evaluate for filtering.</param>
        /// <param name="condition">The condition to apply when evaluating the attribute value.</param>
        /// <param name="attrValue">The value to compare against the attribute value, if applicable. This parameter is optional and defaults to
        /// an empty string.</param>
        /// <returns>An <see cref="OperationResult{T}"/> containing the filtered <see cref="Nodeset"/> if the operation succeeds;
        /// otherwise, an error result indicating the failure reason.</returns>
        public static OperationResult<Nodeset> Filter(Nodeset sourceNodeset, string attrName, ConditionType condition, string attrValue = "")
        {
            if (!sourceNodeset.NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out var attrIndex))
                return OperationResult<Nodeset>.Fail("AttributeNotFound", $"Attribute '{attrName}' not found in nodeset '{sourceNodeset.Name}'.");
            if ((attrValue == null || attrValue.Length == 0) && condition != ConditionType.notnull && condition != ConditionType.isnull)
                return OperationResult<Nodeset>.Fail("AttributeValueNotFound", $"Attribute value must be set for '{condition}' condition.");
            //Nodeset filtered = sourceNodeset.Clone(sourceNodeset.Name + "_filtered");

            //Nodeset clone = new Nodeset(newName != null ? newName : Name + "_clone") { NodeAttributeDefinitionManager = NodeAttributeDefinitionManager.Clone() };

            Nodeset filtered = new Nodeset(sourceNodeset.Name + "_clone") { NodeAttributeDefinitionManager = sourceNodeset.NodeAttributeDefinitionManager.Clone() };

            foreach (uint nodeId in sourceNodeset.NodeIdArray)
            {
                var result = sourceNodeset.GetNodeAttribute(nodeId, attrName);
                bool matches = result.Success switch
                {
                    true => condition switch
                    {
                        ConditionType.notnull => true,  // Existing attribute counts as 'notnull'
                        _ => Misc.EvalutateCondition(result.Value, attrValue!, condition), // Evaluate condition here
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
        #endregion
    }
}
