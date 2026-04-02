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

            // Validate layer names if specified (null means use all layers)
            if (layers != null)
                foreach (string layerName in layers)
                    if (!network.Layers.ContainsKey(layerName))
                        return OperationResult<StructureResult>.Fail("LayerNotFound", $"Layer '{layerName}' not found in network '{network.Name}'.");

            // Build collection of unique attribute values
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

            string[] labels = uniqueAttrValues.OrderBy(s => s).ToArray();
            if (hasMissingValues)
                labels = [.. labels, "(missing)"];

            // Create result nodeset: one node per unique attribute value, with a 'label' string attribute
            Nodeset nodesetResults = new Nodeset(attrName + "_values");
            var defineResult = nodesetResults.DefineNodeAttribute("label", NodeAttributeType.String);
            byte labelIndex = defineResult.Value;
            Dictionary<string, uint> nodeAttributeStringToNodeId = [];
            for (uint i = 0; i < (uint)labels.Length; i++)
            {
                int poolIndex = nodesetResults.GetOrAddStringToPool(labels[i]);
                nodesetResults._addNodeWithAttributes(i, (new List<byte> { labelIndex }, new List<NodeAttributeValue> { new NodeAttributeValue(poolIndex) }));
                nodeAttributeStringToNodeId[labels[i]] = i;
            }
            Network networkResults = new Network(attrName + "_rwdistances_results", nodesetResults);

            int nbrNodesPerStepLevel = (int)(nodeset.Count * walkfactor);

            // For each step length s, run walks picking alters from all specified layers simultaneously
            for (int s = 1; s <= maxSteps; s++)
            {
                Dictionary<(uint, uint), int> resultsDict = [];

                for (int i = 0; i < nbrNodesPerStepLevel; i++)
                {
                    uint nodeIndex = (uint)Math.Floor(i / walkfactor);
                    if (!(nodeset.GetNodeIdByIndex(nodeIndex) is uint egoNodeId))
                        continue;

                    string startNodeAttrString = nodeset.GetNodeAttribute(egoNodeId, attrIndex) is NodeAttributeValue navStart
                        ? (attrType == NodeAttributeType.String
                            ? nodeset.GetStringFromPool((int)navStart.GetValue(attrType)!)
                            : navStart.ToString(attrType))
                        : "(missing)";

                    bool abort = false;
                    uint currentNodeId = egoNodeId;
                    uint? previousNodeId = null;

                    for (int j = 0; j < s; j++)
                    {
                        // Pick a random alter across all specified layers (null = all layers)
                        var randomAlterResult = Analyses.GetRandomAlter(network, currentNodeId, layers, EdgeTraversal.Out, balanced, weighted);
                        if (!randomAlterResult.Success)
                        {
                            abort = true;
                            break;
                        }

                        uint candidateId = randomAlterResult.Value;

                        // Enforce no backtrack: try once more to avoid stepping back to the previous node
                        if (!backtrack && previousNodeId.HasValue && candidateId == previousNodeId.Value)
                        {
                            var retryResult = Analyses.GetRandomAlter(network, currentNodeId, layers, EdgeTraversal.Out, balanced, weighted);
                            if (retryResult.Success && retryResult.Value != previousNodeId.Value)
                                candidateId = retryResult.Value;
                            // else: accept the backtrack rather than aborting
                        }

                        previousNodeId = currentNodeId;
                        currentNodeId = candidateId;
                    }

                    if (abort)
                        continue;

                    string endNodeAttrString = nodeset.GetNodeAttribute(currentNodeId, attrIndex) is NodeAttributeValue navEnd
                        ? (attrType == NodeAttributeType.String
                            ? nodeset.GetStringFromPool((int)navEnd.GetValue(attrType)!)
                            : navEnd.ToString(attrType))
                        : "(missing)";

                    uint nodeIdFrom = nodeAttributeStringToNodeId[startNodeAttrString];
                    uint nodeIdTo = nodeAttributeStringToNodeId[endNodeAttrString];
                    if (resultsDict.TryGetValue((nodeIdFrom, nodeIdTo), out int existingCount))
                        resultsDict[(nodeIdFrom, nodeIdTo)] = existingCount + 1;
                    else
                        resultsDict[(nodeIdFrom, nodeIdTo)] = 1;
                }

                LayerOneMode resultLayer = new LayerOneMode(attrName + "_steps_" + s, EdgeDirectionality.Directed, EdgeType.Valued, true);
                foreach (var kvp in resultsDict)
                    resultLayer.AddEdge(kvp.Key.Item1, kvp.Key.Item2, kvp.Value);
                networkResults.Layers.Add(resultLayer.Name, resultLayer);
            }

            // Compute average distance layer: avgDistance(A→B) = Σ(s × count[A,B,s]) / Σ(count[A,B,s])
            Dictionary<(uint from, uint to), (float weightedSum, float totalCount)> avgDict = [];
            for (int s = 1; s <= maxSteps; s++)
            {
                if (!networkResults.Layers.TryGetValue(attrName + "_steps_" + s, out ILayer? stepLayer))
                    continue;
                foreach (var (egoId, alters, values) in ((ILayerOneMode)stepLayer).GetAllEgoData())
                {
                    ReadOnlySpan<uint> alterSpan = alters.Span;
                    ReadOnlySpan<float> valSpan = values.Span;
                    for (int k = 0; k < alterSpan.Length; k++)
                    {
                        var key = (egoId, alterSpan[k]);
                        float count = valSpan.IsEmpty ? 1f : valSpan[k];
                        if (avgDict.TryGetValue(key, out var existing))
                            avgDict[key] = (existing.weightedSum + s * count, existing.totalCount + count);
                        else
                            avgDict[key] = (s * count, count);
                    }
                }
            }

            LayerOneMode avgLayer = new LayerOneMode(attrName + "_avgdistance", EdgeDirectionality.Directed, EdgeType.Valued, true);
            foreach (var kvp in avgDict)
                avgLayer.AddEdge(kvp.Key.from, kvp.Key.to, kvp.Value.weightedSum / kvp.Value.totalCount);
            networkResults.Layers.Add(avgLayer.Name, avgLayer);

            // Compute standard deviation distance layer using the step layers and avgdistance
            Dictionary<(uint from, uint to), (float weightedSumSq, float totalCount)> stdevDict = [];
            for (int s = 1; s <= maxSteps; s++)
            {
                if (!networkResults.Layers.TryGetValue(attrName + "_steps_" + s, out ILayer? stepLayer))
                    continue;
                foreach (var (egoId, alters, values) in ((ILayerOneMode)stepLayer).GetAllEgoData())
                {
                    ReadOnlySpan<uint> alterSpan = alters.Span;
                    ReadOnlySpan<float> valSpan = values.Span;
                    for (int k = 0; k < alterSpan.Length; k++)
                    {
                        var key = (egoId, alterSpan[k]);
                        float count = valSpan.IsEmpty ? 1f : valSpan[k];
                        float avg = avgLayer.GetEdgeValue(key.Item1, key.Item2);
                        float diff = s - avg;
                        if (stdevDict.TryGetValue(key, out var existing))
                            stdevDict[key] = (existing.weightedSumSq + diff * diff * count, existing.totalCount + count);
                        else
                            stdevDict[key] = (diff * diff * count, count);
                    }
                }
            }

            LayerOneMode stdevLayer = new LayerOneMode(attrName + "_stdevdistance", EdgeDirectionality.Directed, EdgeType.Valued, true);
            foreach (var kvp in stdevDict)
                stdevLayer.AddEdge(kvp.Key.from, kvp.Key.to, (float)Math.Sqrt(kvp.Value.weightedSumSq / kvp.Value.totalCount));
            networkResults.Layers.Add(stdevLayer.Name, stdevLayer);

            // Remove step layers if savesteps=false
            if (!savesteps)
                for (int s = 1; s <= maxSteps; s++)
                    networkResults.Layers.Remove(attrName + "_steps_" + s);

            StructureResult results = new StructureResult(networkResults, new Dictionary<string, IStructure> { { "nodeset", nodesetResults } });
            return OperationResult<StructureResult>.Ok(results, $"Random walk distances computed. {labels.Length} unique attribute values, {maxSteps} step levels.");
            //        Nodeset nodeset = network.Nodeset;
            //        var nodeAttributeInfo = nodeset.NodeAttributeDefinitionManager.GetNodeAttributeDefinition(attrName);
            //        if (nodeAttributeInfo == null)
            //            return OperationResult<StructureResult>.Fail("AttributeUnknown", $"Attribute '{attrName}' not found in nodeset '{nodeset.Name}'.");

            //        NodeAttributeType attrType = nodeAttributeInfo.Value.AttrType;
            //        byte attrIndex = nodeAttributeInfo.Value.Index;

            //        if (attrType != NodeAttributeType.String && attrType != NodeAttributeType.Char && attrType != NodeAttributeType.Int)
            //            return OperationResult<StructureResult>.Fail("InvalidAttributeType", $"Attribute '{attrName}' is of type '{nodeAttributeInfo.Value.AttrType}': must be char, integer, or string.");

            //        // Build up collection of unique attribute values
            //        // Maybe externalize: could be useful elsewhere
            //        HashSet<string> uniqueAttrValues = [];
            //        bool hasMissingValues = false;
            //        foreach (uint nodeId in nodeset.NodeIdArray)
            //        {
            //            if (!(nodeset.GetNodeAttribute(nodeId, attrIndex) is NodeAttributeValue attributeValue))
            //                hasMissingValues = true;
            //            else
            //            {
            //                string label = attrType == NodeAttributeType.String
            //                    ? nodeset.GetStringFromPool((int)attributeValue.GetValue(attrType)!)
            //                    : attributeValue.ToString(attrType);
            //                uniqueAttrValues.Add(label);
            //            }
            //        }
            //        if (hasMissingValues)
            //            uniqueAttrValues.Add("(missing)");

            //        // Create a string array with unique attribute values
            //        string[] labels = uniqueAttrValues.ToArray();

            //        // Create nodeset for result network and create as many nodes as there are unique node attribute values
            //        Nodeset nodesetResults = new Nodeset(attrName + "_values");
            //        // Define a 'label' attribute
            //        var defineResult = nodesetResults.DefineNodeAttribute("label", NodeAttributeType.String);
            //        byte labelIndex = defineResult.Value;
            //        Dictionary<string, uint> nodeAttributeStringToNodeId = [];
            //        for (uint i = 0; i < (uint)labels.Length; i++)
            //        {
            //            int poolIndex = nodesetResults.GetOrAddStringToPool(labels[i]);
            //            nodesetResults._addNodeWithAttributes(i, (new List<byte> { labelIndex }, new List<NodeAttributeValue> { new NodeAttributeValue(poolIndex) }));
            //            nodeAttributeStringToNodeId[labels[i]] = i;
            //        }
            //        // Create network for results
            //        Network networkResults = new Network(attrName + "_rwdistances_results", nodesetResults);

            //        // Determine how many nodes should be included in each step level
            //        // This is related to walkFactor: if walkFactor=0.5, only every second should be included
            //        // if walkfactor = 3, each node should be included 3 times
            //        int nbrNodesPerStepLevel = (int)(nodeset.Count * walkfactor);

            //        // Iterate through all with given path lengths (i.e. s as number of steps
            //        for (int s = 1; s <= maxSteps; s++)
            //        {
            //            Dictionary<(uint, uint), int> resultsDict = [];
            //            for (int i = 0; i < nbrNodesPerStepLevel; i++)
            //            {
            //                uint nodeIndex = (uint)Math.Floor(i / walkfactor);
            //                if (!(nodeset.GetNodeIdByIndex(nodeIndex) is uint egoNodeId))
            //                    continue;

            //                // Get the attr value for ego
            //                //string startNodeAttrString = nodeset.GetNodeAttribute(egoNodeId, attrIndex) is NodeAttributeValue navStart
            //                //    ? nodeset.GetStringFromPool((int)navStart.GetValue(attrType)!)
            //                //    : "(missing)";
            //                string startNodeAttrString = nodeset.GetNodeAttribute(egoNodeId, attrIndex) is NodeAttributeValue navStart
            //? (attrType == NodeAttributeType.String
            //    ? nodeset.GetStringFromPool((int)navStart.GetValue(attrType)!)
            //    : navStart.ToString(attrType))
            //: "(missing)";


            //                // Now take s steps 
            //                bool abort = false;
            //                uint currentNodeId = egoNodeId;
            //                for (int j = 0; j < s; j++)
            //                {
            //                    // Get an alter node from the current node
            //                    var randomAlterResult = Analyses.GetRandomAlter(network, currentNodeId, layers![0], EdgeTraversal.Out, balanced, weighted);
            //                    // If this didn't work, e.g. no more alters in that direction, then abort the whole walk
            //                    if (!randomAlterResult.Success)
            //                    {
            //                        abort = true;
            //                        break;
            //                    }
            //                    // As long as the alter node is not back at the start, update the current one
            //                    // An alternative would be to abort this walk completely
            //                    if (randomAlterResult.Value != egoNodeId)
            //                        currentNodeId = randomAlterResult.Value;

            //                }

            //                // If this was aborted: move to next node, just skip this one
            //                if (abort)
            //                    continue;

            //                // Ok, done s walks: now I should store s
            //                //string endNodeAttrString = nodeset.GetNodeAttribute(currentNodeId, attrIndex) is NodeAttributeValue navEnd
            //                //    ? nodeset.GetStringFromPool((int)navEnd.GetValue(attrType)!)
            //                //    : "(missing)";

            //                string endNodeAttrString = nodeset.GetNodeAttribute(currentNodeId, attrIndex) is NodeAttributeValue navEnd
            //? (attrType == NodeAttributeType.String
            //    ? nodeset.GetStringFromPool((int)navEnd.GetValue(attrType)!)
            //    : navEnd.ToString(attrType))
            //: "(missing)";


            //                uint nodeIdFrom = nodeAttributeStringToNodeId[startNodeAttrString];
            //                uint nodeIdTo = nodeAttributeStringToNodeId[endNodeAttrString];
            //                if (resultsDict.TryGetValue((nodeIdFrom, nodeIdTo), out int value))
            //                    resultsDict[(nodeIdFrom, nodeIdTo)] = value + 1;
            //                else
            //                    resultsDict[(nodeIdFrom, nodeIdTo)] = 1;
            //            }

            //            LayerOneMode resultLayer = new LayerOneMode(attrName + "_steps_" + s, EdgeDirectionality.Directed, EdgeType.Valued, true);
            //            foreach (var kvp in resultsDict)
            //                resultLayer.AddEdge(kvp.Key.Item1, kvp.Key.Item2, kvp.Value);
            //            networkResults.Layers.Add(resultLayer.Name, resultLayer);
            //        }

            //        // Return results
            //        StructureResult results = new StructureResult(networkResults, new Dictionary<string, IStructure> { { "nodeset", nodesetResults } });

            //        return OperationResult<StructureResult>.Ok(results, "Random walker done!");



            //        //return OperationResult<StructureResult>.Fail("NotYetImplemented", $"RandomWalkNodeAttributeDistances not yet implemented");

        }
    }
}
