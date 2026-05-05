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
        public static OperationResult<StructureResult> ShortestPathsNodeAttributeDistances(Network network, string attrName, string[]? layerNames)
        {
            if (CheckLayersExist(network, layerNames) is OperationResult result)
                return OperationResult<StructureResult>.Fail(result);

            Nodeset nodeset = network.Nodeset;
            var categoryResult = BuildCategoryNodeset(network, attrName);
            if (!categoryResult.Success)
                return OperationResult<StructureResult>.Fail(categoryResult);
            CategoryNodeset cat = categoryResult.Value;
            Nodeset nodesetResults = cat.Nodeset;
            Dictionary<string, uint> labelToNodeId = cat.LabelToNodeId;
            string[] labels = cat.Labels;
            NodeAttributeType attrType = cat.AttrType;
            byte attrIndex = cat.AttrIndex;

            List<ILayer> resolvedLayers = [];
            if (layerNames == null)
                resolvedLayers.AddRange(network.Layers.Values);
            else
                foreach (string ln in layerNames)
                {
                    var lr = network.GetLayer(ln);
                    if (!lr.Success)
                        return OperationResult<StructureResult>.Fail(lr);
                    resolvedLayers.Add(lr.Value!);
                }

            string GetCategoryString(uint nodeId) => nodeset.GetNodeAttribute(nodeId, attrIndex) is NodeAttributeValue nav
                ? (attrType == NodeAttributeType.String
                    ? nodeset.GetStringFromPool((int)nav.GetValue(attrType)!)
                    : nav.ToString(attrType))
                : "(missing)";

            Dictionary<(uint from, uint to), (double sum, double sumSq, int count)> distDict = [];
            Dictionary<(uint from, uint to), Dictionary<int, int>> distHistDict = [];
            uint[] allNodeIds = nodeset.NodeIdArray;

            foreach (uint sourceNodeId in allNodeIds)
            {
                if (!labelToNodeId.TryGetValue(GetCategoryString(sourceNodeId), out uint sourceCatId))
                    continue;

                // BFS from sourceNodeId; use distances dict as visited set
                Queue<uint> queue = [];
                Dictionary<uint, int> distances = [];
                queue.Enqueue(sourceNodeId);
                distances[sourceNodeId] = 0;
                while (queue.Count > 0)
                {
                    uint current = queue.Dequeue();
                    int nextDist = distances[current] + 1;
                    foreach (var layer in resolvedLayers)
                        foreach (uint neighborId in layer.GetNodeAlters(current, EdgeTraversal.Out))
                        {
                            if (!distances.ContainsKey(neighborId))
                            {
                                distances[neighborId] = nextDist;
                                queue.Enqueue(neighborId);
                            }
                        }
                }

                foreach (uint targetNodeId in allNodeIds)
                {
                    if (targetNodeId == sourceNodeId)
                        continue;
                    if (!distances.TryGetValue(targetNodeId, out int dist))
                        continue;
                    if (!labelToNodeId.TryGetValue(GetCategoryString(targetNodeId), out uint targetCatId))
                        continue;
                    double d = dist;
                    var key = (sourceCatId, targetCatId);
                    if (distDict.TryGetValue(key, out var existing))
                        distDict[key] = (existing.sum + d, existing.sumSq + d * d, existing.count + 1);
                    else
                        distDict[key] = (d, d * d, 1);
                    if (!distHistDict.TryGetValue(key, out var hist))
                        distHistDict[key] = hist = [];
                    hist[dist] = hist.TryGetValue(dist, out int binCount) ? binCount + 1 : 1;
                }
            }

            Network networkResults = new Network(attrName + "_sp_results", nodesetResults);
            LayerOneMode avgLayer = new LayerOneMode(attrName + "_sp_avg", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode q1Layer = new LayerOneMode(attrName + "_sp_q1", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode medianLayer = new LayerOneMode(attrName + "_sp_median", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode q3Layer = new LayerOneMode(attrName + "_sp_q3", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode stdevLayer = new LayerOneMode(attrName + "_sp_stdev", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode seLayer = new LayerOneMode(attrName + "_sp_se", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode countLayer = new LayerOneMode(attrName + "_sp_count", EdgeDirectionality.Directed, EdgeType.Valued, true);

            foreach (var kvp in distDict)
            {
                int count = kvp.Value.count;
                float mean = (float)(kvp.Value.sum / count);
                float variance = count > 1
                    ? (float)((kvp.Value.sumSq - kvp.Value.sum * kvp.Value.sum / count) / (count - 1))
                    : 0f;
                float stdev = (float)Math.Sqrt(Math.Max(0f, variance));

                //// Compute median from sparse histogram by scanning sorted distance values
                //float median = 0f;
                //if (distHistDict.TryGetValue(kvp.Key, out var hist))
                //{
                //    int lowerPos = (count + 1) / 2;
                //    int upperPos = count / 2 + 1;
                //    float lowerVal = 0f, upperVal = 0f;
                //    int cumulative = 0;
                //    foreach (int d in hist.Keys.OrderBy(k => k))
                //    {
                //        cumulative += hist[d];
                //        if (lowerVal == 0f && cumulative >= lowerPos)
                //            lowerVal = d;
                //        if (cumulative >= upperPos)
                //        {
                //            upperVal = d;
                //            break;
                //        }
                //    }
                //    median = (lowerVal + upperVal) / 2f;
                //}

                (uint from, uint to) = kvp.Key;
                avgLayer.AddEdge(from, to, mean);

                if (distHistDict.TryGetValue(kvp.Key, out var hist))
                {
                    q1Layer.AddEdge(from, to, PercentileFromHistogram(hist, count, 0.25f));
                    medianLayer.AddEdge(from, to, PercentileFromHistogram(hist, count, 0.50f));
                    q3Layer.AddEdge(from, to, PercentileFromHistogram(hist, count, 0.75f));
                }

                stdevLayer.AddEdge(from, to, stdev);
                seLayer.AddEdge(from, to, stdev / (float)Math.Sqrt(count));
                countLayer.AddEdge(from, to, count);
            }

            networkResults.Layers.Add(avgLayer.Name, avgLayer);
            networkResults.Layers.Add(q1Layer.Name, q1Layer);
            networkResults.Layers.Add(medianLayer.Name, medianLayer);
            networkResults.Layers.Add(q3Layer.Name, q3Layer);
            networkResults.Layers.Add(stdevLayer.Name, stdevLayer);
            networkResults.Layers.Add(seLayer.Name, seLayer);
            networkResults.Layers.Add(countLayer.Name, countLayer);

            StructureResult structureResult = new StructureResult(networkResults, new Dictionary<string, IStructure> { { "nodeset", nodesetResults } });
            int totalPairs = distDict.Values.Sum(v => v.count);
            return OperationResult<StructureResult>.Ok(structureResult, $"Shortest paths computed. {labels.Length} unique attribute values, {totalPairs} reachable node pairs.");
        }


        public static OperationResult<StructureResult> RandomWalkNodeAttributeDistances(Network network, string attrName, int maxSteps, string[]? layers, float walkfactor = 1.0f, bool balanced = false, bool weighted = false, bool backtrack = false, bool savesteps = false)
        {
            if (CheckLayersExist(network, layers) is OperationResult result)
                return OperationResult<StructureResult>.Fail(result);

            if (walkfactor <= 0)
                return OperationResult<StructureResult>.Fail("InvalidParameter", $"The 'walkfactor' parameter must be greater than zero.");
            if (maxSteps <= 0)
                return OperationResult<StructureResult>.Fail("InvalidParameter", $"The 'maxSteps' parameter must be greater than zero.");

            Nodeset nodeset = network.Nodeset;

            var categoryResult = BuildCategoryNodeset(network, attrName);
            if (!categoryResult.Success)
                return OperationResult<StructureResult>.Fail(categoryResult.Code, categoryResult.Message);
            CategoryNodeset cat = categoryResult.Value;
            Nodeset nodesetResults = cat.Nodeset;
            Dictionary<string, uint> nodeAttributeStringToNodeId = cat.LabelToNodeId;
            string[] labels = cat.Labels;
            NodeAttributeType attrType = cat.AttrType;
            byte attrIndex = cat.AttrIndex;

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

                    if (!nodeAttributeStringToNodeId.TryGetValue(startNodeAttrString, out uint nodeIdFrom) ||
                        !nodeAttributeStringToNodeId.TryGetValue(endNodeAttrString, out uint nodeIdTo))
                        continue;

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
                stdevLayer.AddEdge(kvp.Key.from, kvp.Key.to,
                    kvp.Value.totalCount > 1
                    ? (float)Math.Sqrt(kvp.Value.weightedSumSq / (kvp.Value.totalCount - 1))
                    : 0f);
            networkResults.Layers.Add(stdevLayer.Name, stdevLayer);

            // Remove step layers if savesteps=false
            if (!savesteps)
                for (int s = 1; s <= maxSteps; s++)
                    networkResults.Layers.Remove(attrName + "_steps_" + s);

            StructureResult results = new StructureResult(networkResults, new Dictionary<string, IStructure> { { "nodeset", nodesetResults } });
            return OperationResult<StructureResult>.Ok(results, $"Random walk distances computed. {labels.Length} unique attribute values, {maxSteps} step levels.");
        }

        public static OperationResult<StructureResult> RandomWalkNodeAttributeFirstPassageTimeDistances(Network network, string attrName, int maxSteps, string[]? layers, float walkfactor, int minPairObs, bool balanced, bool weighted)
        {
            if (CheckLayersExist(network, layers) is OperationResult result)
                return OperationResult<StructureResult>.Fail(result);

            if (walkfactor <= 0)
                return OperationResult<StructureResult>.Fail("InvalidParameter", $"The 'walkfactor' parameter must be greater than zero.");
            if (maxSteps <= 0)
                return OperationResult<StructureResult>.Fail("InvalidParameter", $"The 'maxSteps' parameter must be greater than zero.");
            Nodeset nodeset = network.Nodeset;
            var categoryResult = BuildCategoryNodeset(network, attrName);
            if (!categoryResult.Success)
                return OperationResult<StructureResult>.Fail(categoryResult.Code, categoryResult.Message);
            CategoryNodeset cat = categoryResult.Value;
            Nodeset nodesetResults = cat.Nodeset;
            Dictionary<string, uint> nodeAttributeStringToNodeId = cat.LabelToNodeId;
            string[] labels = cat.Labels;
            NodeAttributeType attrType = cat.AttrType;
            byte attrIndex = cat.AttrIndex;

            Network networkResults = new Network(network.Name + "_" + attrName + "_rwfpt_results", nodesetResults);

            string GetCategoryString(uint nodeId) => nodeset.GetNodeAttribute(nodeId, attrIndex) is NodeAttributeValue nav
                ? (attrType == NodeAttributeType.String
                    ? nodeset.GetStringFromPool((int)nav.GetValue(attrType)!)
                    : nav.ToString(attrType))
                : "(missing)";

            Dictionary<(uint from, uint to), int[]> fptHistograms = [];
            Dictionary<uint, int> sourceWalkCount = [];
            
            void RunWalk(uint startNodeId)
            {
                if (!nodeAttributeStringToNodeId.TryGetValue(GetCategoryString(startNodeId), out uint sourceCatId))
                    return; // node's category not in the map, skip this walk
                if (sourceWalkCount.TryGetValue(sourceCatId, out int existingSWC))
                    sourceWalkCount[sourceCatId] = existingSWC + 1;
                else
                    sourceWalkCount[sourceCatId] = 1;
                HashSet<uint> seen = [];
                uint currentNodeId = startNodeId;
                for (int step=1; step<=maxSteps;step++)
                {
                    var alterResult = Analyses.GetRandomAlter(network, currentNodeId, layers, EdgeTraversal.Out, balanced, weighted);
                    if (!alterResult.Success)
                        break;

                    currentNodeId = alterResult.Value;
                    if (!nodeAttributeStringToNodeId.TryGetValue(GetCategoryString(currentNodeId), out uint currentCatId))
                        continue; // unmapped category at this step, skip recording but keep walking
                    if (seen.Add(currentCatId))
                    {
                        var key = (sourceCatId, currentCatId);
                        if (!fptHistograms.TryGetValue(key, out int[]? hist))
                            fptHistograms[key] = hist = new int[maxSteps];
                        hist[step - 1]++;
                    }
                    if (seen.Count == labels.Length)
                        break;
                }
            }

            // Initial pass
            uint[] allNodeIds = nodeset.NodeIdArray;
            int nbrWalks = (int)(allNodeIds.Length * walkfactor);
            for (int i=0; i<nbrWalks;i++)
            {
                uint nodeIndex = (uint)Math.Floor(i / walkfactor);
                if (nodeIndex < allNodeIds.Length)
                    RunWalk(allNodeIds[nodeIndex]);
            }

            // Targeted restarts for undersampled category-pairs
            if (minPairObs > 0)
            {
                Dictionary<uint, List<uint>> categoryToNodes = [];
                foreach (uint nodeId in nodeset.NodeIdArray)
                {
                    if (!nodeAttributeStringToNodeId.TryGetValue(GetCategoryString(nodeId), out uint catId))
                        continue;
                    if (!categoryToNodes.TryGetValue(catId, out var nodeList))
                        categoryToNodes[catId] = nodeList = [];
                    nodeList.Add(nodeId);
                }

                foreach (var (sourceCatId, sourceNodes) in categoryToNodes)
                {
                    int nodeIdx = 0;
                    int maxAttempts = minPairObs * labels.Length * 3;
                    for (int attempt = 0; attempt < maxAttempts; attempt++)
                    {
                        bool allSatisfied = true;
                        for (uint t = 0; t < (uint)labels.Length; t++)
                        {
                            if (!fptHistograms.TryGetValue((sourceCatId, t), out int[]? obs) || obs.Sum() < minPairObs)
                            {
                                allSatisfied = false;
                                break;
                            }
                        }
                        if (allSatisfied)
                            break;
                        RunWalk(sourceNodes[nodeIdx % sourceNodes.Count]);
                        nodeIdx++;
                    }
                }
            }

            // Build output layers
            LayerOneMode avgLayer = new LayerOneMode(attrName + "_fpt_avg", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode q1Layer = new LayerOneMode(attrName + "_fpt_q1", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode medianLayer = new LayerOneMode(attrName + "_fpt_median", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode q3Layer = new LayerOneMode(attrName + "_fpt_q3", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode stdevLayer = new LayerOneMode(attrName + "_fpt_stdev", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode seLayer = new LayerOneMode(attrName + "_fpt_se", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode countLayer = new LayerOneMode(attrName + "_fpt_count", EdgeDirectionality.Directed, EdgeType.Valued, true);
            LayerOneMode coverageLayer = new LayerOneMode(attrName + "_fpt_coverage", EdgeDirectionality.Directed, EdgeType.Valued, true);

            foreach (var kvp in fptHistograms)
            {
                int[] hist = kvp.Value;

                // Derive sum, sumSq, count from histogram
                int count = 0;
                double sum = 0, sumSq = 0;
                for (int s = 0; s < hist.Length; s++)
                {
                    if (hist[s] == 0) continue;
                    int step = s + 1;
                    count += hist[s];
                    sum += (double)step * hist[s];
                    sumSq += (double)step * step * hist[s];
                }
                if (count == 0) continue;

                float mean = (float)(sum / count);
                float variance = count > 1
                    ? (float)((sumSq - sum * sum / count) / (count - 1))
                    : 0f;
                float stdev = (float)Math.Sqrt(Math.Max(0f, variance));

                //// Compute median: scan cumulative histogram to find the lower and upper middle positions
                //int lowerPos = (count + 1) / 2;
                //int upperPos = count / 2 + 1;
                //float lowerVal = 0f, upperVal = 0f;
                //int cumulative = 0;
                //for (int s = 0; s < hist.Length; s++)
                //{
                //    cumulative += hist[s];
                //    if (lowerVal == 0f && cumulative >= lowerPos)
                //        lowerVal = s + 1;
                //    if (cumulative >= upperPos)
                //    {
                //        upperVal = s + 1;
                //        break;
                //    }
                //}
                //float median = (lowerVal + upperVal) / 2f;




                (uint from, uint to) = kvp.Key;
                avgLayer.AddEdge(from, to, mean);
                q1Layer.AddEdge(from, to, PercentileFromHistogram(hist, count, 0.25f));
                medianLayer.AddEdge(from, to, PercentileFromHistogram(hist, count, 0.50f));
                q3Layer.AddEdge(from, to, PercentileFromHistogram(hist, count, 0.75f));
                stdevLayer.AddEdge(from, to, stdev);
                seLayer.AddEdge(from, to, stdev / (float)Math.Sqrt(count));
                countLayer.AddEdge(from, to, count);
                if (sourceWalkCount.TryGetValue(from, out int totalWalks) && totalWalks > 0)
                    coverageLayer.AddEdge(from, to, (float)count / totalWalks);
            }

            networkResults.Layers.Add(avgLayer.Name, avgLayer);
            networkResults.Layers.Add(q1Layer.Name, q1Layer);
            networkResults.Layers.Add(medianLayer.Name, medianLayer);
            networkResults.Layers.Add(q3Layer.Name, q3Layer);
            networkResults.Layers.Add(stdevLayer.Name, stdevLayer);
            networkResults.Layers.Add(seLayer.Name, seLayer);
            networkResults.Layers.Add(countLayer.Name, countLayer);
            networkResults.Layers.Add(coverageLayer.Name, coverageLayer);

            StructureResult results = new StructureResult(networkResults, new Dictionary<string, IStructure> { { "nodeset", nodesetResults } });
            int totalObs = fptHistograms.Values.Sum(h => h.Sum());
            return OperationResult<StructureResult>.Ok(results, $"Random walk FPT distances computed. {labels.Length} unique attribute values, {totalObs} total observations.");
        }

        /// <summary>
        /// Returns the pth percentile from a fixed-size histogram (array) where bin i
        /// represents value i+1
        /// </summary>
        private static float PercentileFromHistogram(int[] hist, int count, float p)
        {
            int lowerPos = (int)Math.Ceiling(p * count);
            int upperPos = (int)Math.Floor(p * count) + 1;
            float lowerVal = 0f, upperVal = 0f;
            int cumulative = 0;
            for (int s = 0; s < hist.Length; s++)
            {
                cumulative += hist[s];
                if (lowerVal == 0f && cumulative >= lowerPos)
                    lowerVal = s + 1;
                if (cumulative >= upperPos)
                {
                    upperVal = s + 1;
                    break;
                }
            }
            return (lowerVal + upperVal) / 2f;
        }

        /// <summary>
        /// Returns the pth percentile from a sparse histogram represented by a dict int,int
        /// </summary>
        private static float PercentileFromHistogram(Dictionary<int, int> hist, int count, float p)
        {
            int lowerPos = (int)Math.Ceiling(p * count);
            int upperPos = (int)Math.Floor(p * count) + 1;
            float lowerVal = 0f, upperVal = 0f;
            int cumulative = 0;
            foreach (int d in hist.Keys.OrderBy(k => k))
            {
                cumulative += hist[d];
                if (lowerVal == 0f && cumulative >= lowerPos)
                    lowerVal = d;
                if (cumulative >= upperPos)
                {
                    upperVal = d;
                    break;
                }
            }
            return (lowerVal + upperVal) / 2f;
        }

        private static OperationResult? CheckLayersExist(Network network, string[]? layers)
        {
            if (layers != null)
                foreach (string layerName in layers)
                    if (!network.Layers.ContainsKey(layerName))
                        return OperationResult.Fail("LayerNotFound", $"Layer '{layerName}' not found in network '{network.Name}'.");
            return null;
        }

        private static OperationResult<CategoryNodeset> BuildCategoryNodeset(Network network, string attrName)
        {
            Nodeset nodeset = network.Nodeset;
            var nodeAttributeInfo = nodeset.NodeAttributeDefinitionManager.GetNodeAttributeDefinition(attrName);
            if (nodeAttributeInfo == null)
                return OperationResult<CategoryNodeset>.Fail("AttributeUnknown", $"Attribute '{attrName}' not found in nodeset '{nodeset.Name}'.");

            NodeAttributeType attrType = nodeAttributeInfo.Value.AttrType;
            byte attrIndex = nodeAttributeInfo.Value.Index;

            if (attrType != NodeAttributeType.String && attrType != NodeAttributeType.Char && attrType != NodeAttributeType.Int && attrType != NodeAttributeType.Bool)
                return OperationResult<CategoryNodeset>.Fail("InvalidAttributeType", $"Attribute '{attrName}' is of type '{attrType}': must be char, integer, bool, or string.");

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

            Nodeset nodesetResults = new Nodeset(attrName + "_values");
            byte labelIndex = nodesetResults.DefineNodeAttribute("label", NodeAttributeType.String).Value;
            Dictionary<string, uint> labelToNodeId = [];
            for (uint i = 0; i < (uint)labels.Length; i++)
            {
                int poolIndex = nodesetResults.GetOrAddStringToPool(labels[i]);
                nodesetResults._addNodeWithAttributes(i, (new List<byte> { labelIndex }, new List<NodeAttributeValue> { new NodeAttributeValue(poolIndex) }));
                labelToNodeId[labels[i]] = i;
            }

            return OperationResult<CategoryNodeset>.Ok(new CategoryNodeset
            {
                Nodeset = nodesetResults,
                LabelToNodeId = labelToNodeId,
                Labels = labels,
                AttrType = attrType,
                AttrIndex = attrIndex
            });

        }
    }
}
