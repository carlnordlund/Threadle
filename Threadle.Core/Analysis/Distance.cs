using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Analysis
{
    public static class Distance
    {

        public static OperationResult<StructureResult> RandomWalkNodeAttributeDistances(Network network, string attrName, int maxSteps, string[]? layers, float walkfactor = 1.0f, bool balanced = false, bool weighted = false, bool backtrack = false, bool savesteps = false)
        {
            Nodeset nodeset = network.Nodeset;
            var nodeAttributeInfo = nodeset.NodeAttributeDefinitionManager.GetNodeAttributeDefinition(attrName);
            if (nodeAttributeInfo == null)
                return OperationResult<StructureResult>.Fail("AttributeUnknown", $"Attribute '{attrName}' not found in nodeset '{nodeset.Name}'.");

            NodeAttributeType attrType = nodeAttributeInfo.Value.AttrType;
            byte attrIndex = nodeAttributeInfo.Value.Index;

            if (attrType != NodeAttributeType.String && attrType != NodeAttributeType.Char && attrType != NodeAttributeType.Int)
                return OperationResult<StructureResult>.Fail("InvalidAttributeType", $"Attribute '{attrName}' is of type '{nodeAttributeInfo.Value.AttrType}': must be char, integer, or string.");

            // Build up collection of unique attribute values
            // Maybe externalize: could be useful elsewhere
            HashSet<string> uniqueAttrValues = [];
            bool hasMissingValues = false;
            foreach (uint nodeId in nodeset.NodeIdArray)
            {
                if (!(nodeset.GetNodeAttribute(nodeId, attrIndex) is NodeAttributeValue attributeValue))
                    hasMissingValues = true;
                else
                {
                    string label = attrType == NodeAttributeType.String
                        ? nodeset.GetStringFromPool((int)attributeValue.GetValue(attrType)!)
                        : attributeValue.ToString(attrType);
                    uniqueAttrValues.Add(label);
                }
            }
            if (hasMissingValues)
                uniqueAttrValues.Add("(missing)");

            // Create a string array with unique attribute values
            string[] labels = uniqueAttrValues.ToArray();

            // Create nodeset for result network and create as many nodes as there are unique node attribute values
            Nodeset nodesetResults = new Nodeset(attrName + "_values");
            // Define a 'label' attribute
            var defineResult = nodesetResults.DefineNodeAttribute("label", NodeAttributeType.String);
            byte labelIndex = defineResult.Value;

            for (uint i = 0; i < (uint)labels.Length; i++)
            {
                int poolIndex = nodesetResults.GetOrAddStringToPool(labels[i]);
                nodesetResults._addNodeWithAttributes(i, (new List<byte> { labelIndex }, new List<NodeAttributeValue> { new NodeAttributeValue(poolIndex) }));
            }
            // Continue later when I have implemented string node attributes!

            Network networkResults = new Network(attrName + "_rwdistances_results", nodesetResults);


            StructureResult results = new StructureResult(networkResults, new Dictionary<string, IStructure> { { "nodeset", nodesetResults } });

            return OperationResult<StructureResult>.Ok(results, "Random walker done!");



            //return OperationResult<StructureResult>.Fail("NotYetImplemented", $"RandomWalkNodeAttributeDistances not yet implemented");

        }
    }
}
