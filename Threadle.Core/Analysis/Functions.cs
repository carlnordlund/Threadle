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
        internal static Dictionary<uint, uint> DegreeCentrality(Network network, LayerOneMode layerOneMode, EdgeTraversal edgeTraversal)
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
        internal static Dictionary<uint, uint> DegreeCentrality(Network network, LayerTwoMode layerTwoMode)
        {
            Dictionary<uint, uint> degreeCentrality = [];
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                degreeCentrality[nodeId] = (uint)layerTwoMode.GetNodeAlters(nodeId).Length;
            return degreeCentrality;
        }

        /// <summary>
        /// Calculates the density of a specific 1-mode layer in a specific network.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layer">The 1-mode layer object.</param>
        /// <returns>Density (as a double value).</returns>
        internal static double Density(Network network, LayerOneMode layer)
        {
            ulong nbrPotentialEdges = Misc.GetNbrPotentialEdges((ulong)network.Nodeset.Count, layer.Directionality, layer.Selfties);
            ulong nbrExistingEdges = layer.NbrEdges;
            return (double)nbrExistingEdges / nbrPotentialEdges;
        }

        /// <summary>
        /// Calculates the density of a specific 2-mode layer in a specific network.
        /// Does this by obtaining the number of node alters for each node.
        /// Can take time for very large networks.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layer">The 2-mode layer object.</param>
        /// <returns>Density (as a double value)</returns>
        internal static double Density(Network network, LayerTwoMode layer)
        {
            int nbrNodes = network.Nodeset.Count;
            ulong nbrPotentialEdges = (ulong)(nbrNodes * (nbrNodes - 1));

            ulong nbrExistingEdges = 0;
            foreach (uint nodeId in network.Nodeset.NodeIdArray)
                nbrExistingEdges += (ulong)layer.GetNodeAlters(nodeId).Length;
            return (double)nbrExistingEdges / nbrPotentialEdges;
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
                if (attrValue != null && attrValue.Value.Type == NodeAttributeType.Char)
                {
                    char charValue = (char)attrValue.Value.GetValue()!;
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
                //stats["Mode"] = null;
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
                if (attrValue != null && attrValue.Value.Type == NodeAttributeType.Bool)
                {
                    if ((bool)attrValue.Value.GetValue()!)
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
                if (attrValue != null && attrValue.Value.Type == NodeAttributeType.Float)
                    values.Add((float)attrValue.Value.GetValue()!);
            }
            countWithValue = values.Count;
            Dictionary<string, object> stats = [];

            if (values.Count == 0)
                return stats;

            double mean = values.Average();
            double variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
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
                if (attrValue != null && attrValue.Value.Type == NodeAttributeType.Int)
                    values.Add((int)attrValue.Value.GetValue()!);
            }
            countWithValues = values.Count;
            Dictionary<string, object> stats = [];
            if (values.Count == 0)
                return stats;

            double mean = values.Average();
            double variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
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
        #endregion


        #region Methods (private)
        /// <summary>
        /// Calculates the gross degree centrality for a specific network and specific 1-mode layer.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layerOneMode">The 1-mode layer object.</param>
        /// <returns>Returns a dictionary with gross degree centrality measures by node id.</returns>
        private static Dictionary<uint, uint> GrossDegreeCentrality(Network network, LayerOneMode layerOneMode)
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
        private static Dictionary<uint, uint> InDegreeCentrality(Network network, LayerOneMode layerOneMode)
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
        private static Dictionary<uint, uint> OutDegreeCentrality(Network network, LayerOneMode layerOneMode)
        {
            Dictionary<uint, uint> outDegreeCentrality = [];
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                outDegreeCentrality[nodeId] = layerOneMode.GetOutDegree(nodeId);
            return outDegreeCentrality;
        }

        internal static Dictionary<uint, int> ConnectedComponents(Network network, ILayer layer)
        {
            Dictionary<uint, int> componentIds = [];
            int currentComponentId = 0;
            HashSet<uint> visited = [];

            foreach (uint nodeId in network.Nodeset.NodeIdArray)
            {
                if (!visited.Contains(nodeId))
                {
                    Queue<uint> queue = [];
                    queue.Enqueue(nodeId);
                    visited.Add(nodeId);
                    while (queue.Count > 0)
                    {
                        uint current = queue.Dequeue();
                        componentIds[current] = currentComponentId;
                        foreach (uint neighborId in layer.GetNodeAlters(current, EdgeTraversal.Both))
                        {
                            if (!visited.Contains(neighborId))
                            {
                                visited.Add(neighborId);
                                queue.Enqueue(neighborId);
                            }
                        }
                    }
                    currentComponentId++;
                }
            }
            return componentIds;
        }

        internal static Dictionary<string, object>? GetRandomEdge(ILayer layer, uint[] nodeIds, int maxAttempts)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                uint node1 = nodeIds[Misc.Random.Next(nodeIds.Length)];
                uint node2 = nodeIds[Misc.Random.Next(nodeIds.Length)];
                if (node1 == node2)
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

        internal static Dictionary<string, object>? GetRandomEdgeSweepOneMode(LayerOneMode layerOneMode)
        {
            int totalEdges = (int)layerOneMode.NbrEdges;
            int randomIndex = Misc.Random.Next(totalEdges);
            foreach (var kvp in layerOneMode.Edgesets)
            {
                if (randomIndex < kvp.Value.NbrEdges)
                {
                    uint node1 = kvp.Key;
                    uint node2 = kvp.Value.GetOutboundNodeIds.ElementAt(randomIndex);
                    float value = layerOneMode.GetEdgeValue(node1, node2);
                    return new Dictionary<string, object>
                    {
                        ["node1"] = node1,
                        ["node2"] = node2,
                        ["value"] = value
                    };
                }
                randomIndex -= (int)kvp.Value.NbrEdges;
            }
            return null;
        }

        internal static Dictionary<string, object>? GetRandomEdgeWeightedTwoMode(LayerTwoMode layerTwoMode)
        {
            var validHyperedges = layerTwoMode.AllHyperEdges.Values.Where(h => h.NbrNodes >= 2).ToList();
            if (validHyperedges.Count == 0)
                return null;
            List<int> weights = new(validHyperedges.Count);
            Int64 totalWeight = 0;
            foreach (var hyperedge in validHyperedges)
            {
                int k = hyperedge.NbrNodes;
                int weight = k * (k - 1) / 2;
                weights.Add(weight);
                totalWeight += weight;
            }

            if (totalWeight == 0)
                return null;
            Int64 randomWeight = Misc.Random.NextInt64(totalWeight);

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

            Hyperedge selectedHyperedge = validHyperedges[selectedIndex];
            var Nodeids = selectedHyperedge.NodeIds.ToArray();
            int idx1 = Misc.Random.Next(Nodeids.Length);
            int idx2;
            do
            {
                idx2 = Misc.Random.Next(Nodeids.Length);
            } while (idx2 == idx1);
            return new Dictionary<string, object>
            {
                ["node1"] = Nodeids[idx1],
                ["node2"] = Nodeids[idx2],
                ["value"] = layerTwoMode.GetEdgeValue(Nodeids[idx1], Nodeids[idx2])
            };
        }
        #endregion
    }
}
