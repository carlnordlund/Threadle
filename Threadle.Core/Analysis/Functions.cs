using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Analysis
{
    /// <summary>
    /// A collection of internal functions useful for the methods in Analyses.cs
    /// </summary>
    internal static class Functions
    {
        #region Methods (internal)
        /// <summary>
        /// Calculates node degree centrality measures of a specific 1-mode layer in a specific network.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layerOneMode">The 1-mode layer object.</param>
        /// <param name="edgeTraversal">A <see cref="EdgeTraversal"/> value specifying whether indegree, outdegree or grossdegree should be calculated.</param>
        /// <returns>Returns a dictionary with degree centrality measures by node id.</returns>
        internal static Dictionary<uint, uint> DegreeCentrality(Network network, ILayerOneMode layerOneMode, EdgeTraversal edgeTraversal)
        {
            Dictionary<uint, uint> degreeCentrality = edgeTraversal switch
            {
                EdgeTraversal.Out => OutDegreeCentrality(network, layerOneMode),
                EdgeTraversal.In => InDegreeCentrality(network, layerOneMode),
                _ => GrossDegreeCentrality(network, layerOneMode)
            };
            return degreeCentrality;
        }

        /// <summary>
        /// Calculates the degree centrality for a specific network and specific 2-mode layer.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layerOneMode">The 2-mode layer object.</param>
        /// <returns>Returns a dictionary with outdegree centrality measures by node id.</returns>
        internal static Dictionary<uint, uint> DegreeCentrality(Network network, ILayerTwoMode layerTwoMode)
        {
            Dictionary<uint, uint> degreeCentrality = [];
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                degreeCentrality[nodeId] = (uint)layerTwoMode.GetNodeAlters(nodeId, EdgeTraversal.Both).Length;
            return degreeCentrality;
        }

        /// <summary>
        /// Calculates the density of a specific 1-mode layer in a specific network.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layer">The 1-mode layer object.</param>
        /// <returns>Density (as a double value).</returns>
        internal static double Density(Network network, ILayerOneMode layer)
        {
            ulong nbrPotentialEdges = Misc.GetNbrPotentialEdges((ulong)network.Nodeset.Count, layer.Directionality, layer.Selfties);
            if (nbrPotentialEdges == 0)
                return 0.0;
            return (double)layer.NbrEdges / nbrPotentialEdges;
        }

        /// <summary>
        /// Calculates the density of a specific 2-mode layer in a specific network.
        /// Defaults to a 200-node sample (unlike most other samplesize-bearing commands, which
        /// default to an exact computation): summing every node's binary projected degree
        /// (<see cref="ILayerTwoMode.GetNodeAlters"/>) is NOT the O(sum of hyperedge sizes) a
        /// "pseudo-projected, nothing materialized" framing might suggest - GetNodeAlters itself
        /// re-scans a node's hyperedges from scratch on every call, with no cross-call or
        /// cross-hyperedge memoization, so summing it over every node costs O(sum of hyperedge
        /// sizes squared): the same order as materializing a full binary projection, just never
        /// persisted. That cost is inherent to an exact *binary* (deduplicated) degree count -
        /// the same reason <see cref="NetworkLevelFunctions.DyadCensusTwoMode"/>'s exact Mutual
        /// count has this cost too - not a missed optimization, so a sampled default is kept here
        /// specifically (sampleSize=0 still computes the exact value on request, at that real cost).
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layer">The 2-mode layer object.</param>
        /// <returns>Density (as a double value)</returns>
        internal static double Density(Network network, ILayerTwoMode layer, int sampleSize = 200)
        {
            int nbrNodes = network.Nodeset.Count;
            if (nbrNodes <= 1)
                return 0.0;

            ulong nbrPotentialEdges = (ulong)nbrNodes * (ulong)(nbrNodes - 1);
            uint[] allNodeIds = network.Nodeset.NodeIdArray;

            if (sampleSize <= 0 || sampleSize >= nbrNodes)
            {
                ulong sumAllAlters = 0;
                foreach (uint id in allNodeIds)
                    sumAllAlters += (ulong)layer.GetNodeAlters(id, EdgeTraversal.Out).Length;
                return (double)sumAllAlters / nbrPotentialEdges;
            }

            uint[] ids = (uint[])allNodeIds.Clone();
            Misc.SampleWithoutReplacementInPlace(ids, sampleSize);
            ulong sumAlters = 0;
            for (int i = 0; i < sampleSize; i++)
                sumAlters += (ulong)layer.GetNodeAlters(ids[i], EdgeTraversal.Out).Length;

            double meanAlters = (double)sumAlters / sampleSize;
            return meanAlters * nbrNodes / (double)nbrPotentialEdges;
        }

        /// <summary>
        /// Returns summary statistics about a node attribute of type char.
        /// </summary>
        /// <param name="nodeset">The Nodeset structure.</param>
        /// <param name="attrIndex">The attribute index.</param>
        /// <param name="countWithValues">Outbound variable with the number of nodes that have this attribute set.</param>
        /// <returns>Returns a string-object dictionary with summary statistics.</returns>
        internal static Dictionary<string, object> CalculateCharStatistics(Nodeset nodeset, byte attrIndex, out int countWithValues)
        {
            Dictionary<char, int> frequency = [];
            foreach (uint nodeId in nodeset.NodeIdArray)
            {
                var attrValue = nodeset.GetNodeAttribute(nodeId, attrIndex);
                if (attrValue != null)
                {
                    char charValue = (char)attrValue.Value.GetValue(NodeAttributeType.Char)!;
                    if (frequency.TryGetValue(charValue, out int count))
                        frequency[charValue] = count + 1;
                    else
                        frequency[charValue] = 1;
                }
            }
            countWithValues = frequency.Values.Sum();
            var frequencyForJsonOutput = frequency.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => (object)kvp.Value
                );

            Dictionary<string, object> stats = new()
            {
                ["Frequency"] = frequencyForJsonOutput,
                ["Unique_Values"] = frequency.Count
            };

            if (frequency.Count > 0)
            {
                var mode = frequency.OrderByDescending(kvp => kvp.Value).First();
                stats["Mode"] = mode.Key;
                stats["Mode_Count"] = mode.Value;
            }
            else
            {
                stats["Mode_Count"] = 0;
            }
            return stats;
        }

        /// <summary>
        /// Returns summary statistics about a node attribute of type bool.
        /// </summary>
        /// <param name="nodeset">The Nodeset structure.</param>
        /// <param name="attrIndex">The attribute index.</param>
        /// <param name="countWithValues">Outbound variable with the number of nodes that have this attribute set.</param>
        /// <returns>Returns a string-object dictionary with summary statistics.</returns>
        internal static Dictionary<string, object> CalculateBoolStatistics(Nodeset nodeset, byte attrIndex, out int countWithValue)
        {
            int countTrue = 0, countFalse = 0;
            foreach (uint nodeId in nodeset.NodeIdArray)
            {
                var attrValue = nodeset.GetNodeAttribute(nodeId, attrIndex);
                if (attrValue != null)
                {
                    if ((bool)attrValue.Value.GetValue(NodeAttributeType.Bool)!)
                        countTrue++;
                    else
                        countFalse++;
                }
            }
            countWithValue = countTrue + countFalse;
            Dictionary<string, object> stats = new()
            {
                ["Count_True"] = countTrue,
                ["Count_False"] = countFalse,
                ["Ratio_True"] = countWithValue > 0 ? (double)countTrue / countWithValue * 100.0 : 0.0
            };
            return stats;
        }

        /// <summary>
        /// Returns summary statistics about a node attribute of type float.
        /// </summary>
        /// <param name="nodeset">The Nodeset structure.</param>
        /// <param name="attrIndex">The attribute index.</param>
        /// <param name="countWithValues">Outbound variable with the number of nodes that have this attribute set.</param>
        /// <returns>Returns a string-object dictionary with summary statistics.</returns>
        internal static Dictionary<string, object> CalculateFloatStatistics(Nodeset nodeset, byte attrIndex, out int countWithValue)
        {
            List<float> values = [];

            foreach (uint nodeId in nodeset.NodeIdArray)
            {
                var attrValue = nodeset.GetNodeAttribute(nodeId, attrIndex);
                if (attrValue != null)
                    values.Add((float)attrValue.Value.GetValue(NodeAttributeType.Float)!);
            }
            countWithValue = values.Count;
            return CalculateValueStatistics(values);
        }

        /// <summary>
        /// Computes Mean/Median/StdDev/Min/Max/Q1/Q3 for an arbitrary set of float values. Shared
        /// by node-attribute statistics (<see cref="CalculateFloatStatistics"/>) and layer edge-value
        /// statistics (<see cref="GetLayerEdgeValues"/> callers), so both compute these the same way.
        /// Returns an empty dictionary if 'values' is empty.
        /// </summary>
        /// <param name="values">The values to summarize.</param>
        /// <returns>A string-object dictionary with Mean, Median, StdDev, Min, Max, Q1, Q3.</returns>
        internal static Dictionary<string, object> CalculateValueStatistics(List<float> values)
        {
            Dictionary<string, object> stats = [];
            if (values.Count == 0)
                return stats;

            double mean = values.Average();
            double variance = values.Count > 1 ? values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1) : 0.0;
            double stdDev = Math.Sqrt(variance);

            float[] sorted = [.. values];
            Array.Sort(sorted);

            stats["Mean"] = mean;
            stats["Median"] = GetPercentile(sorted, 50);
            stats["StdDev"] = stdDev;
            stats["Min"] = sorted[0];
            stats["Max"] = sorted[^1];
            stats["Q1"] = GetPercentile(sorted, 25);
            stats["Q3"] = GetPercentile(sorted, 75);
            return stats;
        }

        /// <summary>
        /// Gathers every edge value in a 1-mode layer into a flat list, for feeding into
        /// <see cref="CalculateValueStatistics"/>. For symmetric (undirected) layers, each edge is
        /// counted once (only when alterId &gt; egoId), matching the dedup convention
        /// Threadle.Core.Utilities.LayerImportExport.ExportOneModeEdgeList uses to avoid writing
        /// each undirected edge twice; directed layers count every arc independently, since each
        /// direction is its own tie with its own value.
        /// </summary>
        /// <param name="layer">The (valued) 1-mode layer to gather edge values from.</param>
        /// <param name="nodeIds">All node ids in the layer's nodeset (needed to find self-loops in
        /// symmetric layers, since GetAllEgoData() never yields them).</param>
        /// <returns>A flat list of every edge's value.</returns>
        internal static List<float> GetLayerEdgeValues(ILayerOneMode layer, uint[] nodeIds)
        {
            List<float> values = [];
            foreach (var (egoId, alters, edgeValues) in layer.GetAllEgoData())
            {
                var alterSpan = alters.Span;
                var valueSpan = edgeValues.Span;
                for (int i = 0; i < alterSpan.Length; i++)
                {
                    uint alterId = alterSpan[i];
                    if (!layer.IsDirectional && alterId <= egoId)
                        continue; // undirected: count each edge once
                    values.Add(valueSpan[i]);
                }
            }

            // GetAllEgoData() never yields self-loops for symmetric layers: add them explicitly,
            // once each (a self-loop has no "other side" to double-count).
            if (!layer.IsDirectional && layer.Selfties)
                foreach (uint nodeId in nodeIds)
                    if (layer.CheckEdgeExists(nodeId, nodeId))
                        values.Add(layer.GetEdgeValue(nodeId, nodeId));

            return values;
        }

        /// <summary>
        /// Returns summary statistics about a node attribute of type int.
        /// </summary>
        /// <param name="nodeset">The Nodeset structure.</param>
        /// <param name="attrIndex">The attribute index.</param>
        /// <param name="countWithValues">Outbound variable with the number of nodes that have this attribute set.</param>
        /// <returns>Returns a string-object dictionary with summary statistics.</returns>
        internal static Dictionary<string, object> CalculateIntStatistics(Nodeset nodeset, byte attrIndex, out int countWithValues)
        {
            List<int> values = [];
            foreach (uint nodeId in nodeset.NodeIdArray)
            {
                var attrValue = nodeset.GetNodeAttribute(nodeId, attrIndex);
                if (attrValue != null)
                    values.Add((int)attrValue.Value.GetValue(NodeAttributeType.Int)!);
            }
            countWithValues = values.Count;
            Dictionary<string, object> stats = [];
            if (values.Count == 0)
                return stats;

            double mean = values.Average();
            double variance = values.Count > 1 ? values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1) : 0.0;
            double stdDev = Math.Sqrt(variance);
            int[] sorted = [.. values];
            Array.Sort(sorted);

            stats["Mean"] = mean;
            stats["Median"] = GetPercentile(sorted, 50);
            stats["StdDev"] = stdDev;
            stats["Min"] = sorted[0];
            stats["Max"] = sorted[^1];
            stats["Q1"] = GetPercentile(sorted, 25);
            stats["Q3"] = GetPercentile(sorted, 75);
            return stats;
        }

        /// <summary>
        /// Returns summary statistics about a node attribute of type string.
        /// </summary>
        /// <param name="nodeset">The Nodeset structure.</param>
        /// <param name="attrIndex">The attribute index.</param>
        /// <param name="countWithValues">Outbound variable with the number of nodes that have this attribute set.</param>
        /// <returns>Returns a string-object dictionary with summary statistics.</returns>
        internal static Dictionary<string, object> CalculateStringStatistics(Nodeset nodeset, byte attrIndex, out int countWithValues)
        {
            Dictionary<string, int> frequency = [];
            foreach (uint nodeId in nodeset.NodeIdArray)
            {
                var attrValue = nodeset.GetNodeAttribute(nodeId, attrIndex);
                if (attrValue != null)
                {
                    string strValue = nodeset.GetStringFromPool((int)attrValue.Value.GetValue(NodeAttributeType.String)!);
                    if (frequency.TryGetValue(strValue, out int count))
                        frequency[strValue] = count + 1;
                    else
                        frequency[strValue] = 1;
                }
            }
            countWithValues = frequency.Values.Sum();

            var sorted = frequency.OrderByDescending(kvp => kvp.Value).ToList();
            const int topN = 50;

            var frequencyForOutput = sorted
                .Take(topN)
                .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);

            Dictionary<string, object> stats = new()
            {
                ["Frequency"] = frequencyForOutput,
                ["Unique_Values"] = frequency.Count,
                ["Frequency_Capped_At"] = topN
            };

            if (sorted.Count > 0)
            {
                stats["Mode"] = sorted[0].Key;
                stats["Mode_Count"] = sorted[0].Value;
            }
            else
            {
                stats["Mode_Count"] = 0;
            }
            return stats;
        }



        /// <summary>
        /// Support function to determine the percentile value from a sorted array of integers
        /// </summary>
        /// <param name="sortedValues">An array of sorted integer values</param>
        /// <param name="percentile">The percentile to determine (0-100)</param>
        /// <returns>Returns the percentile (double).</returns>
        internal static double GetPercentile(int[] sortedValues, double percentile)
        {
            if (sortedValues.Length == 0)
                return 0;
            double n = sortedValues.Length;
            double index = (percentile / 100.0) * (n - 1);
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper)
                return sortedValues[lower];
            double fraction = index - lower;
            return sortedValues[lower] * (1 - fraction) + sortedValues[upper] * fraction;
        }

        /// <summary>
        /// Support function to determine the percentile value from a sorted array of float values
        /// </summary>
        /// <param name="sortedValues">An array of sorted float values</param>
        /// <param name="percentile">The percentile to determine (0-100)</param>
        /// <returns>Returns the percentile (double).</returns>
        internal static double GetPercentile(float[] sortedValues, double percentile)
        {
            if (sortedValues.Length == 0)
                return 0;
            double n = sortedValues.Length;
            double index = (percentile / 100.0) * (n - 1);
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper)
                return sortedValues[lower];
            double fraction = index - lower;
            return sortedValues[lower] * (1 - fraction) + sortedValues[upper] * fraction;
        }

        internal static uint WeightedPick(List<(uint alterId, float weight)> candidates)
        {
            float total = 0f;
            foreach (var (_, w) in candidates) total += w;
            float r = (float)(Misc.Random.NextDouble() * total);
            float cumulative = 0f;
            foreach (var (alterId, weight) in candidates)
            {
                cumulative += weight;
                if (r < cumulative) return alterId;
            }
            return candidates[^1].alterId;
        }

        internal static void AppendWeightedCandidates(List<(uint, float)> candidates, ILayer layer, uint nodeId, EdgeTraversal edgeTraversal)
        {
            var (alters, weights) = layer.GetNodeAltersWithWeights(nodeId, edgeTraversal);
            var alterSpan = alters.Span;
            bool hasWeights = weights.Length > 0;
            var weightSpan = weights.Span;
            for (int i = 0; i < alterSpan.Length; i++)
                candidates.Add((alterSpan[i], hasWeights ? weightSpan[i] : 1f));
        }




        #endregion


        #region Methods (private)
        /// <summary>
        /// Calculates the gross degree centrality for a specific network and specific 1-mode layer.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layerOneMode">The 1-mode layer object.</param>
        /// <returns>Returns a dictionary with gross degree centrality measures by node id.</returns>
        private static Dictionary<uint, uint> GrossDegreeCentrality(Network network, ILayerOneMode layerOneMode)
        {
            Dictionary<uint, uint> grossDegreeCentrality = [];
            if (layerOneMode.IsSymmetric)
                foreach (var nodeId in network.Nodeset.NodeIdArray)
                    grossDegreeCentrality[nodeId] = layerOneMode.GetInDegree(nodeId);
            else
                foreach (var nodeId in network.Nodeset.NodeIdArray)
                    grossDegreeCentrality[nodeId] = layerOneMode.GetInDegree(nodeId) + layerOneMode.GetOutDegree(nodeId);
            return grossDegreeCentrality;
        }

        /// <summary>
        /// Calculates the indegree centrality for a specific network and specific 1-mode layer.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layerOneMode">The 1-mode layer object.</param>
        /// <returns>Returns a dictionary with indegree centrality measures by node id.</returns>
        private static Dictionary<uint, uint> InDegreeCentrality(Network network, ILayerOneMode layerOneMode)
        {
            Dictionary<uint, uint> inDegreeCentrality = [];
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                inDegreeCentrality[nodeId] = layerOneMode.GetInDegree(nodeId);
            return inDegreeCentrality;
        }

        /// <summary>
        /// Calculates the outdegree centrality for a specific network and specific 1-mode layer.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layerOneMode">The 1-mode layer object.</param>
        /// <returns>Returns a dictionary with outdegree centrality measures by node id.</returns>
        private static Dictionary<uint, uint> OutDegreeCentrality(Network network, ILayerOneMode layerOneMode)
        {
            Dictionary<uint, uint> outDegreeCentrality = [];
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                outDegreeCentrality[nodeId] = layerOneMode.GetOutDegree(nodeId);
            return outDegreeCentrality;
        }

        /// <summary>
        /// Identifies connected components in a network and assigns a unique identifier to each component.
        /// Uses <see cref="GraphAlgorithms.BFS"/> (the same hyperedge-aware traversal diameter()/
        /// eccentricity() use) for each unvisited seed node, rather than this method's own loop
        /// calling <see cref="ILayer.GetNodeAlters"/> directly: GetNodeAlters re-scans a 2-mode
        /// node's hyperedges from scratch on every call with no cross-call memoization, so calling
        /// it once per node (as a hand-rolled BFS naturally would) costs O(sum of hyperedge sizes
        /// squared) in aggregate; GraphAlgorithms.BFS instead gates each hyperedge behind a
        /// per-traversal visited set, so it is expanded at most once across the whole BFS,
        /// genuinely O(sum of hyperedge sizes). Same component assignment either way.
        /// </summary>
        /// <param name="network">The network containing the nodes to analyze.</param>
        /// <param name="layer">The layer used to retrieve neighboring nodes for traversal.</param>
        /// <returns>A dictionary mapping each node ID to its corresponding component ID.</returns>
        internal static Dictionary<uint, int> ConnectedComponents(Network network, ILayer layer)
        {
            Dictionary<uint, int> componentIds = [];
            int currentComponentId = 0;
            HashSet<uint> visited = [];

            GraphAlgorithms.SplitLayers([layer], out var oneModes, out var twoModesDynamic, out var twoModesStatic);

            foreach (uint nodeId in network.Nodeset.NodeIdArray)
            {
                if (visited.Contains(nodeId)) continue;
                var dist = GraphAlgorithms.BFS(nodeId, oneModes, twoModesDynamic, twoModesStatic, EdgeTraversal.Both);
                foreach (uint reached in dist.Keys)
                {
                    visited.Add(reached);
                    componentIds[reached] = currentComponentId;
                }
                currentComponentId++;
            }
            return componentIds;
        }

        internal static Dictionary<string, object>? GetRandomEdge(ILayer layer, uint[] nodeIds, int maxAttempts)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                uint node1 = nodeIds[Misc.Random.Next(nodeIds.Length)];
                uint node2 = nodeIds[Misc.Random.Next(nodeIds.Length)];
                // If nodes are the same, i.e. a selftie, and this is not a 1-mode layer that allows selfties, then continue:
                if (node1 == node2 && !(layer is ILayerOneMode oneModeLayer && oneModeLayer.Selfties))
                    continue;
                float value = layer.GetEdgeValue(node1, node2);
                if (value > 0)
                    return new Dictionary<string, object>
                    {
                        ["node1"] = node1,
                        ["node2"] = node2,
                        ["value"] = value
                    };
            }
            return null;
        }

        internal static Dictionary<string, object>? GetRandomEdgeSweepOneMode(ILayerOneMode layerOneMode)
        {
            if (layerOneMode.NbrEdges == 0)
                return null;
            int totalEdges = (int)Math.Min(layerOneMode.NbrEdges, int.MaxValue);
            int randomIndex = Misc.Random.Next(totalEdges);
            var edges = layerOneMode.GetAllEdges(randomIndex, 1);
            return edges.Count > 0 ? edges[0] : null;
        }

        internal static Dictionary<string, object>? GetRandomEdgeWeightedTwoMode(ILayerTwoMode layerTwoMode)
        {
            string[] allNames = layerTwoMode.GetAllHyperedgeNames(0, (int)Math.Min(layerTwoMode.NbrHyperedges, int.MaxValue));
            List<string> validNames = [];
            List<long> weights = [];
            long totalWeight = 0;
            foreach (string name in allNames)
            {
                int k = layerTwoMode.GetHyperedgeNodeIds(name).Length;
                if (k >= 2)
                {
                    long weight = (long)k * (k - 1) / 2;
                    validNames.Add(name);
                    weights.Add(weight);
                    totalWeight += weight;
                }
            }
            if (totalWeight == 0)
                return null;
            long randomWeight = Misc.Random.NextInt64(totalWeight);
            int selectedIndex = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (randomWeight < weights[i])
                {
                    selectedIndex = i;
                    break;
                }
                randomWeight -= weights[i];
            }
            uint[] nodeIds = layerTwoMode.GetHyperedgeNodeIds(validNames[selectedIndex]);
            // Return null in case this hyperedge only has one connection
            if (nodeIds.Length < 2)
                return null;
            int idx1 = Misc.Random.Next(nodeIds.Length);
            int idx2;
            do
            {
                idx2 = Misc.Random.Next(nodeIds.Length);
            } while (idx2 == idx1);
            return new Dictionary<string, object>
            {
                ["node1"] = nodeIds[idx1],
                ["node2"] = nodeIds[idx2],
                ["value"] = layerTwoMode.GetEdgeValue(nodeIds[idx1], nodeIds[idx2])
            };
        }
        #endregion
    }
}