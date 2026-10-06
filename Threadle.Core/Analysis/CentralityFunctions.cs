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
        /// Runs AccumulateBFSCentralities for every source node in parallel (up to 'maxthreads',
        /// see 'setting()'), then merges the per-source contributions into shared accumulators.
        /// Each thread accumulates into its own local dictionaries (thread-local partial sums, one
        /// BFS per source with no locking on the hot path); the partial sums are merged under a
        /// single lock once per thread's batch, not once per source. Used by BetweennessCentrality,
        /// ClosenessCentrality, and HarmonicCentrality, which all share this same BFS pass.
        /// </summary>
        internal static (Dictionary<uint, double> Betweenness, Dictionary<uint, double> ClosenessDistSum, Dictionary<uint, int> ClosenessReachable, Dictionary<uint, double> Harmonic)
            AccumulateBFSCentralitiesForSources(uint[] sources, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
        {
            var betweenness = new Dictionary<uint, double>();
            var closenessDistSum = new Dictionary<uint, double>();
            var closenessReachable = new Dictionary<uint, int>();
            var harmonic = new Dictionary<uint, double>();
            var mergeLock = new object();

            System.Threading.Tasks.Parallel.For(0, sources.Length, UserSettings.GetParallelOptions(),
                () => (
                    Betweenness: new Dictionary<uint, double>(),
                    ClosenessDistSum: new Dictionary<uint, double>(),
                    ClosenessReachable: new Dictionary<uint, int>(),
                    Harmonic: new Dictionary<uint, double>()
                ),
                (i, loopState, local) =>
                {
                    AccumulateBFSCentralities(sources[i], oneModes, twoModesDynamic, twoModesStatic, traversal,
                        local.Betweenness, local.ClosenessDistSum, local.ClosenessReachable, local.Harmonic);
                    return local;
                },
                local =>
                {
                    lock (mergeLock)
                    {
                        foreach (var (node, value) in local.Betweenness)
                            betweenness[node] = betweenness.GetValueOrDefault(node) + value;
                        foreach (var (node, value) in local.ClosenessDistSum)
                            closenessDistSum[node] = closenessDistSum.GetValueOrDefault(node) + value;
                        foreach (var (node, value) in local.ClosenessReachable)
                            closenessReachable[node] = closenessReachable.GetValueOrDefault(node) + value;
                        foreach (var (node, value) in local.Harmonic)
                            harmonic[node] = harmonic.GetValueOrDefault(node) + value;
                    }
                });

            return (betweenness, closenessDistSum, closenessReachable, harmonic);
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
        /// Single-source BFSBrandes pass that accumulates contributions to edge betweenness for a
        /// single 1-mode layer. Each predecessor edge (v,w) on a shortest-path DAG is credited with
        /// the exact same per-predecessor contribution term that AccumulateBFSCentralities folds
        /// into node w's cumulative delta, so this reuses the identical Brandes backward pass —
        /// it just also credits the edge, not only the node. For undirected layers, the edge key is
        /// canonicalized (lower node id first) so both discovery directions accumulate into the same
        /// entry, representing one undirected edge; for directed layers, (v,w) is kept as-is, since
        /// arcs v→w and w→v (if both exist) are distinct ties with independent scores.
        /// </summary>
        internal static void AccumulateEdgeBetweenness(uint source, ILayerOneMode layer, EdgeTraversal traversal, Dictionary<(uint From, uint To), double> edgeBetweenness)
        {
            GraphAlgorithms.BFSBrandes(source, [layer], [], [], traversal,
                out var dist, out var sigma, out var pred, out var order);

            bool directed = layer.IsDirectional;
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

                    var edgeKey = (directed || v < w) ? (v, w) : (w, v);
                    edgeBetweenness[edgeKey] = edgeBetweenness.GetValueOrDefault(edgeKey) + contrib;
                }
            }
        }

        /// <summary>
        /// Runs AccumulateEdgeBetweenness for every source node in parallel (up to 'maxthreads', see
        /// 'setting()'), merging thread-local partial sums into a shared accumulator under a lock
        /// after each thread's batch — same accumulate+merge pattern as
        /// AccumulateBFSCentralitiesForSources.
        /// </summary>
        internal static Dictionary<(uint From, uint To), double> AccumulateEdgeBetweennessForSources(uint[] sources, ILayerOneMode layer, EdgeTraversal traversal)
        {
            var edgeBetweenness = new Dictionary<(uint From, uint To), double>();
            var mergeLock = new object();

            System.Threading.Tasks.Parallel.For(0, sources.Length, UserSettings.GetParallelOptions(),
                () => new Dictionary<(uint From, uint To), double>(),
                (i, loopState, local) =>
                {
                    AccumulateEdgeBetweenness(sources[i], layer, traversal, local);
                    return local;
                },
                local =>
                {
                    lock (mergeLock)
                    {
                        foreach (var (edge, value) in local)
                            edgeBetweenness[edge] = edgeBetweenness.GetValueOrDefault(edge) + value;
                    }
                });

            return edgeBetweenness;
        }

        /// <summary>
        /// Normalizes raw edge betweenness scores, using the same bound as node betweenness: divide
        /// by (n-1)(n-2) for directed layers, (n-1)(n-2)/2 for undirected. When sampled
        /// (totalSources &lt; n): scale up by n/totalSources.
        /// </summary>
        internal static Dictionary<(uint From, uint To), double> FinalizeEdgeBetweenness(Dictionary<(uint From, uint To), double> raw, int n, int totalSources, bool directed)
        {
            double norm = directed
                ? (n - 1.0) * (n - 2.0)
                : (n - 1.0) * (n - 2.0) / 2.0;
            double scale = (totalSources > 0 && totalSources < n) ? (double)n / totalSources : 1.0;

            var result = new Dictionary<(uint From, uint To), double>(raw.Count);
            foreach (var (edge, value) in raw)
                result[edge] = norm > 0 ? value * scale / norm : 0.0;
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
        internal static Dictionary<uint, double> EigenvectorCentrality(
            uint[] nodeIds,
            List<ILayerOneMode> oneModes,
            List<LayerTwoMode> twoModesDynamic,
            List<LayerTwoModeStatic> twoModesStatic,
            EdgeTraversal traversal,
            int maxIterations = 100,
            double tolerance = 1e-8)
        {
            int n = nodeIds.Length;
            if (n == 0) return [];

            // Build nodeId → array-index map (used only during setup, not inside the hot loop)
            var idx = new Dictionary<uint, int>(n);
            for (int i = 0; i < n; i++) idx[nodeIds[i]] = i;

            // Pre-compute 1-mode adjacency as (neighborIdx, weight)[] per node.
            // Done once so the iteration loop has zero dictionary lookups.
            var adj = new (int vi, double w)[n][];
            for (int ui = 0; ui < n; ui++)
            {
                uint u = nodeIds[ui];
                var edges = new List<(int, double)>();
                foreach (var layer in oneModes)
                {
                    var (alters, weights) = layer.GetNodeAltersWithWeights(u, traversal);
                    bool hasWeights = weights.Length > 0;
                    for (int i = 0; i < alters.Length; i++)
                        if (idx.TryGetValue(alters.Span[i], out int vi))
                            edges.Add((vi, hasWeights ? weights.Span[i] : 1.0));
                }
                adj[ui] = [.. edges];
            }

            // Pre-compute 2-mode dynamic
            var dynHeMembers = new List<int[]>();
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
                        var members = new List<int>();
                        foreach (uint m in he.NodeIds)
                            if (idx.TryGetValue(m, out int mi)) members.Add(mi);
                        if (members.Count > 1) dynHeMembers.Add([.. members]);
                    }
                }
            }

            var statHeMembers = new List<int[]>();
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
                        var members = new List<int>();
                        for (int j = hStart; j < hEnd; j++)
                            if (idx.TryGetValue(layer.GetHyperedgeNodeAt(j), out int mi)) members.Add(mi);
                        if (members.Count > 1) statHeMembers.Add([.. members]);
                    }
                }
            }



            // Power iteration — all hot-path operations are array accesses (~1 ns each)
            double[] x = new double[n];
            double[] xNew = new double[n];
            for (int i = 0; i < n; i++) x[i] = 1.0 / n;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                Array.Clear(xNew, 0, n);

                // 1-mode: pure array access, no dictionary lookups
                for (int ui = 0; ui < n; ui++)
                {
                    double xu = x[ui];
                    foreach (var (vi, w) in adj[ui])
                        xNew[vi] += w * xu;
                }

                // 2-mode dynamic — pure array access now
                foreach (int[] members in dynHeMembers)
                {
                    double heSum = 0;
                    foreach (int mi in members) heSum += x[mi];
                    foreach (int mi in members) xNew[mi] += heSum - x[mi];
                }

                // 2-mode static — pure array access now
                foreach (int[] members in statHeMembers)
                {
                    double heSum = 0;
                    foreach (int mi in members) heSum += x[mi];
                    foreach (int mi in members) xNew[mi] += heSum - x[mi];
                }

                //// 2-mode dynamic: per-hyperedge sum (hyperedges can't be pre-flattened cheaply)
                //foreach (var layer in twoModesDynamic)
                //{
                //    var processed = new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance);
                //    foreach (uint u in nodeIds)
                //    {
                //        var hec = layer.GetNonEmptyHyperedgeCollection(u);
                //        if (hec == null) continue;
                //        foreach (var he in hec.HyperEdges)
                //        {
                //            if (!processed.Add(he)) continue;
                //            double heSum = 0;
                //            foreach (uint m in he.NodeIds)
                //                if (idx.TryGetValue(m, out int mi)) heSum += x[mi];
                //            foreach (uint m in he.NodeIds)
                //                if (idx.TryGetValue(m, out int mi)) xNew[mi] += heSum - x[mi];
                //        }
                //    }
                //}

                //// 2-mode static: per-hyperedge sum
                //foreach (var layer in twoModesStatic)
                //{
                //    var processed = new HashSet<int>();
                //    foreach (uint u in nodeIds)
                //    {
                //        if (!layer.TryGetNodeHyperedgeRange(u, out int nStart, out int nEnd)) continue;
                //        for (int k = nStart; k < nEnd; k++)
                //        {
                //            int hIdx = layer.GetNodeHyperedgeIndex(k);
                //            if (!processed.Add(hIdx)) continue;
                //            layer.GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
                //            double heSum = 0;
                //            for (int j = hStart; j < hEnd; j++)
                //                if (idx.TryGetValue(layer.GetHyperedgeNodeAt(j), out int mi)) heSum += x[mi];
                //            for (int j = hStart; j < hEnd; j++)
                //                if (idx.TryGetValue(layer.GetHyperedgeNodeAt(j), out int mi)) xNew[mi] += heSum - x[mi];
                //        }
                //    }
                //}

                // L2 normalize
                double l2 = 0;
                for (int i = 0; i < n; i++) l2 += xNew[i] * xNew[i];
                l2 = Math.Sqrt(l2);
                if (l2 < 1e-15) break;
                for (int i = 0; i < n; i++) xNew[i] /= l2;

                // Convergence check, then swap buffers (no allocation)
                double maxDiff = 0;
                for (int i = 0; i < n; i++) maxDiff = Math.Max(maxDiff, Math.Abs(xNew[i] - x[i]));
                (x, xNew) = (xNew, x);
                if (maxDiff < tolerance) break;
            }

            var result = new Dictionary<uint, double>(n);
            for (int i = 0; i < n; i++) result[nodeIds[i]] = x[i];
            return result;
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