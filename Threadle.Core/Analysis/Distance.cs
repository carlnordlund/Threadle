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
        /// <summary>
        /// Calculates exact shortest-path distances between all reachable node pairs and aggregates
        /// them by node attribute category, returning mean/quartile/stdev/count layers per category
        /// pair. Runs a full BFS from every node — O(N×(N+E)) — so it's only feasible for smaller
        /// networks; use shortestpaths() with a sampled set of node pairs for larger ones. Each
        /// source node's BFS and category-pair accumulation is independent of every other source's,
        /// so this runs in parallel across up to 'maxthreads' threads (see 'setting()').
        /// </summary>
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
            var mergeLock = new object();

            // Each source node's BFS and category-pair accumulation is independent of every
            // other source's, so this runs in parallel across up to 'maxthreads' threads (see
            // 'setting()'), with thread-local partial accumulators merged under a lock after
            // each thread's batch.
            System.Threading.Tasks.Parallel.For(0, allNodeIds.Length, UserSettings.GetParallelOptions(),
                () => (
                    DistDict: new Dictionary<(uint from, uint to), (double sum, double sumSq, int count)>(),
                    HistDict: new Dictionary<(uint from, uint to), Dictionary<int, int>>()
                ),
                (idx, loopState, local) =>
                {
                    uint sourceNodeId = allNodeIds[idx];
                    if (!labelToNodeId.TryGetValue(GetCategoryString(sourceNodeId), out uint sourceCatId))
                        return local;

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
                        if (local.DistDict.TryGetValue(key, out var existing))
                            local.DistDict[key] = (existing.sum + d, existing.sumSq + d * d, existing.count + 1);
                        else
                            local.DistDict[key] = (d, d * d, 1);
                        if (!local.HistDict.TryGetValue(key, out var hist))
                            local.HistDict[key] = hist = [];
                        hist[dist] = hist.TryGetValue(dist, out int binCount) ? binCount + 1 : 1;
                    }
                    return local;
                },
                local =>
                {
                    lock (mergeLock)
                    {
                        foreach (var (key, value) in local.DistDict)
                        {
                            if (distDict.TryGetValue(key, out var existing))
                                distDict[key] = (existing.sum + value.sum, existing.sumSq + value.sumSq, existing.count + value.count);
                            else
                                distDict[key] = value;
                        }
                        foreach (var (key, hist) in local.HistDict)
                        {
                            if (!distHistDict.TryGetValue(key, out var globalHist))
                                distHistDict[key] = globalHist = [];
                            foreach (var (dist, binCount) in hist)
                                globalHist[dist] = globalHist.TryGetValue(dist, out int existingCount) ? existingCount + binCount : binCount;
                        }
                    }
                });

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



        public static (OperationResult<StructureResult> Result, List<(string From, string To, int Step, long Count)>? Histograms) RandomWalkNodeAttributeFirstPassageTimeDistances(Network network, string attrName, int maxSteps, string[]? layers, float walkfactor, int minPairObs, bool balanced, bool weighted, bool returnHistograms = false)
        {
            if (CheckLayersExist(network, layers) is OperationResult result)
                return (OperationResult<StructureResult>.Fail(result), []);

            if (walkfactor <= 0)
                return (OperationResult<StructureResult>.Fail("InvalidParameter", $"The 'walkfactor' parameter must be greater than zero."), []);
            if (maxSteps <= 0)
                return (OperationResult<StructureResult>.Fail("InvalidParameter", $"The 'maxSteps' parameter must be greater than zero."), []);
            Nodeset nodeset = network.Nodeset;
            var categoryResult = BuildCategoryNodeset(network, attrName);
            if (!categoryResult.Success)
                return (OperationResult<StructureResult>.Fail(categoryResult.Code, categoryResult.Message), []);
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

            Dictionary<(uint from, uint to), long[]> fptHistograms = [];
            Dictionary<uint, int> sourceWalkCount = [];

            // Resolve layers once — captured by RunWalk closure, avoids per-step List<ILayer> allocation
            List<ILayer> resolvedLayers = [];
            if (layers == null)
                resolvedLayers.AddRange(network.Layers.Values);
            else
                foreach (string ln in layers)
                    resolvedLayers.Add(network.GetLayer(ln).Value!);

            void RunWalk(uint startNodeId, Dictionary<(uint from, uint to), long[]> histograms, Dictionary<uint, int> walkCounts)
            {
                if (!nodeAttributeStringToNodeId.TryGetValue(GetCategoryString(startNodeId), out uint sourceCatId))
                    return;
                walkCounts[sourceCatId] = walkCounts.TryGetValue(sourceCatId, out int existingSWC) ? existingSWC + 1 : 1;
                HashSet<uint> seen = [];
                uint currentNodeId = startNodeId;
                for (int step = 1; step <= maxSteps; step++)
                {
                    var randomAlterResult = Analyses.GetRandomAlter(currentNodeId, resolvedLayers, EdgeTraversal.Out, balanced, weighted);
                    if (!randomAlterResult.Success) break;
                    currentNodeId = randomAlterResult.Value;
                    if (currentNodeId == startNodeId || !nodeAttributeStringToNodeId.TryGetValue(GetCategoryString(currentNodeId), out uint currentCatId))
                        continue;
                    if (seen.Add(currentCatId))
                    {
                        var key = (sourceCatId, currentCatId);
                        if (!histograms.TryGetValue(key, out long[]? hist))
                            histograms[key] = hist = new long[maxSteps];
                        hist[step - 1]++;
                    }
                    if (seen.Count == labels.Length)
                        break;
                }
            }

            // Initial pass: walks are independent of each other, so this runs in parallel (up to the
            // 'maxthreads' setting) with each task accumulating into its own local histograms/walkCounts,
            // merged into the shared dictionaries once the task's share of walks is done. Because Misc.Random
            // is thread-static rather than seeded per worker thread, exact reproducibility via randomseed()
            // is only guaranteed when maxthreads is set to 1.
            uint[] allNodeIds = nodeset.NodeIdArray;
            int nbrWalks = (int)(allNodeIds.Length * walkfactor);
            object mergeLock = new();

            Parallel.For(0, nbrWalks, UserSettings.GetParallelOptions(),
                localInit: () => (Histograms: new Dictionary<(uint, uint), long[]>(), WalkCounts: new Dictionary<uint, int>()),
                body: (i, loopState, local) =>
                {
                    uint nodeIndex = (uint)Math.Floor(i / walkfactor);
                    if (nodeIndex < allNodeIds.Length)
                        RunWalk(allNodeIds[nodeIndex], local.Histograms, local.WalkCounts);
                    return local;
                },
                localFinally: local =>
                {
                    lock (mergeLock)
                    {
                        foreach (var (key, hist) in local.Histograms)
                        {
                            if (!fptHistograms.TryGetValue(key, out long[]? existingHist))
                                fptHistograms[key] = hist;
                            else
                                for (int s = 0; s < hist.Length; s++)
                                    existingHist[s] += hist[s];
                        }
                        foreach (var (catId, count) in local.WalkCounts)
                            sourceWalkCount[catId] = sourceWalkCount.TryGetValue(catId, out int existing) ? existing + count : count;
                    }
                });

            // Targeted restarts for undersampled category-pairs. Kept sequential: each attempt's
            // "are we done yet" check depends on the accumulated results of previous attempts for the
            // same source category, so this isn't embarrassingly parallel the way the initial pass is.
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
                    int maxAttempts = minPairObs * labels.Length * 3;
                    for (int attempt = 0; attempt < maxAttempts; attempt++)
                    {
                        bool allSatisfied = true;
                        for (uint t = 0; t < (uint)labels.Length; t++)
                        {
                            if (!fptHistograms.TryGetValue((sourceCatId, t), out long[]? obs) || obs.Sum() < minPairObs)
                            {
                                allSatisfied = false;
                                break;
                            }
                        }
                        if (allSatisfied)
                            break;
                        RunWalk(sourceNodes[Misc.Random.Next(sourceNodes.Count)], fptHistograms, sourceWalkCount);
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
                long[] hist = kvp.Value;

                // Derive sum, sumSq, count from histogram
                long count = 0;
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

            List<(string From, string To, int Step, long Count)>? histList = null;
            if (returnHistograms)
            {
                histList = new List<(string From, string To, int Step, long Count)>();
                foreach (var kvp in fptHistograms)
                {
                    string fromLabel = labels[kvp.Key.from];
                    string toLabel = labels[kvp.Key.to];
                    for (int s = 0; s < kvp.Value.Length; s++)
                        if (kvp.Value[s] > 0)
                            histList.Add((fromLabel, toLabel, s + 1, kvp.Value[s]));
                }
            }

            StructureResult results = new StructureResult(networkResults, new Dictionary<string, IStructure> { { "nodeset", nodesetResults } });
            long totalObs = fptHistograms.Values.Sum(h => h.Sum());
            return (OperationResult<StructureResult>.Ok(results, $"Random walk FPT distances computed. {labels.Length} unique attribute values, {totalObs} total observations."), histList);
        }

        /// <summary>
        /// Returns the pth percentile from a fixed-size histogram (array) where bin i
        /// represents value i+1
        /// </summary>
        private static float PercentileFromHistogram(long[] hist, long count, float p)
        {
            long lowerPos = (long)Math.Ceiling(p * count);
            long upperPos = (long)Math.Floor(p * count) + 1;
            float lowerVal = 0f, upperVal = 0f;
            long cumulative = 0;
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