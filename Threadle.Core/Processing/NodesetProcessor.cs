using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Processing
{
    public static class NodesetProcessor
    {
        public static OperationResult<Nodeset> Filter(Nodeset sourceNodeset, string attrName, ConditionType condition, string attrValue = "")
        {
            if (!sourceNodeset.NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out var attrIndex))
                return OperationResult<Nodeset>.Fail("AttributeNotFound", $"Attribute '{attrName}' not found in nodeset '{sourceNodeset.Name}'.");
            if ((attrValue == null || attrValue.Length == 0) && condition != ConditionType.notnull && condition != ConditionType.isnull)
                return OperationResult<Nodeset>.Fail("AttributeValueNotFound", $"Attribute value must be set for '{condition}' condition.");

            Nodeset filtered = sourceNodeset.Clone(sourceNodeset.Name + "_filtered", false);
            foreach (uint nodeId in sourceNodeset.NodeIdArray)
            {
                var result = sourceNodeset.GetNodeAttribute(nodeId, attrName);
                bool matches = result.Success switch
                {
                    true => condition switch
                    {
                        ConditionType.notnull => true,  // Existing attribute counts as 'notnull'
                        _ => Misc.EvalutateCondition(result.Value, attrValue, condition), // Evaluate condition here
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
                    filtered.AddNode(nodeId, sourceNodeset.CloneNodeAttributeDictionary(nodeId));
            }
            return OperationResult<Nodeset>.Ok(filtered);
        }
    }
}
