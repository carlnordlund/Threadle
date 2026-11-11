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
        /// Single-source plain-BFS pass that accumulates contributions to closeness and harmonic
        /// centrality only, fed by GraphAlgorithms.BFS. Closeness/harmonic only need distances, so
        /// this skips the path counting and backward pass that betweenness (BrandesBetweenness)
        /// needs - the same "distances only" traversal NetworkLevelFunctions.Diameter/Eccentricity
        /// use.
        /// </summary>
        internal static void AccumulateDistanceCentralities(uint source, GraphAlgorithms.BFSContext bfsContext, GraphAlgorithms.BFSScratch scratch, Dictionary<uint, double> closenessDistSum, Dictionary<uint, int> closenessReachable, Dictionary<uint, double> harmonic)
        {
            var dist = GraphAlgorithms.BFS(source, bfsContext, scratch);

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
        /// accumulators (thread-local partial sums, merged under a lock once per thread's batch).
        /// Used by ClosenessCentrality and HarmonicCentrality; betweenness uses
        /// BrandesBetweenness.NodeBetweenness.
        /// </summary>
        internal static (Dictionary<uint, double> ClosenessDistSum, Dictionary<uint, int> ClosenessReachable, Dictionary<uint, double> Harmonic)
            AccumulateDistanceCentralitiesForSources(uint[] sources, Dictionary<uint, int> nodeIndex, uint[] nodeIds, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
        {
            var closenessDistSum = new Dictionary<uint, double>();
            var closenessReachable = new Dictionary<uint, int>();
            var harmonic = new Dictionary<uint, double>();
            var mergeLock = new object();
            if (sources.Length == 0)
                return (closenessDistSum, closenessReachable, harmonic);
            var bfsContext = GraphAlgorithms.BFSContext.Create(nodeIndex, nodeIds, oneModes, twoModesDynamic, twoModesStatic, traversal);

            System.Threading.Tasks.Parallel.For(0, sources.Length, UserSettings.GetParallelOptions(),
                () => (
                    ClosenessDistSum: new Dictionary<uint, double>(),
                    ClosenessReachable: new Dictionary<uint, int>(),
                    Harmonic: new Dictionary<uint, double>(),
                    Scratch: new GraphAlgorithms.BFSScratch()
                ),
                (i, loopState, local) =>
                {
                    AccumulateDistanceCentralities(sources[i], bfsContext, local.Scratch,
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
        /// pass from every node as source (BrandesBetweenness.NodeBetweenness), which sums over
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
        /// AccumulateDistanceCentralitiesForSources - each thread accumulates into its own private
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

            // Out-neighbour table (distinct neighbours, the binary projection for 2-mode layers) as one CSR
            // array, built in two passes over the same neighbour stream - count, then fill - with a reusable
            // per-thread marker array for de-duplication, instead of a HashSet and an array per node.
            var offsets = new int[n + 1];
            System.Threading.Tasks.Parallel.For(0, n, UserSettings.GetParallelOptions(),
                () => new PageRankNeighbourScratch(n, idx),
                (ui, loopState, sc) =>
                {
                    offsets[ui + 1] = sc.Collect(nodeIds[ui], ui, oneModes, twoModesDynamic, twoModesStatic, null, 0);
                    return sc;
                },
                _ => { });
            for (int i = 0; i < n; i++) offsets[i + 1] += offsets[i];
            var neighbours = new int[offsets[n]];
            System.Threading.Tasks.Parallel.For(0, n, UserSettings.GetParallelOptions(),
                () => new PageRankNeighbourScratch(n, idx),
                (ui, loopState, sc) =>
                {
                    sc.Collect(nodeIds[ui], ui, oneModes, twoModesDynamic, twoModesStatic, neighbours, offsets[ui]);
                    return sc;
                },
                _ => { });

            // Pull-based iteration: each node sums the shares of the nodes pointing to it. For symmetric relations
            // (undirected 1-mode layers, 2-mode co-membership) those are its own out-neighbours; otherwise the
            // table is transposed once. No locks or per-thread n-sized buffers, and the summation order per node
            // is fixed, so the result does not depend on the number of threads.
            bool symmetric = oneModes.All(l => !l.IsDirectional);
            int[] inOffsets = offsets, inNeighbours = neighbours;
            if (!symmetric)
            {
                inOffsets = new int[n + 1];
                foreach (int v in neighbours) inOffsets[v + 1]++;
                for (int i = 0; i < n; i++) inOffsets[i + 1] += inOffsets[i];
                inNeighbours = new int[neighbours.Length];
                var fill = (int[])inOffsets.Clone();
                for (int u = 0; u < n; u++)
                    for (int k = offsets[u]; k < offsets[u + 1]; k++)
                        inNeighbours[fill[neighbours[k]]++] = u;
            }

            double[] pr = new double[n];
            double[] prNew = new double[n];
            double[] share = new double[n];
            for (int i = 0; i < n; i++) pr[i] = 1.0 / n;
            double teleport = (1.0 - dampingFactor) / n;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                double danglingSum = 0;
                for (int u = 0; u < n; u++)
                {
                    int deg = offsets[u + 1] - offsets[u];
                    if (deg == 0) { danglingSum += pr[u]; share[u] = 0; }
                    else share[u] = dampingFactor * pr[u] / deg;
                }
                double baseValue = teleport + dampingFactor * danglingSum / n;
                System.Threading.Tasks.Parallel.For(0, n, UserSettings.GetParallelOptions(), v =>
                {
                    double sum = 0;
                    for (int k = inOffsets[v]; k < inOffsets[v + 1]; k++)
                        sum += share[inNeighbours[k]];
                    prNew[v] = baseValue + sum;
                });

                double maxDiff = 0;
                for (int i = 0; i < n; i++) maxDiff = Math.Max(maxDiff, Math.Abs(prNew[i] - pr[i]));
                (pr, prNew) = (prNew, pr);
                if (maxDiff < tolerance) break;
            }

            var result = new Dictionary<uint, double>(n);
            for (int i = 0; i < n; i++) result[nodeIds[i]] = pr[i];
            return result;
        }


        /// <summary>
        /// Per-thread helper for PageRank's neighbour table: collects a node's distinct out-neighbours (1-mode
        /// layers via VisitAlters, 2-mode co-members excluding the node itself), counting them or writing them
        /// to the table, with a generation-stamped marker array for de-duplication.
        /// </summary>
        private sealed class PageRankNeighbourScratch
        {
            private readonly int[] _stamp;
            private readonly Dictionary<uint, int> _idx;
            private readonly Action<uint> _visit;
            private int _gen;
            private int _count;
            private int[]? _target;
            private int _pos;

            internal PageRankNeighbourScratch(int n, Dictionary<uint, int> idx)
            {
                _stamp = new int[n];
                _idx = idx;
                _visit = v => { if (_idx.TryGetValue(v, out int vi)) Add(vi); };
            }

            private void Add(int vi)
            {
                if (_stamp[vi] == _gen) return;
                _stamp[vi] = _gen;
                if (_target != null) _target[_pos + _count] = vi;
                _count++;
            }

            internal int Collect(uint u, int ui, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, int[]? target, int pos)
            {
                if (++_gen == int.MaxValue) { Array.Clear(_stamp); _gen = 1; }
                _count = 0; _target = target; _pos = pos;
                foreach (var layer in oneModes)
                    layer.VisitAlters(u, EdgeTraversal.Out, _visit);
                foreach (var layer in twoModesDynamic)
                {
                    var hec = layer.GetNonEmptyHyperedgeCollection(u);
                    if (hec == null) continue;
                    foreach (var he in hec.HyperEdges)
                        foreach (uint m in he.NodeIds)
                            if (m != u && _idx.TryGetValue(m, out int mi)) Add(mi);
                }
                foreach (var layer in twoModesStatic)
                {
                    if (!layer.TryGetNodeHyperedgeRange(u, out int nStart, out int nEnd)) continue;
                    for (int k = nStart; k < nEnd; k++)
                    {
                        layer.GetHyperedgeRange(layer.GetNodeHyperedgeIndex(k), out int hStart, out int hEnd);
                        for (int j = hStart; j < hEnd; j++)
                        {
                            uint m = layer.GetHyperedgeNodeAt(j);
                            if (m != u && _idx.TryGetValue(m, out int mi)) Add(mi);
                        }
                    }
                }
                return _count;
            }
        }

        #endregion
    }
}
