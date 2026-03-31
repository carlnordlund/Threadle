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
            Dictionary<string, uint> nodeAttributeStringToNodeId = [];
            for (uint i = 0; i < (uint)labels.Length; i++)
            {
                int poolIndex = nodesetResults.GetOrAddStringToPool(labels[i]);
                nodesetResults._addNodeWithAttributes(i, (new List<byte> { labelIndex }, new List<NodeAttributeValue> { new NodeAttributeValue(poolIndex) }));
                nodeAttributeStringToNodeId[labels[i]] = i;
            }
            // Create network for results
            Network networkResults = new Network(attrName + "_rwdistances_results", nodesetResults);

            // Determine how many nodes should be included in each step level
            // This is related to walkFactor: if walkFactor=0.5, only every second should be included
            // if walkfactor = 3, each node should be included 3 times
            int nbrNodesPerStepLevel = (int)(nodeset.Count * walkfactor);

            // Iterate through all with given path lengths (i.e. s as number of steps
            for (int s = 1; s <= maxSteps; s++)
            {
                Dictionary<(uint, uint), int> resultsDict = [];
                for (int i = 0; i < nbrNodesPerStepLevel; i++)
                {
                    uint nodeIndex = (uint)Math.Floor(i / walkfactor);
                    if (!(nodeset.GetNodeIdByIndex(nodeIndex) is uint egoNodeId))
                        continue;

                    // Get the attr value for ego
                    string startNodeAttrString = nodeset.GetNodeAttribute(egoNodeId, attrIndex) is NodeAttributeValue navStart ? navStart.ToString(attrType) : "(missing)";

                    // Now take s steps 
                    bool abort = false;
                    uint currentNodeId = egoNodeId;
                    for (int j = 0; j < s; j++)
                    {
                        // Get an alter node from the current node
                        var randomAlterResult = Analyses.GetRandomAlter(network, currentNodeId, layers![0], EdgeTraversal.Out, balanced, weighted);
                        // If this didn't work, e.g. no more alters in that direction, then abort the whole walk
                        if (!randomAlterResult.Success)
                        {
                            abort = false;
                            break;
                        }
                        // As long as the alter node is not back at the start, update the current one
                        // An alternative would be to abort this walk completely
                        if (randomAlterResult.Value != egoNodeId)
                            currentNodeId = randomAlterResult.Value;

                    }

                    // If this was aborted: move to next node, just skip this one
                    if (abort)
                        continue;
                        
                    // Ok, done s walks: now I should store s
                    string endNodeAttrString = nodeset.GetNodeAttribute(currentNodeId, attrIndex) is NodeAttributeValue navEnd ? navEnd.ToString(attrType) : "(missing)";

                    uint nodeIdFrom = nodeAttributeStringToNodeId[startNodeAttrString];
                    uint nodeIdTo = nodeAttributeStringToNodeId[endNodeAttrString];
                    if (resultsDict.TryGetValue((nodeIdFrom, nodeIdTo), out int value))
                        resultsDict[(nodeIdFrom, nodeIdTo)] = value + 1;
                    else
                        resultsDict[(nodeIdFrom, nodeIdTo)] = 1;
                }

                LayerOneMode resultLayer = new LayerOneMode(attrName + "_steps_" + s, EdgeDirectionality.Directed, EdgeType.Valued, true);
                foreach (var kvp in resultsDict)
                    resultLayer.AddEdge(kvp.Key.Item1, kvp.Key.Item2, kvp.Value);
                networkResults.Layers.Add(resultLayer.Name, resultLayer);
            }

            // Return results
            StructureResult results = new StructureResult(networkResults, new Dictionary<string, IStructure> { { "nodeset", nodesetResults } });

            return OperationResult<StructureResult>.Ok(results, "Random walker done!");



            //return OperationResult<StructureResult>.Fail("NotYetImplemented", $"RandomWalkNodeAttributeDistances not yet implemented");

        }
    }
}
