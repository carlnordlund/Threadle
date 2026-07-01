using System.Globalization;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Analysis
{
    internal static class CentralityFunctions
    {
        #region Methods (internal)

        /// <summary>
        /// Fisher-Yates partial shuffle to pick sampleSize nodes without replacement.
        /// Returns the full array unchanged if sampleSize <= 0 or >= nodeIds.Length.
        /// </summary>
        internal static uint[] SampleNodes(uint[] nodeIds, int sampleSize)
        {
            if (sampleSize <= 0 || sampleSize >= nodeIds.Length)
                return nodeIds;
            uint[] pool = (uint[])nodeIds.Clone();
            for (int i = 0; i < sampleSize; i++)
            {
                int j = Misc.Random.Next(i, pool.Length);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            return pool[..sampleSize];
        }

        /// <summary>
        /// Single-source BFSBrandes pass that accumulates contributions to betweenness,
        /// closeness, and harmonic centrality in-place. All three are accumulated together
        /// so callers pay only one BFS per source regardless of which metrics they need.
        /// </summary>
        internal static void AccumulateBFSCentralities(uint source, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal, Dictionary<uint, double> betweenness, Dictionary<uint, double> closenessDistSum, Dictionary<uint, int> closenessReachable, Dictionary<uint, double> harmonic)
        {
            GraphAlgorithms.BFSBrandes(source, oneModes, twoModesDynamic, twoModesStatic, traversal,
                out var dist, out var sigma, out var pred, out var order);

            // Closeness + harmonic: accumulate from distances produced by this source's BFS
            foreach (var (v, d) in dist)
            {
                if (v == source) continue;
                closenessDistSum[source] = closenessDistSum.GetValueOrDefault(source) + d;
                closenessReachable[source] = closenessReachable.GetValueOrDefault(source) + 1;
                harmonic[source] = harmonic.GetValueOrDefault(source) + 1.0 / d;
            }

            // Brandes back-propagation for betweenness
            var delta = new Dictionary<uint, double>(order.Count);
            while (order.Count > 0)
            {
                uint w = order.Pop();
                if (!pred.TryGetValue(w, out var preds)) continue;
                double sigmaw = (double)sigma.GetValueOrDefault(w, 1L);
                foreach (uint v in preds)
                {
                    double sigmav = (double)sigma.GetValueOrDefault(v, 1L);
                    double contrib = (sigmav / sigmaw) * (1.0 + delta.GetValueOrDefault(w));
                    delta[v] = delta.GetValueOrDefault(v) + contrib;
                }
                if (w != source)
                    betweenness[w] = betweenness.GetValueOrDefault(w) + delta.GetValueOrDefault(w);
            }
        }

        /// <summary>
        /// Normalizes raw betweenness scores.
        /// Directed: divide by (n-1)(n-2). Undirected: divide by (n-1)(n-2)/2.
        /// When sampled (totalSources < n): scale up by n/totalSources.
        /// </summary>
        internal static Dictionary<uint, double> FinalizeBetweenness(Dictionary<uint, double> raw, uint[] allNodeIds, int totalSources, bool directed)
        {
            int n = allNodeIds.Length;
            double norm = directed
                ? (n - 1.0) * (n - 2.0)
                : (n - 1.0) * (n - 2.0) / 2.0;
            double scale = (totalSources > 0 && totalSources < n) ? (double)n / totalSources : 1.0;

            var result = new Dictionary<uint, double>(n);
            foreach (uint v in allNodeIds)
                result[v] = norm > 0 ? raw.GetValueOrDefault(v) * scale / norm : 0.0;
            return result;
        }

        /// <summary>
        /// Wasserman-Faust closeness: ((reachable)^2) / ((n-1) * distSum).
        /// Handles disconnected components gracefully (unreachable nodes get 0).
        /// </summary>
        internal static Dictionary<uint, double> FinalizeCloseness(Dictionary<uint, double> distSums, Dictionary<uint, int> reachable, uint[] allNodeIds)
        {
            int n = allNodeIds.Length;
            var result = new Dictionary<uint, double>(n);
            foreach (uint v in allNodeIds)
            {
                double dSum = distSums.GetValueOrDefault(v);
                int reach = reachable.GetValueOrDefault(v);
                result[v] = (dSum > 0 && reach > 0)
                    ? ((double)reach * reach) / ((n - 1.0) * dSum)
                    : 0.0;
            }
            return result;
        }

        /// <summary>
        /// Finalizes harmonic centrality, optionally normalizing by 1/(n-1).
        /// </summary>
        internal static Dictionary<uint, double> FinalizeHarmonic(Dictionary<uint, double> raw, uint[] allNodeIds, bool normalize)
        {
            int n = allNodeIds.Length;
            double norm = (normalize && n > 1) ? 1.0 / (n - 1) : 1.0;
            var result = new Dictionary<uint, double>(n);
            foreach (uint v in allNodeIds)
                result[v] = raw.GetValueOrDefault(v) * norm;
            return result;
        }

        /// <summary>
        /// Power iteration for eigenvector centrality over a multilayer network.
        /// 2-mode layers: per-hyperedge summation prevents O(k^2) expansion —
        /// heSum = Σ x[m] over all members, then each member m receives heSum - x[m].
        /// </summary>
        internal static Dictionary<uint, double> EigenvectorCentrality(uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal, int maxIterations = 100, double tolerance = 1e-8)
        {
            int n = nodeIds.Length;
            if (n == 0) return [];

            var x = new Dictionary<uint, double>(n);
            foreach (uint v in nodeIds) x[v] = 1.0 / n;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                var xNew = new Dictionary<uint, double>(n);
                foreach (uint v in nodeIds) xNew[v] = 0.0;

                // 1-mode contribution
                // 1-mode layers
                foreach (uint u in nodeIds)
                {
                    if (traversal == EdgeTraversal.Both)
                    {
                        // Deduplicate: mutual ties must not be counted twice
                        var unique = new HashSet<uint>();
                        foreach (var layer in oneModes)
                        {
                            foreach (uint v in layer.GetNodeAlters(u, EdgeTraversal.Out)) unique.Add(v);
                            foreach (uint v in layer.GetNodeAlters(u, EdgeTraversal.In)) unique.Add(v);
                        }
                        foreach (uint v in unique)
                            if (xNew.ContainsKey(v)) xNew[v] += x[u];
                    }
                    else
                    {
                        foreach (var layer in oneModes)
                            foreach (uint v in layer.GetNodeAlters(u, traversal))
                                if (xNew.ContainsKey(v)) xNew[v] += x[u];
                    }
                }
                // 2-mode dynamic: process each hyperedge once via per-hyperedge sum
                foreach (var layer in twoModesDynamic)
                {
                    var processed = new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance);
                    foreach (uint u in nodeIds)
                    {
                        var hec = layer.GetNonEmptyHyperedgeCollection(u);
                        if (hec == null) continue;
                        foreach (var he in hec.HyperEdges)
                        {
                            if (!processed.Add(he)) continue;
                            double heSum = 0;
                            foreach (uint m in he.NodeIds) heSum += x.GetValueOrDefault(m);
                            foreach (uint m in he.NodeIds)
                                if (xNew.ContainsKey(m)) xNew[m] += heSum - x.GetValueOrDefault(m);
                        }
                    }
                }

                // 2-mode static: process each CSR hyperedge once via per-hyperedge sum
                foreach (var layer in twoModesStatic)
                {
                    var processed = new HashSet<int>();
                    foreach (uint u in nodeIds)
                    {
                        if (!layer.TryGetNodeHyperedgeRange(u, out int nStart, out int nEnd)) continue;
                        for (int k = nStart; k < nEnd; k++)
                        {
                            int hIdx = layer.GetNodeHyperedgeIndex(k);
                            if (!processed.Add(hIdx)) continue;
                            layer.GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
                            double heSum = 0;
                            for (int j = hStart; j < hEnd; j++) heSum += x.GetValueOrDefault(layer.GetHyperedgeNodeAt(j));
                            for (int j = hStart; j < hEnd; j++)
                            {
                                uint m = layer.GetHyperedgeNodeAt(j);
                                if (xNew.ContainsKey(m)) xNew[m] += heSum - x.GetValueOrDefault(m);
                            }
                        }
                    }
                }

                // L2 normalize
                double l2 = 0;
                foreach (double v in xNew.Values) l2 += v * v;
                l2 = Math.Sqrt(l2);
                if (l2 < 1e-15) break;
                foreach (uint v in nodeIds) xNew[v] /= l2;

                // Convergence check
                double maxDiff = 0;
                foreach (uint v in nodeIds) maxDiff = Math.Max(maxDiff, Math.Abs(xNew[v] - x[v]));
                x = xNew;
                if (maxDiff < tolerance) break;
            }
            return x;
        }

        /// <summary>
        /// PageRank with teleportation. Precomputes unique projected out-neighbor lists once
        /// (co-members deduplicated across hyperedges) then iterates until convergence.
        /// Dangling nodes (no out-edges) distribute their mass uniformly to all nodes.
        /// </summary>
        internal static Dictionary<uint, double> PageRank(uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, double dampingFactor = 0.85, int maxIterations = 100, double tolerance = 1e-8)
        {
            int n = nodeIds.Length;
            if (n == 0) return [];
            var nodeSet = new HashSet<uint>(nodeIds);

            // Precompute unique out-neighbor lists (projected adjacency, deduplicated)
            var outNeighbors = new Dictionary<uint, uint[]>(n);
            foreach (uint u in nodeIds)
            {
                var neighbors = new HashSet<uint>();
                foreach (var layer in oneModes)
                    foreach (uint v in layer.GetNodeAlters(u, EdgeTraversal.Out))
                        if (nodeSet.Contains(v)) neighbors.Add(v);
                foreach (var layer in twoModesDynamic)
                {
                    var hec = layer.GetNonEmptyHyperedgeCollection(u);
                    if (hec == null) continue;
                    foreach (var he in hec.HyperEdges)
                        foreach (uint m in he.NodeIds)
                            if (m != u && nodeSet.Contains(m)) neighbors.Add(m);
                }
                foreach (var layer in twoModesStatic)
                {
                    if (!layer.TryGetNodeHyperedgeRange(u, out int nStart, out int nEnd)) continue;
                    for (int k = nStart; k < nEnd; k++)
                    {
                        int hIdx = layer.GetNodeHyperedgeIndex(k);
                        layer.GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
                        for (int j = hStart; j < hEnd; j++)
                        {
                            uint m = layer.GetHyperedgeNodeAt(j);
                            if (m != u && nodeSet.Contains(m)) neighbors.Add(m);
                        }
                    }
                }
                outNeighbors[u] = [.. neighbors];
            }

            var pr = new Dictionary<uint, double>(n);
            foreach (uint v in nodeIds) pr[v] = 1.0 / n;
            double teleport = (1.0 - dampingFactor) / n;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                var prNew = new Dictionary<uint, double>(n);
                foreach (uint v in nodeIds) prNew[v] = teleport;
                double danglingSum = 0;

                foreach (uint u in nodeIds)
                {
                    uint[] outs = outNeighbors[u];
                    if (outs.Length == 0) { danglingSum += pr[u]; continue; }
                    double share = dampingFactor * pr[u] / outs.Length;
                    foreach (uint v in outs) prNew[v] += share;
                }

                double danglingContrib = dampingFactor * danglingSum / n;
                foreach (uint v in nodeIds) prNew[v] += danglingContrib;

                double maxDiff = 0;
                foreach (uint v in nodeIds) maxDiff = Math.Max(maxDiff, Math.Abs(prNew[v] - pr[v]));
                pr = prNew;
                if (maxDiff < tolerance) break;
            }
            return pr;
        }

        #endregion
    }
}