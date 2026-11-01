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
        /// <paramref name="scratch"/> is reused across every source a thread handles (see
        /// <see cref="GraphAlgorithms.BFSBrandesScratch"/>) rather than allocated fresh per call.
        /// There is no stored predecessor list: for each node <c>w</c> processed in reverse BFS
        /// order, predecessors are re-derived by re-scanning <c>w</c>'s neighbors under the
        /// reverse traversal direction (1-mode layers) or re-scanning every hyperedge <c>w</c>
        /// belongs to (2-mode layers), keeping any candidate <c>v</c> with
        /// <c>Dist[v] == Dist[w] - 1</c>. Re-scanning each shared hyperedge in full (rather than
        /// deduping across hyperedges) reproduces the exact multiplicity the old predecessor list
        /// had: two nodes sharing two hyperedges get two separate predecessor credits.
        /// </summary>
        internal static void AccumulateBFSCentralities(uint source, Dictionary<uint, int> nodeIndex, uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal, Dictionary<uint, double> betweenness, Dictionary<uint, double> closenessDistSum, Dictionary<uint, int> closenessReachable, Dictionary<uint, double> harmonic, GraphAlgorithms.BFSBrandesScratch scratch)
        {
            GraphAlgorithms.BFSBrandes(source, nodeIndex, nodeIds, oneModes, twoModesDynamic, twoModesStatic, traversal, scratch);
            int[] dist = scratch.Dist;
            long[] sigma = scratch.Sigma;
            int[] stamp = scratch.Stamp;
            int myStamp = scratch.CurrentStamp;
            int[] order = scratch.Order;
            int orderCount = scratch.OrderCount;
            double[] delta = scratch.Delta;
            int sourceIdx = nodeIndex[source];

            // Closeness + harmonic: accumulate from distances produced by this source's BFS
            for (int i = 0; i < orderCount; i++)
            {
                int vIdx = order[i];
                if (vIdx == sourceIdx) continue;
                int d = dist[vIdx];
                closenessDistSum[source] = closenessDistSum.GetValueOrDefault(source) + d;
                closenessReachable[source] = closenessReachable.GetValueOrDefault(source) + 1;
                harmonic[source] = harmonic.GetValueOrDefault(source) + 1.0 / d;
            }

            // Reset delta for reached nodes only (O(reached), not O(n))
            for (int i = 0; i < orderCount; i++) delta[order[i]] = 0.0;

            // Brandes back-propagation for betweenness, re-deriving predecessors as we go
            int currentWIdx = 0;
            int targetDist = 0;
            double sigmaw = 0;
            void Consider(uint v)
            {
                if (nodeIndex.TryGetValue(v, out int vIdx) && stamp[vIdx] == myStamp && dist[vIdx] == targetDist)
                    delta[vIdx] += ((double)sigma[vIdx] / sigmaw) * (1.0 + delta[currentWIdx]);
            }
            Action<uint> consider = Consider;

            for (int i = orderCount - 1; i >= 0; i--)
            {
                int wIdx = order[i];
                uint w = nodeIds[wIdx];
                currentWIdx = wIdx;
                targetDist = dist[wIdx] - 1;
                sigmaw = sigma[wIdx];

                foreach (var layer in oneModes)
                {
                    // Both-on-directed needs GetNodeAlters' own dedup, same as the forward pass -
                    // an undeduped double-direction neighbor would double-count its contribution.
                    if (traversal == EdgeTraversal.Both && layer.IsDirectional)
                        foreach (uint v in layer.GetNodeAlters(w, traversal))
                            consider(v);
                    else
                        layer.VisitAlters(w, Reverse(traversal), consider);
                }

                for (int li = 0; li < twoModesDynamic.Count; li++)
                {
                    var hec = twoModesDynamic[li].GetNonEmptyHyperedgeCollection(w);
                    if (hec == null) continue;
                    foreach (var he in hec.HyperEdges)
                        foreach (uint v in he.NodeIds)
                            if (v != w) consider(v);
                }

                for (int li = 0; li < twoModesStatic.Count; li++)
                {
                    if (!twoModesStatic[li].TryGetNodeHyperedgeRange(w, out int nStart, out int nEnd)) continue;
                    for (int k = nStart; k < nEnd; k++)
                    {
                        int hIdx = twoModesStatic[li].GetNodeHyperedgeIndex(k);
                        twoModesStatic[li].GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
                        for (int j = hStart; j < hEnd; j++)
                        {
                            uint v = twoModesStatic[li].GetHyperedgeNodeAt(j);
                            if (v != w) consider(v);
                        }
                    }
                }

                if (w != source)
                    betweenness[w] = betweenness.GetValueOrDefault(w) + delta[wIdx];
            }
        }

        /// <summary>Reverses a traversal direction for the backward pass: predecessor candidates
        /// under forward traversal <paramref name="t"/> are found by walking the opposite
        /// direction from <c>w</c>. <see cref="EdgeTraversal.Both"/> and undirected layers (which
        /// ignore the traversal argument entirely) are unaffected.</summary>
        private static EdgeTraversal Reverse(EdgeTraversal t) => t switch
        {
            EdgeTraversal.Out => EdgeTraversal.In,
            EdgeTraversal.In => EdgeTraversal.Out,
            _ => t
        };

        /// <summary>
        /// Runs AccumulateBFSCentralities for every source node in parallel (up to 'maxthreads',
        /// see 'setting()'), then merges the per-source contributions into shared accumulators.
        /// Each thread accumulates into its own local dictionaries (thread-local partial sums, one
        /// BFS per source with no locking on the hot path); the partial sums are merged under a
        /// single lock once per thread's batch, not once per source. Used by BetweennessCentrality,
        /// ClosenessCentrality, and HarmonicCentrality, which all share this same BFS pass.
        /// </summary>
        internal static (Dictionary<uint, double> Betweenness, Dictionary<uint, double> ClosenessDistSum, Dictionary<uint, int> ClosenessReachable, Dictionary<uint, double> Harmonic)
            AccumulateBFSCentralitiesForSources(uint[] sources, Dictionary<uint, int> nodeIndex, uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
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
                    Harmonic: new Dictionary<uint, double>(),
                    Scratch: new GraphAlgorithms.BFSBrandesScratch()
                ),
                (i, loopState, local) =>
                {
                    AccumulateBFSCentralities(sources[i], nodeIndex, nodeIds, oneModes, twoModesDynamic, twoModesStatic, traversal,
                        local.Betweenness, local.ClosenessDistSum, local.ClosenessReachable, local.Harmonic,
                        local.Scratch);
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
        /// Single-source plain-BFS pass that accumulates contributions to closeness and harmonic
        /// centrality only - the same loop AccumulateBFSCentralities runs over its BFSBrandes
        /// 'dist' output, but here fed by GraphAlgorithms.BFS instead. Closeness/harmonic never
        /// read sigma/predecessors/order (only 'dist'), so this avoids paying for Brandes' sigma
        /// bookkeeping at all: on a 2-mode layer, BFSBrandes must re-scan a hyperedge's member list
        /// once per same-level arriving member to keep sigma correct (worst case O(Σ hyperedge
        /// sizes squared)), where plain BFS expands each hyperedge exactly once (O(Σ hyperedge
        /// sizes)) - the same traversal NetworkLevelFunctions.Diameter/Eccentricity already use for
        /// this same "distances only" shape. Same distances either way, since both BFS variants
        /// compute identical shortest-path levels - only the bookkeeping differs.
        /// </summary>
        internal static void AccumulateDistanceCentralities(uint source, Dictionary<uint, int> nodeIndex, uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal, Dictionary<uint, double> closenessDistSum, Dictionary<uint, int> closenessReachable, Dictionary<uint, double> harmonic)
        {
            var dist = GraphAlgorithms.BFS(source, nodeIndex, nodeIds, oneModes, twoModesDynamic, twoModesStatic, traversal);

            foreach (var (v, d) in dist)
            {
                if (v == source) continue;
                closenessDistSum[source] = closenessDistSum.GetValueOrDefault(source) + d;
                closenessReachable[source] = closenessReachable.GetValueOrDefault(source) + 1;
                harmonic[source] = harmonic.GetValueOrDefault(source) + 1.0 / d;
            }
        }

        /// <summary>
        /// Runs AccumulateDistanceCentralities for every source node in parallel (up to
        /// 'maxthreads', see 'setting()'), then merges the per-source contributions into shared
        /// accumulators - same thread-local-then-merge-under-lock pattern as
        /// AccumulateBFSCentralitiesForSources, just built on the cheaper plain-BFS single-source
        /// function above. Used by ClosenessCentrality and HarmonicCentrality only; betweenness
        /// still needs AccumulateBFSCentralitiesForSources's real Brandes pass.
        /// </summary>
        internal static (Dictionary<uint, double> ClosenessDistSum, Dictionary<uint, int> ClosenessReachable, Dictionary<uint, double> Harmonic)
            AccumulateDistanceCentralitiesForSources(uint[] sources, Dictionary<uint, int> nodeIndex, uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
        {
            var closenessDistSum = new Dictionary<uint, double>();
            var closenessReachable = new Dictionary<uint, int>();
            var harmonic = new Dictionary<uint, double>();
            var mergeLock = new object();

            System.Threading.Tasks.Parallel.For(0, sources.Length, UserSettings.GetParallelOptions(),
                () => (
                    ClosenessDistSum: new Dictionary<uint, double>(),
                    ClosenessReachable: new Dictionary<uint, int>(),
                    Harmonic: new Dictionary<uint, double>()
                ),
                (i, loopState, local) =>
                {
                    AccumulateDistanceCentralities(sources[i], nodeIndex, nodeIds, oneModes, twoModesDynamic, twoModesStatic, traversal,
                        local.ClosenessDistSum, local.ClosenessReachable, local.Harmonic);
                    return local;
                },
                local =>
                {
                    lock (mergeLock)
                    {
                        foreach (var (node, value) in local.ClosenessDistSum)
                            closenessDistSum[node] = closenessDistSum.GetValueOrDefault(node) + value;
                        foreach (var (node, value) in local.ClosenessReachable)
                            closenessReachable[node] = closenessReachable.GetValueOrDefault(node) + value;
                        foreach (var (node, value) in local.Harmonic)
                            harmonic[node] = harmonic.GetValueOrDefault(node) + value;
                    }
                });

            return (closenessDistSum, closenessReachable, harmonic);
        }

        /// <summary>
        /// Normalizes raw betweenness scores. raw[v] is accumulated by running Brandes' backward
        /// pass from every node as source (AccumulateBFSCentralitiesForSources), which sums over
        /// every ORDERED pair (s,t). For a directed graph that already matches the (n-1)(n-2)
        /// denominator (the count of ordered pairs excluding v). For an undirected graph (or any
        /// traversal where paths are symmetric, i.e. sigma_st(v) == sigma_ts(v)), each unordered
        /// pair {s,t} is summed twice this way - once as (s,t), once as (t,s) - so the true,
        /// unordered-pair sum is raw/2, which then divides by the unordered-pair count
        /// (n-1)(n-2)/2. Algebraically (raw/2)/((n-1)(n-2)/2) == raw/(n-1)(n-2): the same
        /// denominator as the directed case, so both directed and undirected always use (n-1)(n-2).
        /// When sampled (totalSources < n): scale up by n/totalSources.
        /// </summary>
        internal static Dictionary<uint, double> FinalizeBetweenness(Dictionary<uint, double> raw, uint[] allNodeIds, int totalSources)
        {
            int n = allNodeIds.Length;
            double norm = (n - 1.0) * (n - 2.0);
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
        /// arcs v→w and w→v (if both exist) are distinct ties with independent scores. No stored
        /// predecessor list - predecessors are re-derived per node via the reverse traversal
        /// direction, same as AccumulateBFSCentralities (only 1-mode layers apply here, so no
        /// hyperedge re-scan is needed).
        /// </summary>
        internal static void AccumulateEdgeBetweenness(uint source, Dictionary<uint, int> nodeIndex, uint[] nodeIds, ILayerOneMode layer, EdgeTraversal traversal, Dictionary<(uint From, uint To), double> edgeBetweenness, GraphAlgorithms.BFSBrandesScratch scratch)
        {
            GraphAlgorithms.BFSBrandes(source, nodeIndex, nodeIds, [layer], [], [], traversal, scratch);
            int[] dist = scratch.Dist;
            long[] sigma = scratch.Sigma;
            int[] stamp = scratch.Stamp;
            int myStamp = scratch.CurrentStamp;
            int[] order = scratch.Order;
            int orderCount = scratch.OrderCount;
            double[] delta = scratch.Delta;

            for (int i = 0; i < orderCount; i++) delta[order[i]] = 0.0;

            bool directed = layer.IsDirectional;
            int currentWIdx = 0;
            uint currentW = 0;
            int targetDist = 0;
            double sigmaw = 0;
            void Consider(uint v)
            {
                if (!nodeIndex.TryGetValue(v, out int vIdx) || stamp[vIdx] != myStamp || dist[vIdx] != targetDist)
                    return;
                double contrib = ((double)sigma[vIdx] / sigmaw) * (1.0 + delta[currentWIdx]);
                delta[vIdx] += contrib;
                var edgeKey = (directed || v < currentW) ? (v, currentW) : (currentW, v);
                edgeBetweenness[edgeKey] = edgeBetweenness.GetValueOrDefault(edgeKey) + contrib;
            }
            Action<uint> consider = Consider;

            for (int i = orderCount - 1; i >= 0; i--)
            {
                int wIdx = order[i];
                uint w = nodeIds[wIdx];
                currentWIdx = wIdx;
                currentW = w;
                targetDist = dist[wIdx] - 1;
                sigmaw = sigma[wIdx];

                if (traversal == EdgeTraversal.Both && layer.IsDirectional)
                    foreach (uint v in layer.GetNodeAlters(w, traversal))
                        consider(v);
                else
                    layer.VisitAlters(w, Reverse(traversal), consider);
            }
        }

        /// <summary>
        /// Runs AccumulateEdgeBetweenness for every source node in parallel (up to 'maxthreads', see
        /// 'setting()'), merging thread-local partial sums into a shared accumulator under a lock
        /// after each thread's batch — same accumulate+merge pattern as
        /// AccumulateBFSCentralitiesForSources.
        /// </summary>
        internal static Dictionary<(uint From, uint To), double> AccumulateEdgeBetweennessForSources(uint[] sources, Dictionary<uint, int> nodeIndex, uint[] nodeIds, ILayerOneMode layer, EdgeTraversal traversal)
        {
            var edgeBetweenness = new Dictionary<(uint From, uint To), double>();
            var mergeLock = new object();

            System.Threading.Tasks.Parallel.For(0, sources.Length, UserSettings.GetParallelOptions(),
                () => (
                    Local: new Dictionary<(uint From, uint To), double>(),
                    Scratch: new GraphAlgorithms.BFSBrandesScratch()
                ),
                (i, loopState, local) =>
                {
                    AccumulateEdgeBetweenness(sources[i], nodeIndex, nodeIds, layer, traversal, local.Local, local.Scratch);
                    return local;
                },
                local =>
                {
                    lock (mergeLock)
                    {
                        foreach (var (edge, value) in local.Local)
                            edgeBetweenness[edge] = edgeBetweenness.GetValueOrDefault(edge) + value;
                    }
                });

            return edgeBetweenness;
        }

        /// <summary>
        /// Normalizes raw edge betweenness scores. See FinalizeBetweenness for why both the
        /// directed and undirected case use the same (n-1)(n-2) denominator once the implicit
        /// double-counting in the undirected (symmetric-path) case is correctly accounted for.
        /// When sampled (totalSources &lt; n): scale up by n/totalSources.
        /// </summary>
        internal static Dictionary<(uint From, uint To), double> FinalizeEdgeBetweenness(Dictionary<(uint From, uint To), double> raw, int n, int totalSources)
        {
            double norm = (n - 1.0) * (n - 2.0);
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
        /// (co-members deduplicated across hyperedges), then iterates until convergence.
        /// Dangling nodes (no out-edges) distribute their mass uniformly to all nodes.
        /// Both the precompute and each iteration's update are parallelized (up to 'maxthreads',
        /// see 'setting()'): the precompute is embarrassingly parallel (each node writes only its
        /// own disjoint output-index slot, no merge needed); the per-iteration update is a
        /// scatter-add (each source node's share is added to every one of its targets), so it uses
        /// the same thread-local-accumulator-merged-under-lock pattern as
        /// AccumulateBFSCentralitiesForSources - each thread accumulates into its own private
        /// array, summed into the shared one once per thread's batch, not once per source. The
        /// outer iteration loop itself stays sequential (each iteration needs the previous one's
        /// fully-merged result). Floating-point summation is not associative, so a parallel run's
        /// result can differ from a sequential one by rounding noise at the tolerance level, same
        /// as any other parallel reduction in this codebase - not a behavior change in the
        /// converged value.
        /// </summary>
        internal static Dictionary<uint, double> PageRank(uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, double dampingFactor = 0.85, int maxIterations = 100, double tolerance = 1e-8)
        {
            int n = nodeIds.Length;
            if (n == 0) return [];

            // Build nodeId -> array-index map once (sequential setup, not the hot path) - mirrors
            // EigenvectorCentrality's own array-based approach in this same file.
            var idx = new Dictionary<uint, int>(n);
            for (int i = 0; i < n; i++) idx[nodeIds[i]] = i;

            // Precompute unique out-neighbor index lists (projected adjacency, deduplicated).
            // Each thread writes only its own slot outNeighbors[ui] - no merge/lock needed.
            var outNeighbors = new int[n][];
            System.Threading.Tasks.Parallel.For(0, n, UserSettings.GetParallelOptions(), ui =>
            {
                uint u = nodeIds[ui];
                var neighbors = new HashSet<int>();
                foreach (var layer in oneModes)
                    foreach (uint v in layer.GetNodeAlters(u, EdgeTraversal.Out))
                        if (idx.TryGetValue(v, out int vi)) neighbors.Add(vi);
                foreach (var layer in twoModesDynamic)
                {
                    var hec = layer.GetNonEmptyHyperedgeCollection(u);
                    if (hec == null) continue;
                    foreach (var he in hec.HyperEdges)
                        foreach (uint m in he.NodeIds)
                            if (m != u && idx.TryGetValue(m, out int mi)) neighbors.Add(mi);
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
                            if (m != u && idx.TryGetValue(m, out int mi)) neighbors.Add(mi);
                        }
                    }
                }
                outNeighbors[ui] = [.. neighbors];
            });

            double[] pr = new double[n];
            for (int i = 0; i < n; i++) pr[i] = 1.0 / n;
            double teleport = (1.0 - dampingFactor) / n;
            var mergeLock = new object();

            for (int iter = 0; iter < maxIterations; iter++)
            {
                double[] prNew = new double[n];
                for (int i = 0; i < n; i++) prNew[i] = teleport;
                double danglingSum = 0;

                System.Threading.Tasks.Parallel.For(0, n, UserSettings.GetParallelOptions(),
                    () => (Local: new double[n], DanglingSum: 0.0),
                    (ui, loopState, local) =>
                    {
                        int[] outs = outNeighbors[ui];
                        if (outs.Length == 0)
                        {
                            local.DanglingSum += pr[ui];
                            return local;
                        }
                        double share = dampingFactor * pr[ui] / outs.Length;
                        foreach (int vi in outs) local.Local[vi] += share;
                        return local;
                    },
                    local =>
                    {
                        lock (mergeLock)
                        {
                            for (int i = 0; i < n; i++) prNew[i] += local.Local[i];
                            danglingSum += local.DanglingSum;
                        }
                    });

                double danglingContrib = dampingFactor * danglingSum / n;
                for (int i = 0; i < n; i++) prNew[i] += danglingContrib;

                double maxDiff = 0;
                for (int i = 0; i < n; i++) maxDiff = Math.Max(maxDiff, Math.Abs(prNew[i] - pr[i]));
                pr = prNew;
                if (maxDiff < tolerance) break;
            }

            var result = new Dictionary<uint, double>(n);
            for (int i = 0; i < n; i++) result[nodeIds[i]] = pr[i];
            return result;
        }

        #endregion
    }
}