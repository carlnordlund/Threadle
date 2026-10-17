using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Analysis
{
    /// <summary>
    /// Internal functions for community detection.
    /// </summary>
    internal static class CommunityFunctions
    {
        private const double Epsilon = 1e-9;

        #region Methods (internal)

        /// <summary>
        /// Leiden (Traag, Waltman &amp; van Eck 2019): multi-level local-moving + aggregation, like
        /// <see cref="LouvainCommunities"/>, but with a refinement step inserted before each
        /// aggregation that fixes Louvain's known flaw of occasionally producing internally
        /// disconnected communities. After the local-moving phase produces a (coarse) partition P,
        /// <see cref="RefinePartition"/> rebuilds it from singletons, only ever merging a node into a
        /// sub-community it shares a direct edge with, and only within its own P-community — every
        /// resulting sub-community is therefore guaranteed connected by construction. Aggregation
        /// then groups nodes by this refined partition rather than by P directly, so a coarser-level
        /// super-node's members are always connected too. Merge choices within refinement are drawn
        /// via a softmax over positive modularity gains (<paramref name="randomness"/>, θ in the
        /// original paper; near 0 is effectively greedy, larger values explore more), rather than
        /// picking the single best candidate — this randomization is what lets Leiden escape local
        /// optima that a purely greedy method gets stuck in across repeated runs. Each subsequent
        /// aggregation level seeds its local-moving phase from the previous level's P-groups (instead
        /// of restarting from singletons), continuing from where the coarser level left off. Uses the
        /// same resolution-scaled modularity objective as CommunityDetectionLouvain (not the CPM
        /// objective from the original paper), so results are directly comparable. Directed layers
        /// are symmetrized and multiple layers combine via summed weight, matching
        /// CommunityDetectionLouvain. Returns the community index (0-based, compacted) for every node
        /// plus the achieved modularity Q. When numRestarts &gt; 1, runs that many independent restarts
        /// (each with its own random node order and refinement draws, via Misc.Random's thread-local
        /// stream) in parallel across up to 'maxthreads' threads (see 'setting()') and keeps the
        /// highest-modularity result. Because Misc.Random is thread-static rather than seeded per
        /// worker thread, exact reproducibility via randomseed() is only guaranteed when maxthreads
        /// is set to 1.
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LeidenCommunities(
            uint[] nodeIds, List<ILayerOneMode> layers, double resolution = 1.0, double randomness = 0.01, int numRestarts = 1)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            randomness = Math.Max(randomness, 1e-6);

            var (neighbors0, weights0, degree0, twoM) = BuildAdjacency(nodeIds, layers);

            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            if (numRestarts <= 1)
                return LeidenSingleRun(nodeIds, neighbors0, weights0, degree0, twoM, resolution, randomness);

            return RunBestOfRestarts(numRestarts,
                () => LeidenSingleRun(nodeIds, neighbors0, weights0, degree0, twoM, resolution, randomness));
        }

        /// <summary>
        /// A single Leiden pass: multi-level local-moving + refinement + aggregation starting from
        /// the given (shared, never mutated in place) level-0 adjacency. Safe to call concurrently
        /// for independent restarts against the same arrays.
        /// </summary>
        private static (Dictionary<uint, int> communities, double modularity) LeidenSingleRun(
            uint[] nodeIds, int[][] neighbors0, double[][] weights0, double[] degree0, double twoM, double resolution, double randomness)
        {
            int n0 = nodeIds.Length;
            int[] identity = new int[n0];
            for (int i = 0; i < n0; i++) identity[i] = i;

            int[] mapping = RunLeidenLevelsFrom(n0, identity, n0, neighbors0, weights0, degree0, twoM, resolution, randomness, seedCommunity: null);

            var communities = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++) communities[nodeIds[i]] = mapping[i];

            double modularity = ComputeModularity(n0, neighbors0, weights0, degree0, twoM, resolution, mapping);
            return (communities, modularity);
        }

        /// <summary>
        /// Leiden's multi-level local-moving + refinement + aggregation loop, extracted so it can
        /// be entered either from the true level-0 singleton start (<see cref="LeidenSingleRun"/>,
        /// 1-mode, behavior-preserving extraction — identical to the loop this replaced) or from an
        /// already-aggregated starting graph (<see cref="LeidenSingleRunTwoMode"/>, whose level-0
        /// is instead a hyperedge-direct local-moving pass over a 2-mode layer with no refinement
        /// step — see that method's remarks for why). <paramref name="initialMapping"/> maps each
        /// of the <paramref name="n0"/> original nodes to its starting-level node index (identity
        /// for the singleton case); the loop composes further aggregation into it exactly as
        /// before. <paramref name="seedCommunity"/> is null for a fresh start (both callers use
        /// this - the 2-mode caller does not carry its hyperedge-direct level's partition forward
        /// as a P-seed, unlike the loop's own cross-level seeding once inside it).
        /// </summary>
        private static int[] RunLeidenLevelsFrom(int n0, int[] initialMapping, int curN,
            int[][] curNeighbors, double[][] curWeights, double[] curDegree, double twoM, double resolution,
            double randomness, int[]? seedCommunity)
        {
            int[] mapping = (int[])initialMapping.Clone();

            while (true)
            {
                int[] community = new int[curN];
                if (seedCommunity == null)
                    for (int i = 0; i < curN; i++) community[i] = i;
                else
                    Array.Copy(seedCommunity, community, curN);

                bool improved = LocalMovingPhase(curN, curNeighbors, curWeights, curDegree, twoM, resolution, community);
                int[] refined = RefinePartition(curN, curNeighbors, curWeights, curDegree, twoM, resolution, community, randomness);

                var commMap = new Dictionary<int, int>(curN);
                for (int i = 0; i < curN; i++)
                    if (!commMap.ContainsKey(refined[i])) commMap[refined[i]] = commMap.Count;
                int newN = commMap.Count;

                for (int i = 0; i < n0; i++)
                    mapping[i] = commMap[refined[mapping[i]]];

                if (!improved || newN == curN)
                    break;

                var newAdj = new Dictionary<int, double>[newN];
                for (int i = 0; i < newN; i++) newAdj[i] = new Dictionary<int, double>();
                for (int u = 0; u < curN; u++)
                {
                    int cu = commMap[refined[u]];
                    var nbrs = curNeighbors[u];
                    var wts = curWeights[u];
                    for (int k = 0; k < nbrs.Length; k++)
                    {
                        int cv = commMap[refined[nbrs[k]]];
                        newAdj[cu][cv] = newAdj[cu].GetValueOrDefault(cv) + wts[k];
                    }
                }

                // Seed the next level's local-moving from the P-groups computed at this level
                // (every super-node's members share one P value, since refinement never crosses
                // a P boundary), so aggregation continues from Leiden's own prior progress instead
                // of restarting from singletons — same efficiency/quality trick as the paper.
                var pGroupToId = new Dictionary<int, int>(newN);
                int[] nextSeed = new int[newN];
                for (int u = 0; u < curN; u++)
                {
                    int cu = commMap[refined[u]];
                    int pu = community[u];
                    if (!pGroupToId.TryGetValue(pu, out int seedId))
                    {
                        seedId = pGroupToId.Count;
                        pGroupToId[pu] = seedId;
                    }
                    nextSeed[cu] = seedId;
                }

                (curNeighbors, curWeights) = ToArrays(newAdj, newN);
                curDegree = new double[newN];
                for (int i = 0; i < newN; i++) curDegree[i] = SumArray(curWeights[i]);
                curN = newN;
                seedCommunity = nextSeed;
            }

            return mapping;
        }

        /// <summary>
        /// Louvain modularity optimization (Blondel et al. 2008): multi-level local-moving +
        /// aggregation. Directed layers are symmetrized (arc u→v and v→u both accumulate into an
        /// undirected weight, same convention as <see cref="LocalStructureFunctions.StructuralHoles"/>)
        /// since modularity optimization is inherently an undirected-graph formulation. Multiple
        /// layers combine via summed weight; binary layers contribute 1.0 per edge. Self-ties
        /// contribute twice to a node's degree, per the standard undirected-graph convention.
        /// Returns the community index (0-based, compacted) for every node plus the achieved
        /// modularity Q using the given resolution (1.0 = standard modularity; &gt;1 favors more,
        /// smaller communities; &lt;1 favors fewer, larger ones — Reichardt &amp; Bornholdt 2006).
        /// When numRestarts &gt; 1, runs that many independent restarts (each with its own random node
        /// order, via Misc.Random's thread-local stream) in parallel across up to 'maxthreads'
        /// threads (see 'setting()') and keeps the highest-modularity result — local-moving's random
        /// sweep order means different restarts can land in different local optima. Because
        /// Misc.Random is thread-static rather than seeded per worker thread, exact reproducibility
        /// via randomseed() is only guaranteed when maxthreads is set to 1.
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LouvainCommunities(
            uint[] nodeIds, List<ILayerOneMode> layers, double resolution = 1.0, int numRestarts = 1)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            var (neighbors0, weights0, degree0, twoM) = BuildAdjacency(nodeIds, layers);

            if (twoM <= 0)
            {
                // No edges at all: every node is its own singleton community.
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            if (numRestarts <= 1)
                return LouvainSingleRun(nodeIds, neighbors0, weights0, degree0, twoM, resolution);

            return RunBestOfRestarts(numRestarts,
                () => LouvainSingleRun(nodeIds, neighbors0, weights0, degree0, twoM, resolution));
        }

        /// <summary>
        /// A single Louvain pass: multi-level local-moving + aggregation starting from the given
        /// (shared, never mutated in place — each aggregation level builds brand-new arrays) level-0
        /// adjacency. Safe to call concurrently for independent restarts against the same arrays.
        /// </summary>
        private static (Dictionary<uint, int> communities, double modularity) LouvainSingleRun(
            uint[] nodeIds, int[][] neighbors0, double[][] weights0, double[] degree0, double twoM, double resolution)
        {
            int n0 = nodeIds.Length;
            int[] identity = new int[n0];
            for (int i = 0; i < n0; i++) identity[i] = i;

            int[] mapping = RunLouvainLevelsFrom(n0, identity, n0, neighbors0, weights0, degree0, twoM, resolution);

            var communities = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++) communities[nodeIds[i]] = mapping[i];

            double modularity = ComputeModularity(n0, neighbors0, weights0, degree0, twoM, resolution, mapping);
            return (communities, modularity);
        }

        /// <summary>
        /// Louvain's multi-level local-moving + aggregation loop, extracted so it can be entered
        /// either from the true level-0 singleton start (<see cref="LouvainSingleRun"/>, 1-mode,
        /// behavior-preserving extraction — identical to the loop this replaced) or from an
        /// already-aggregated starting graph (<see cref="LouvainSingleRunTwoMode"/>, which builds
        /// that starting graph directly from a 2-mode layer's hyperedges, bypassing
        /// <see cref="BuildAdjacency"/> entirely). <paramref name="initialMapping"/> maps each of
        /// the <paramref name="n0"/> original nodes to its starting-level node index (identity for
        /// the singleton case); the loop composes further aggregation into it exactly as before.
        /// </summary>
        private static int[] RunLouvainLevelsFrom(int n0, int[] initialMapping, int curN,
            int[][] curNeighbors, double[][] curWeights, double[] curDegree, double twoM, double resolution)
        {
            int[] mapping = (int[])initialMapping.Clone();

            while (true)
            {
                int[] community = new int[curN];
                for (int i = 0; i < curN; i++) community[i] = i;

                bool improved = LocalMovingPhase(curN, curNeighbors, curWeights, curDegree, twoM, resolution, community);

                var commMap = new Dictionary<int, int>(curN);
                for (int i = 0; i < curN; i++)
                    if (!commMap.ContainsKey(community[i])) commMap[community[i]] = commMap.Count;
                int newN = commMap.Count;

                for (int i = 0; i < n0; i++)
                    mapping[i] = commMap[community[mapping[i]]];

                if (!improved || newN == curN)
                    break;

                var newAdj = new Dictionary<int, double>[newN];
                for (int i = 0; i < newN; i++) newAdj[i] = new Dictionary<int, double>();
                for (int u = 0; u < curN; u++)
                {
                    int cu = commMap[community[u]];
                    var nbrs = curNeighbors[u];
                    var wts = curWeights[u];
                    for (int k = 0; k < nbrs.Length; k++)
                    {
                        int cv = commMap[community[nbrs[k]]];
                        newAdj[cu][cv] = newAdj[cu].GetValueOrDefault(cv) + wts[k];
                    }
                }

                (curNeighbors, curWeights) = ToArrays(newAdj, newN);
                curDegree = new double[newN];
                for (int i = 0; i < newN; i++) curDegree[i] = SumArray(curWeights[i]);
                curN = newN;
            }

            return mapping;
        }

        /// <summary>
        /// Asynchronous label propagation (Raghavan, Albert &amp; Kumara 2007): every node starts with
        /// a unique label; repeatedly, in random order, each node adopts whichever label carries the
        /// greatest total edge weight among its current neighbors (ties broken at random), updating
        /// in place so later nodes in the same pass see already-updated labels. Stops when a full
        /// pass produces no changes, or after <paramref name="maxIterations"/> passes — unlike
        /// Louvain's local-moving phase, LPA has no monotonic quantity guaranteeing convergence
        /// (labels can cycle), so the cap is required, not just a safety net. Layers are combined and
        /// symmetrized exactly as in <see cref="LouvainCommunities"/>. The returned modularity is not
        /// optimized by this method (LPA doesn't target modularity at all) — it's reported purely so
        /// results are comparable across community detection methods. When numRestarts &gt; 1, runs
        /// that many independent restarts (each with its own random sweep order and tie-breaks, via
        /// Misc.Random's thread-local stream) in parallel across up to 'maxthreads' threads (see
        /// 'setting()') and keeps the one with the highest modularity — a secondary selection
        /// criterion here, since LPA itself doesn't target modularity, but still a reasonable way to
        /// pick among otherwise-arbitrary runs. Because Misc.Random is thread-static rather than
        /// seeded per worker thread, exact reproducibility via randomseed() is only guaranteed when
        /// maxthreads is set to 1.
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LabelPropagationCommunities(
            uint[] nodeIds, List<ILayerOneMode> layers, int maxIterations = 100, int numRestarts = 1)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            var (neighbors, weights, degree, twoM) = BuildAdjacency(nodeIds, layers);

            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            if (numRestarts <= 1)
                return LabelPropagationSingleRun(nodeIds, neighbors, weights, degree, twoM, maxIterations);

            return RunBestOfRestarts(numRestarts,
                () => LabelPropagationSingleRun(nodeIds, neighbors, weights, degree, twoM, maxIterations));
        }

        /// <summary>
        /// A single label-propagation pass against the given (shared, never mutated in place) level-0
        /// adjacency. Safe to call concurrently for independent restarts against the same arrays.
        /// </summary>
        private static (Dictionary<uint, int> communities, double modularity) LabelPropagationSingleRun(
            uint[] nodeIds, int[][] neighbors, double[][] weights, double[] degree, double twoM, int maxIterations)
        {
            int n0 = nodeIds.Length;

            int[] label = new int[n0];
            for (int i = 0; i < n0; i++) label[i] = i;

            int[] order = new int[n0];
            for (int i = 0; i < n0; i++) order[i] = i;

            var weightByLabel = new Dictionary<int, double>();
            var bestLabels = new List<int>();

            for (int pass = 0; pass < maxIterations; pass++)
            {
                Shuffle(order);
                bool changed = false;

                foreach (int u in order)
                {
                    var nbrs = neighbors[u];
                    if (nbrs.Length == 0) continue;
                    var wts = weights[u];

                    weightByLabel.Clear();
                    for (int k = 0; k < nbrs.Length; k++)
                    {
                        int l = label[nbrs[k]];
                        weightByLabel[l] = weightByLabel.GetValueOrDefault(l) + wts[k];
                    }

                    double maxWeight = double.NegativeInfinity;
                    foreach (double w in weightByLabel.Values)
                        if (w > maxWeight) maxWeight = w;

                    bestLabels.Clear();
                    foreach (var (l, w) in weightByLabel)
                        if (w >= maxWeight - Epsilon) bestLabels.Add(l);

                    int newLabel = bestLabels[Misc.Random.Next(bestLabels.Count)];
                    if (newLabel != label[u])
                    {
                        label[u] = newLabel;
                        changed = true;
                    }
                }

                if (!changed) break;
            }

            // Compact labels to a contiguous 0-based community index.
            var labelMap = new Dictionary<int, int>();
            var communities = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++)
            {
                if (!labelMap.TryGetValue(label[i], out int c))
                {
                    c = labelMap.Count;
                    labelMap[label[i]] = c;
                }
                communities[nodeIds[i]] = c;
            }

            int[] mapping = new int[n0];
            for (int i = 0; i < n0; i++) mapping[i] = labelMap[label[i]];
            double modularity = ComputeModularity(n0, neighbors, weights, degree, twoM, 1.0, mapping);
            return (communities, modularity);
        }

        /// <summary>
        /// Girvan-Newman (2002): divisive community detection. Unlike Louvain/Leiden/LP/LPAm, which
        /// build communities up from singletons (agglomerative), this tears the graph down one edge
        /// at a time. At each step: compute edge betweenness over the currently-remaining edges,
        /// remove the single highest-betweenness edge, and score the resulting connected-component
        /// partition by modularity — evaluated against the *original*, un-cut graph, since the
        /// point is to judge how well that partition explains the original network, not the
        /// edge-depleted working copy. The partition with the highest modularity seen across every
        /// removal step (including the trivial all-connected start) is returned. Directed layers are
        /// symmetrized and multiple layers combine via summed weight, matching every other community
        /// detection method (<see cref="BuildAdjacency"/>). Edge betweenness here is the unweighted
        /// (hop-count shortest paths) kind, same convention as CentralityFunctions' node/edge
        /// betweenness — edge weight only decides which edges exist, not path length. Because a full
        /// betweenness recomputation is needed after every single edge removal, this is substantially
        /// more expensive than the agglomerative methods — O(V·E²) in the worst case — so it is best
        /// suited to smaller networks; the per-removal betweenness pass itself runs in parallel across
        /// up to 'maxthreads' threads (see 'setting()'). Deterministic given the graph (ties for
        /// highest betweenness are broken by lowest node-index pair), so unlike the other methods this
        /// has no numRestarts.
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) GirvanNewmanCommunities(
            uint[] nodeIds, List<ILayerOneMode> layers, double resolution = 1.0)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            var (neighbors0, weights0, degree0, twoM) = BuildAdjacency(nodeIds, layers);

            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            // Mutable working copy: edges are progressively removed from this, while neighbors0/
            // weights0 (and twoM) stay untouched, since modularity is always scored against them.
            var workAdj = new Dictionary<int, double>[n0];
            int remainingEdges = 0;
            for (int u = 0; u < n0; u++)
            {
                workAdj[u] = new Dictionary<int, double>(neighbors0[u].Length);
                for (int k = 0; k < neighbors0[u].Length; k++)
                {
                    int v = neighbors0[u][k];
                    if (v == u) continue; // self-ties never lie on a shortest path between two other nodes
                    workAdj[u][v] = weights0[u][k];
                    if (v > u) remainingEdges++;
                }
            }

            int[] bestPartition = ConnectedComponentsFromAdjacency(n0, workAdj);
            double bestModularity = ComputeModularity(n0, neighbors0, weights0, degree0, twoM, resolution, bestPartition);

            while (remainingEdges > 0)
            {
                var betweenness = ComputeEdgeBetweenness(n0, workAdj);
                int bestU = -1, bestV = -1;
                double bestScore = -1.0;
                // Deterministic tie-break: iterate in ascending (u,v) order so the first edge to
                // reach the max score wins, regardless of the parallel merge order inside
                // ComputeEdgeBetweenness (dictionary iteration order would otherwise depend on it).
                foreach (var ((u, v), score) in betweenness.OrderBy(kv => kv.Key))
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestU = u; bestV = v;
                    }
                if (bestU < 0) break; // no more edges carry any betweenness (shouldn't happen while remainingEdges > 0)

                workAdj[bestU].Remove(bestV);
                workAdj[bestV].Remove(bestU);
                remainingEdges--;

                int[] partition = ConnectedComponentsFromAdjacency(n0, workAdj);
                double modularity = ComputeModularity(n0, neighbors0, weights0, degree0, twoM, resolution, partition);
                if (modularity > bestModularity)
                {
                    bestModularity = modularity;
                    bestPartition = partition;
                }
            }

            var communities = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++) communities[nodeIds[i]] = bestPartition[i];
            return (communities, bestModularity);
        }

        #endregion

        #region Methods (private)

        /// <summary>
        /// Runs 'numRestarts' independent trials of a community-detection algorithm in parallel
        /// (up to 'maxthreads', see 'setting()') and returns the one with the highest modularity.
        /// Each trial is a fully self-contained call (its own freshly-allocated working arrays; no
        /// shared mutable state between trials beyond the read-only level-0 adjacency the caller's
        /// closure captures), and each draws from Misc.Random's thread-local stream, so restarts on
        /// different threads explore genuinely different node orders/tie-breaks/refinement choices
        /// without any coordination between them — no locking needed except for the final
        /// keep-the-best comparison.
        /// </summary>
        private static (Dictionary<uint, int> communities, double modularity) RunBestOfRestarts(
            int numRestarts, Func<(Dictionary<uint, int> communities, double modularity)> singleRun)
        {
            var mergeLock = new object();
            (Dictionary<uint, int> communities, double modularity)? best = null;

            System.Threading.Tasks.Parallel.For(0, numRestarts, UserSettings.GetParallelOptions(),
                () => ((Dictionary<uint, int> communities, double modularity)?)null,
                (i, loopState, localBest) =>
                {
                    var candidate = singleRun();
                    if (localBest == null || candidate.modularity > localBest.Value.modularity)
                        return candidate;
                    return localBest;
                },
                localBest =>
                {
                    if (localBest == null) return;
                    lock (mergeLock)
                    {
                        if (best == null || localBest.Value.modularity > best.Value.modularity)
                            best = localBest;
                    }
                });

            return best!.Value;
        }

        /// <summary>
        /// Leiden's refinement step: rebuilds <paramref name="P"/> from singletons, merging a node
        /// into a neighboring sub-community only if (a) that neighbor shares the same P-community —
        /// merges never cross a P boundary — and (b) the node has a direct edge to a member of that
        /// sub-community, since candidates are drawn only from neighbors' current sub-community
        /// labels. Both conditions together guarantee every sub-community in the result is
        /// internally connected. Each node is visited at most once (it is skipped once it has left
        /// its own singleton), and its move — if any — is drawn via a softmax over positive
        /// modularity-gain candidates (temperature <paramref name="randomness"/>), not a greedy
        /// argmax, so repeated runs can explore different, still-valid refinements.
        /// </summary>
        private static int[] RefinePartition(int n, int[][] neighbors, double[][] weights,
            double[] degree, double twoM, double resolution, int[] P, double randomness)
        {
            int[] refined = new int[n];
            for (int i = 0; i < n; i++) refined[i] = i;

            var sigmaTot = new double[n];
            for (int i = 0; i < n; i++) sigmaTot[i] = degree[i];

            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Shuffle(order);

            var candidateWeight = new Dictionary<int, double>();
            var candidateIds = new List<int>();
            var candidateGains = new List<double>();
            var candidateWeights = new List<double>();

            foreach (int u in order)
            {
                if (refined[u] != u) continue; // already merged away from its singleton

                int pu = P[u];
                candidateWeight.Clear();
                var nbrs = neighbors[u];
                var wts = weights[u];
                for (int k = 0; k < nbrs.Length; k++)
                {
                    int v = nbrs[k];
                    if (v == u || P[v] != pu) continue; // stay within u's own P-community
                    int c = refined[v];
                    candidateWeight[c] = candidateWeight.GetValueOrDefault(c) + wts[k];
                }
                if (candidateWeight.Count == 0) continue; // no eligible neighbor: stays singleton

                sigmaTot[u] -= degree[u];

                candidateIds.Clear();
                candidateGains.Clear();
                double bestGain = 0.0; // staying singleton has gain 0 by definition
                foreach (var (c, kIn) in candidateWeight)
                {
                    double gain = kIn - resolution * sigmaTot[c] * degree[u] / twoM;
                    if (gain > Epsilon)
                    {
                        candidateIds.Add(c);
                        candidateGains.Add(gain);
                        if (gain > bestGain) bestGain = gain;
                    }
                }

                if (candidateIds.Count == 0)
                {
                    sigmaTot[u] += degree[u]; // no improving move: stays singleton
                    continue;
                }

                // Numerically-stable softmax: shifting by bestGain keeps every exponent <= 0.
                candidateWeights.Clear();
                for (int k = 0; k < candidateGains.Count; k++)
                    candidateWeights.Add(Math.Exp((candidateGains[k] - bestGain) / randomness));

                int chosen = WeightedRandomChoice(candidateIds, candidateWeights);
                refined[u] = chosen;
                sigmaTot[chosen] += degree[u];
            }

            return refined;
        }

        /// <summary>
        /// Picks one of <paramref name="ids"/> at random, with probability proportional to the
        /// corresponding entry in <paramref name="weights"/> (assumed non-negative, not necessarily
        /// normalized).
        /// </summary>
        private static int WeightedRandomChoice(List<int> ids, List<double> weights)
        {
            double total = 0.0;
            foreach (double w in weights) total += w;

            double r = Misc.Random.NextDouble() * total;
            double cumulative = 0.0;
            for (int i = 0; i < ids.Count; i++)
            {
                cumulative += weights[i];
                if (r <= cumulative) return ids[i];
            }
            return ids[^1]; // floating-point fallback
        }

        /// <summary>
        /// Repeatedly sweeps all nodes (in random order) moving each to whichever neighboring
        /// community (including its own) yields the greatest modularity gain, until a full sweep
        /// produces no moves. Returns whether any node ever moved.
        /// </summary>
        private static bool LocalMovingPhase(int n, int[][] neighbors, double[][] weights,
            double[] degree, double twoM, double resolution, int[] community)
        {
            // Aggregated by the actual starting groups in `community` rather than assumed to be
            // singletons — matters once callers (e.g. Leiden) seed this with a non-singleton
            // partition; reduces to the old sigmaTot[i] = degree[i] when community[i] == i for all i.
            var sigmaTot = new double[n];
            for (int i = 0; i < n; i++) sigmaTot[community[i]] += degree[i];

            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Shuffle(order);

            bool improvedAny = false;
            var neighborCommWeight = new Dictionary<int, double>();

            bool improvedThisPass = true;
            while (improvedThisPass)
            {
                improvedThisPass = false;
                foreach (int u in order)
                {
                    int currentComm = community[u];
                    sigmaTot[currentComm] -= degree[u];

                    neighborCommWeight.Clear();
                    var nbrs = neighbors[u];
                    var wts = weights[u];
                    for (int k = 0; k < nbrs.Length; k++)
                    {
                        int v = nbrs[k];
                        if (v == u) continue; // self-loop: no inter-community linkage signal
                        int c = community[v];
                        neighborCommWeight[c] = neighborCommWeight.GetValueOrDefault(c) + wts[k];
                    }

                    int bestComm = currentComm;
                    double bestGain = neighborCommWeight.GetValueOrDefault(currentComm)
                        - resolution * sigmaTot[currentComm] * degree[u] / twoM;

                    foreach (var (c, kIn) in neighborCommWeight)
                    {
                        if (c == currentComm) continue;
                        double gain = kIn - resolution * sigmaTot[c] * degree[u] / twoM;
                        if (gain > bestGain + Epsilon)
                        {
                            bestGain = gain;
                            bestComm = c;
                        }
                    }

                    sigmaTot[bestComm] += degree[u];
                    if (bestComm != currentComm)
                    {
                        community[u] = bestComm;
                        improvedThisPass = true;
                        improvedAny = true;
                    }
                }
            }
            return improvedAny;
        }        /// <summary>
                 /// LPAm (Barber &amp; Clark 2009): label propagation constrained by modularity — every node
                 /// starts with a unique label; repeatedly, in random order, each node adopts whichever label
                 /// among its current neighbors (or keeps its own) yields the greatest modularity gain,
                 /// rather than plain majority vote. This is the same greedy move rule as one level of
                 /// <see cref="LocalMovingPhase"/>, but — unlike <see cref="LouvainCommunities"/> — it is run
                 /// at a single level only, with no aggregation into a coarser graph afterwards. Because moves
                 /// are gated on actually improving modularity, it avoids vanilla label propagation's
                 /// "monster community" collapse (a move that would merge everything into one giant blob
                 /// always has negative gain once that blob's own internal density stops exceeding the null
                 /// model). It typically yields more, smaller communities than Louvain, since it can't escape
                 /// local optima the way Louvain's aggregation levels do. Layers are combined and symmetrized
                 /// exactly as in <see cref="LouvainCommunities"/>. When numRestarts &gt; 1, runs that many
                 /// independent restarts (each with its own random sweep order, via Misc.Random's
                 /// thread-local stream) in parallel across up to 'maxthreads' threads (see 'setting()')
                 /// and keeps the highest-modularity result. Because Misc.Random is thread-static rather
                 /// than seeded per worker thread, exact reproducibility via randomseed() is only
                 /// guaranteed when maxthreads is set to 1.
                 /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LPAmCommunities(
            uint[] nodeIds, List<ILayerOneMode> layers, int numRestarts = 1)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            var (neighbors, weights, degree, twoM) = BuildAdjacency(nodeIds, layers);

            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            if (numRestarts <= 1)
                return LPAmSingleRun(nodeIds, neighbors, weights, degree, twoM);

            return RunBestOfRestarts(numRestarts,
                () => LPAmSingleRun(nodeIds, neighbors, weights, degree, twoM));
        }

        /// <summary>
        /// A single LPAm pass against the given (shared, never mutated in place) level-0 adjacency.
        /// Safe to call concurrently for independent restarts against the same arrays.
        /// </summary>
        private static (Dictionary<uint, int> communities, double modularity) LPAmSingleRun(
            uint[] nodeIds, int[][] neighbors, double[][] weights, double[] degree, double twoM)
        {
            int n0 = nodeIds.Length;

            int[] community = new int[n0];
            for (int i = 0; i < n0; i++) community[i] = i;

            LocalMovingPhase(n0, neighbors, weights, degree, twoM, 1.0, community);

            var commMap = new Dictionary<int, int>(n0);
            var communities = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++)
            {
                if (!commMap.TryGetValue(community[i], out int c))
                {
                    c = commMap.Count;
                    commMap[community[i]] = c;
                }
                communities[nodeIds[i]] = c;
            }

            int[] mapping = new int[n0];
            for (int i = 0; i < n0; i++) mapping[i] = commMap[community[i]];
            double modularity = ComputeModularity(n0, neighbors, weights, degree, twoM, 1.0, mapping);
            return (communities, modularity);
        }


        /// <summary>
        /// Computes Q = Σ_c [ Σ_in(c)/2m − resolution·(Σ_tot(c)/2m)² ] directly from the original
        /// (level-0) graph and the final per-node community mapping — avoids having to track
        /// modularity incrementally across aggregation levels.
        /// </summary>
        private static double ComputeModularity(int n0, int[][] neighbors0, double[][] weights0,
            double[] degree0, double twoM, double resolution, int[] mapping)
        {
            var sigmaTot = new Dictionary<int, double>();
            var sigmaIn = new Dictionary<int, double>();
            for (int u = 0; u < n0; u++)
            {
                int cu = mapping[u];
                sigmaTot[cu] = sigmaTot.GetValueOrDefault(cu) + degree0[u];
                var nbrs = neighbors0[u];
                var wts = weights0[u];
                for (int k = 0; k < nbrs.Length; k++)
                    if (mapping[nbrs[k]] == cu)
                        sigmaIn[cu] = sigmaIn.GetValueOrDefault(cu) + wts[k];
            }

            double q = 0.0;
            foreach (var (c, stot) in sigmaTot)
            {
                double sin = sigmaIn.GetValueOrDefault(c);
                double frac = stot / twoM;
                q += sin / twoM - resolution * frac * frac;
            }
            return q;
        }

        /// <summary>
        /// Compacted (0-based) connected-component index per node, via plain BFS over the given
        /// (int-indexed, undirected) adjacency. Used by <see cref="GirvanNewmanCommunities"/> to turn
        /// the edge-depleted working graph into a candidate partition after each removal.
        /// </summary>
        private static int[] ConnectedComponentsFromAdjacency(int n, Dictionary<int, double>[] adj)
        {
            int[] component = new int[n];
            Array.Fill(component, -1);
            int next = 0;
            var queue = new Queue<int>();
            for (int start = 0; start < n; start++)
            {
                if (component[start] != -1) continue;
                component[start] = next;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int u = queue.Dequeue();
                    foreach (int v in adj[u].Keys)
                        if (component[v] == -1)
                        {
                            component[v] = next;
                            queue.Enqueue(v);
                        }
                }
                next++;
            }
            return component;
        }

        /// <summary>
        /// Unweighted Brandes edge betweenness over the given (int-indexed, undirected) adjacency —
        /// only which pairs are adjacent matters, not their weight. Runs the per-source BFS passes in
        /// parallel across up to 'maxthreads' threads (see 'setting()'), merging thread-local partial
        /// sums under a lock once per thread's batch — same accumulate+merge pattern as
        /// CentralityFunctions.AccumulateEdgeBetweennessForSources, just over this module's own
        /// compact adjacency representation rather than ILayerOneMode.
        /// </summary>
        private static Dictionary<(int, int), double> ComputeEdgeBetweenness(int n, Dictionary<int, double>[] adj)
        {
            var result = new Dictionary<(int, int), double>();
            var mergeLock = new object();

            System.Threading.Tasks.Parallel.For(0, n, UserSettings.GetParallelOptions(),
                () => new Dictionary<(int, int), double>(),
                (source, loopState, local) =>
                {
                    AccumulateEdgeBetweennessFromSource(source, n, adj, local);
                    return local;
                },
                local =>
                {
                    lock (mergeLock)
                    {
                        foreach (var (edge, value) in local)
                            result[edge] = result.GetValueOrDefault(edge) + value;
                    }
                });
            return result;
        }

        /// <summary>
        /// Single-source unweighted BFS + Brandes backward pass, crediting each predecessor edge on
        /// the shortest-path DAG with the same per-predecessor contribution term node betweenness
        /// would fold into the node's delta. Self-loops (v == u) are skipped: they never lie on a
        /// shortest path between two other nodes.
        /// </summary>
        private static void AccumulateEdgeBetweennessFromSource(int source, int n, Dictionary<int, double>[] adj, Dictionary<(int, int), double> edgeBetweenness)
        {
            var dist = new int[n];
            Array.Fill(dist, -1);
            var sigma = new double[n];
            var pred = new List<int>?[n];
            var order = new Stack<int>();
            var queue = new Queue<int>();

            dist[source] = 0;
            sigma[source] = 1.0;
            queue.Enqueue(source);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                order.Push(u);
                foreach (int v in adj[u].Keys)
                {
                    if (v == u) continue;
                    if (dist[v] < 0)
                    {
                        dist[v] = dist[u] + 1;
                        queue.Enqueue(v);
                    }
                    if (dist[v] == dist[u] + 1)
                    {
                        sigma[v] += sigma[u];
                        (pred[v] ??= []).Add(u);
                    }
                }
            }

            var delta = new double[n];
            while (order.Count > 0)
            {
                int w = order.Pop();
                if (pred[w] == null) continue;
                foreach (int v in pred[w]!)
                {
                    double contrib = (sigma[v] / sigma[w]) * (1.0 + delta[w]);
                    delta[v] += contrib;
                    var edgeKey = v < w ? (v, w) : (w, v);
                    edgeBetweenness[edgeKey] = edgeBetweenness.GetValueOrDefault(edgeKey) + contrib;
                }
            }
        }

        private static void Shuffle(int[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                int j = Misc.Random.Next(i, arr.Length);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }

        private static (int[][] neighbors, double[][] weights) ToArrays(Dictionary<int, double>[] adj, int n)
        {
            var neighbors = new int[n][];
            var weights = new double[n][];
            for (int i = 0; i < n; i++)
            {
                neighbors[i] = new int[adj[i].Count];
                weights[i] = new double[adj[i].Count];
                int k = 0;
                foreach (var (j, w) in adj[i]) { neighbors[i][k] = j; weights[i][k] = w; k++; }
            }
            return (neighbors, weights);
        }

        private static double SumArray(double[] a)
        {
            double s = 0;
            foreach (double v in a) s += v;
            return s;
        }

        /// <summary>
        /// Builds a symmetrized, weighted adjacency (as parallel neighbor/weight arrays keyed by
        /// position in <paramref name="nodeIds"/>) combining all specified layers. Directed arcs u→v
        /// and v→u both accumulate into the same undirected weight (same convention as
        /// <see cref="LocalStructureFunctions.StructuralHoles"/>); binary layers contribute 1.0 per
        /// edge; self-ties end up counted twice, per the standard undirected-graph degree convention.
        /// Also returns each node's total weighted degree and 2m (the sum of all degrees).
        /// </summary>
        private static (int[][] neighbors, double[][] weights, double[] degree, double twoM) BuildAdjacency(
            uint[] nodeIds, List<ILayerOneMode> layers)
        {
            int n = nodeIds.Length;
            var idx = new Dictionary<uint, int>(n);
            for (int i = 0; i < n; i++) idx[nodeIds[i]] = i;

            var adjDict = new Dictionary<int, double>[n];
            for (int i = 0; i < n; i++) adjDict[i] = new Dictionary<int, double>();

            foreach (var layer in layers)
            {
                foreach (var (egoId, alters, values) in layer.GetAllEgoData())
                {
                    if (!idx.TryGetValue(egoId, out int ui)) continue;
                    var aSpan = alters.Span;
                    var wSpan = values.Span;
                    bool hasWeights = wSpan.Length > 0;
                    for (int k = 0; k < aSpan.Length; k++)
                    {
                        if (!idx.TryGetValue(aSpan[k], out int vi)) continue;
                        double w = hasWeights ? wSpan[k] : 1.0;
                        adjDict[ui][vi] = adjDict[ui].GetValueOrDefault(vi) + w;
                        adjDict[vi][ui] = adjDict[vi].GetValueOrDefault(ui) + w;
                    }
                }

                // GetAllEgoData() never yields self-loops for symmetric layers: add them explicitly,
                // counted twice per the standard undirected-graph degree convention (same as a normal
                // discovered edge would be, which accumulates into adjDict[ui][vi] from both "sides").
                if (!layer.IsDirectional && layer.Selfties)
                    foreach (uint nodeId in nodeIds)
                    {
                        if (!idx.TryGetValue(nodeId, out int ui)) continue;
                        if (!layer.CheckEdgeExists(nodeId, nodeId)) continue;
                        double w = layer.GetEdgeValue(nodeId, nodeId);
                        adjDict[ui][ui] = adjDict[ui].GetValueOrDefault(ui) + 2 * w;
                    }
            }

            var (neighbors, weights) = ToArrays(adjDict, n);
            double[] degree = new double[n];
            for (int i = 0; i < n; i++) degree[i] = SumArray(weights[i]);
            double twoM = SumArray(degree);
            return (neighbors, weights, degree, twoM);
        }

        /// <summary>
        /// Count-weighted per-node degree for a single 2-mode layer, pseudo-projected (traversed
        /// node-hyperedge-node directly, without materializing a 1-mode projection): the sum of
        /// shared-hyperedge counts over each node's distinct co-affiliates, via
        /// <see cref="ILayer.GetNodeAltersWithWeights"/> (the same weight the engine already
        /// computes for eigenvectorcentrality()/getedge()'s default projection). Unlike
        /// <see cref="BuildAdjacency"/>, this never builds a pairwise adjacency structure — cost is
        /// O(Σ of each node's own hyperedge sizes), the same bound every other 2-mode-capable
        /// traversal in this codebase relies on.
        /// </summary>
        private static (double[] degree, double twoM) ComputeDegreeTwoMode(uint[] nodeIds, ILayer layer)
        {
            int n = nodeIds.Length;
            double[] degree = new double[n];
            for (int i = 0; i < n; i++)
            {
                var (alters, weights) = layer.GetNodeAltersWithWeights(nodeIds[i], EdgeTraversal.Both);
                double d = 0.0;
                var wSpan = weights.Span;
                for (int k = 0; k < alters.Length; k++)
                    d += wSpan.Length > 0 ? wSpan[k] : 1.0;
                degree[i] = d;
            }
            return (degree, SumArray(degree));
        }

        /// <summary>
        /// A single hyperedge-direct local-moving pass over a 2-mode layer: the same
        /// sweep-until-no-moves loop and modularity-gain formula as <see cref="LocalMovingPhase"/>,
        /// but each node's weight toward a candidate community is accumulated directly from
        /// <see cref="ILayer.GetNodeAltersWithWeights"/> (grouped by each co-affiliate's *current*
        /// community) instead of being read from a precomputed neighbor/weight array - so no
        /// pairwise adjacency is ever built. Cost per node per sweep is O(that node's own degree),
        /// not O(hyperedge size squared). This is the only "count"-weighted tie-strength
        /// convention offered for 2-mode community detection (see the type's remarks on why
        /// "binary" isn't offered here).
        /// </summary>
        private static int[] RunLocalMovingTwoMode(uint[] nodeIds, ILayer layer, double[] degree, double twoM, double resolution)
        {
            int n = nodeIds.Length;
            var idx = new Dictionary<uint, int>(n);
            for (int i = 0; i < n; i++) idx[nodeIds[i]] = i;

            int[] community = new int[n];
            for (int i = 0; i < n; i++) community[i] = i;

            var sigmaTot = new double[n];
            for (int i = 0; i < n; i++) sigmaTot[i] = degree[i];

            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Shuffle(order);

            var neighborCommWeight = new Dictionary<int, double>();

            bool improvedThisPass = true;
            while (improvedThisPass)
            {
                improvedThisPass = false;
                foreach (int ui in order)
                {
                    uint u = nodeIds[ui];
                    int currentComm = community[ui];
                    sigmaTot[currentComm] -= degree[ui];

                    neighborCommWeight.Clear();
                    var (alters, weights) = layer.GetNodeAltersWithWeights(u, EdgeTraversal.Both);
                    var aSpan = alters.Span;
                    var wSpan = weights.Span;
                    for (int k = 0; k < aSpan.Length; k++)
                    {
                        if (!idx.TryGetValue(aSpan[k], out int vi) || vi == ui) continue;
                        double w = wSpan.Length > 0 ? wSpan[k] : 1.0;
                        int c = community[vi];
                        neighborCommWeight[c] = neighborCommWeight.GetValueOrDefault(c) + w;
                    }

                    int bestComm = currentComm;
                    double bestGain = neighborCommWeight.GetValueOrDefault(currentComm)
                        - resolution * sigmaTot[currentComm] * degree[ui] / twoM;

                    foreach (var (c, kIn) in neighborCommWeight)
                    {
                        if (c == currentComm) continue;
                        double gain = kIn - resolution * sigmaTot[c] * degree[ui] / twoM;
                        if (gain > bestGain + Epsilon)
                        {
                            bestGain = gain;
                            bestComm = c;
                        }
                    }

                    sigmaTot[bestComm] += degree[ui];
                    if (bestComm != currentComm)
                    {
                        community[ui] = bestComm;
                        improvedThisPass = true;
                    }
                }
            }
            return community;
        }

        /// <summary>
        /// Builds the first coarse (community-level) graph directly from a 2-mode layer's
        /// hyperedges and a level-0 community assignment (from <see cref="RunLocalMovingTwoMode"/>),
        /// in a single pass over <see cref="ILayerTwoMode.GetAllHyperedgeData"/>: for each
        /// hyperedge, build its community histogram (how many of its members are currently in each
        /// community - O(hyperedge size)), then add each pair of distinct represented communities'
        /// mi*mj product as their mutual coarse edge weight, and mc*(mc-1) as a community's own
        /// self-weight (the same ordered-pair-doubling convention <see cref="BuildAdjacency"/> uses
        /// for an explicit self-tie). Cost is O(Σh (distinct communities in h)²) - at most the
        /// O(Σk_h²) a full projection would cost, and in practice far less once level-0
        /// local-moving has already clustered most of a hyperedge's members together - paid once,
        /// not per sweep or per level. From here on, the returned graph is an ordinary 1-mode
        /// community-to-community graph: every subsequent aggregation level
        /// (<see cref="RunLouvainLevelsFrom"/>/<see cref="RunLeidenLevelsFrom"/>) needs no further
        /// hyperedge-awareness.
        /// </summary>
        private static (int[][] neighbors, double[][] weights, double[] degree, int[] mapping) BuildFirstAggregationTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, int[] level0Community)
        {
            int n0 = nodeIds.Length;
            var idx = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++) idx[nodeIds[i]] = i;

            // Compact level0Community's (arbitrary) ids into contiguous 0..C-1.
            var commMap = new Dictionary<int, int>(n0);
            int[] mapping = new int[n0];
            for (int i = 0; i < n0; i++)
            {
                if (!commMap.TryGetValue(level0Community[i], out int c))
                {
                    c = commMap.Count;
                    commMap[level0Community[i]] = c;
                }
                mapping[i] = c;
            }
            int newN = commMap.Count;

            var adjDict = new Dictionary<int, double>[newN];
            for (int i = 0; i < newN; i++) adjDict[i] = new Dictionary<int, double>();

            var histogram = new Dictionary<int, int>();
            foreach (var (_, memberIds) in layer.GetAllHyperedgeData())
            {
                if (memberIds.Length < 2) continue;
                histogram.Clear();
                foreach (uint m in memberIds)
                {
                    if (!idx.TryGetValue(m, out int mi)) continue;
                    int c = mapping[mi];
                    histogram[c] = histogram.GetValueOrDefault(c) + 1;
                }

                var comms = new List<int>(histogram.Keys);
                for (int a = 0; a < comms.Count; a++)
                {
                    int ca = comms[a];
                    int ma = histogram[ca];
                    if (ma > 1)
                        adjDict[ca][ca] = adjDict[ca].GetValueOrDefault(ca) + (double)ma * (ma - 1);

                    for (int b = a + 1; b < comms.Count; b++)
                    {
                        int cb = comms[b];
                        double w = (double)ma * histogram[cb];
                        adjDict[ca][cb] = adjDict[ca].GetValueOrDefault(cb) + w;
                        adjDict[cb][ca] = adjDict[cb].GetValueOrDefault(ca) + w;
                    }
                }
            }

            var (neighbors, weights) = ToArrays(adjDict, newN);
            double[] degree = new double[newN];
            for (int i = 0; i < newN; i++) degree[i] = SumArray(weights[i]);
            return (neighbors, weights, degree, mapping);
        }

        /// <summary>
        /// Final Q score computed directly from a 2-mode layer's hyperedges and a final community
        /// mapping, via the same per-hyperedge community-histogram trick as
        /// <see cref="BuildFirstAggregationTwoMode"/> (Σ_in(c) accumulated as mc*(mc-1) per
        /// hyperedge) - so scoring the result never needs a materialized level-0 adjacency either.
        /// </summary>
        private static double ComputeModularityTwoMode(uint[] nodeIds, ILayerTwoMode layer, double[] degree0, double twoM, int[] mapping, double resolution)
        {
            int n0 = nodeIds.Length;
            var idx = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++) idx[nodeIds[i]] = i;

            var sigmaTot = new Dictionary<int, double>();
            for (int i = 0; i < n0; i++)
            {
                int c = mapping[i];
                sigmaTot[c] = sigmaTot.GetValueOrDefault(c) + degree0[i];
            }

            var sigmaIn = new Dictionary<int, double>();
            var histogram = new Dictionary<int, int>();
            foreach (var (_, memberIds) in layer.GetAllHyperedgeData())
            {
                if (memberIds.Length < 2) continue;
                histogram.Clear();
                foreach (uint m in memberIds)
                {
                    if (!idx.TryGetValue(m, out int mi)) continue;
                    int c = mapping[mi];
                    histogram[c] = histogram.GetValueOrDefault(c) + 1;
                }
                foreach (var (c, mCount) in histogram)
                    if (mCount > 1)
                        sigmaIn[c] = sigmaIn.GetValueOrDefault(c) + (double)mCount * (mCount - 1);
            }

            double q = 0.0;
            foreach (var (c, stot) in sigmaTot)
            {
                double sin = sigmaIn.GetValueOrDefault(c);
                double frac = stot / twoM;
                q += sin / twoM - resolution * frac * frac;
            }
            return q;
        }

        // ── 2-mode community detection (pseudo-projected, count-weighted tie strength only) ──
        //
        // Louvain/Leiden/LabelPropagation/LPAm's 2-mode entry points below never call
        // BuildAdjacency and never build any pairwise node-to-node structure: level 0 is computed
        // directly from hyperedges (RunLocalMovingTwoMode), and the first aggregation
        // (BuildFirstAggregationTwoMode) turns that into an ordinary, usually much smaller,
        // community-level graph that the existing 1-mode machinery (RunLouvainLevelsFrom/
        // RunLeidenLevelsFrom/LocalMovingPhase/RefinePartition) continues from unchanged. Only
        // "count" (shared-hyperedge-count) tie strength is offered here, unlike assortativity's
        // projection= parameter: a "binary" pair-dedup would need to know individual pair
        // identities globally, which is exactly the O(Σk_h²) pairwise materialization this whole
        // path exists to avoid (count's per-community histograms never need that - see
        // BuildFirstAggregationTwoMode's remarks). CommunityDetectionGirvanNewman has no 2-mode
        // counterpart (edge betweenness has no hyperedge analogue).

        /// <summary>
        /// Louvain (Blondel et al. 2008) for a single 2-mode layer, pseudo-projected: see the
        /// "2-mode community detection" remarks above. Degree and tie strength are both
        /// count-weighted (shared-hyperedge count, matching eigenvectorcentrality()'s existing
        /// convention) - no binary option. Otherwise behaves like <see cref="LouvainCommunities"/>
        /// (same resolution semantics, same numRestarts/best-of-restarts behavior).
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LouvainCommunitiesTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, double resolution = 1.0, int numRestarts = 1)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            var (degree0, twoM) = ComputeDegreeTwoMode(nodeIds, layer);
            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            if (numRestarts <= 1)
                return LouvainSingleRunTwoMode(nodeIds, layer, degree0, twoM, resolution);

            return RunBestOfRestarts(numRestarts,
                () => LouvainSingleRunTwoMode(nodeIds, layer, degree0, twoM, resolution));
        }

        private static (Dictionary<uint, int> communities, double modularity) LouvainSingleRunTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, double[] degree0, double twoM, double resolution)
        {
            int n0 = nodeIds.Length;
            int[] level0Community = RunLocalMovingTwoMode(nodeIds, layer, degree0, twoM, resolution);
            var (neighbors1, weights1, degree1, mapping0) = BuildFirstAggregationTwoMode(nodeIds, layer, level0Community);

            int[] mapping = RunLouvainLevelsFrom(n0, mapping0, neighbors1.Length, neighbors1, weights1, degree1, twoM, resolution);

            var communities = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++) communities[nodeIds[i]] = mapping[i];

            double modularity = ComputeModularityTwoMode(nodeIds, layer, degree0, twoM, mapping, resolution);
            return (communities, modularity);
        }

        /// <summary>
        /// Leiden (Traag, Waltman &amp; van Eck 2019) for a single 2-mode layer, pseudo-projected:
        /// see the "2-mode community detection" remarks above. Level 0 (the hyperedge-direct local
        /// moving pass) has no refinement step - Leiden's connectivity-guaranteeing refinement
        /// only runs from the first aggregation onward, once the graph is an ordinary 1-mode
        /// community-level graph; this is the one deliberate behavioral difference from standard
        /// Leiden, needed to avoid requiring a second hyperedge-direct primitive just for
        /// refinement. Degree and tie strength are both count-weighted - no binary option.
        /// Otherwise behaves like <see cref="LeidenCommunities"/> (same resolution/randomness
        /// semantics, same numRestarts/best-of-restarts behavior).
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LeidenCommunitiesTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, double resolution = 1.0, double randomness = 0.01, int numRestarts = 1)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            randomness = Math.Max(randomness, 1e-6);

            var (degree0, twoM) = ComputeDegreeTwoMode(nodeIds, layer);
            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            if (numRestarts <= 1)
                return LeidenSingleRunTwoMode(nodeIds, layer, degree0, twoM, resolution, randomness);

            return RunBestOfRestarts(numRestarts,
                () => LeidenSingleRunTwoMode(nodeIds, layer, degree0, twoM, resolution, randomness));
        }

        private static (Dictionary<uint, int> communities, double modularity) LeidenSingleRunTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, double[] degree0, double twoM, double resolution, double randomness)
        {
            int n0 = nodeIds.Length;
            int[] level0Community = RunLocalMovingTwoMode(nodeIds, layer, degree0, twoM, resolution);
            var (neighbors1, weights1, degree1, mapping0) = BuildFirstAggregationTwoMode(nodeIds, layer, level0Community);

            int[] mapping = RunLeidenLevelsFrom(n0, mapping0, neighbors1.Length, neighbors1, weights1, degree1, twoM, resolution, randomness, seedCommunity: null);

            var communities = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++) communities[nodeIds[i]] = mapping[i];

            double modularity = ComputeModularityTwoMode(nodeIds, layer, degree0, twoM, mapping, resolution);
            return (communities, modularity);
        }

        /// <summary>
        /// Asynchronous label propagation (Raghavan, Albert &amp; Kumara 2007) for a single 2-mode
        /// layer, pseudo-projected: a structural twin of <see cref="LabelPropagationSingleRun"/>,
        /// but each node's per-label weight is accumulated directly from
        /// <see cref="ILayer.GetNodeAltersWithWeights"/> (count-weighted - no binary option) rather
        /// than a precomputed adjacency. Single-level, like the 1-mode version - no aggregation,
        /// so no coarse-graph construction is needed at all. Otherwise behaves like
        /// <see cref="LabelPropagationCommunities"/> (modularity reported for comparability only,
        /// same numRestarts/best-of-restarts behavior).
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LabelPropagationCommunitiesTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, int maxIterations = 100, int numRestarts = 1)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            var (degree0, twoM) = ComputeDegreeTwoMode(nodeIds, layer);
            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            if (numRestarts <= 1)
                return LabelPropagationSingleRunTwoMode(nodeIds, layer, degree0, twoM, maxIterations);

            return RunBestOfRestarts(numRestarts,
                () => LabelPropagationSingleRunTwoMode(nodeIds, layer, degree0, twoM, maxIterations));
        }

        private static (Dictionary<uint, int> communities, double modularity) LabelPropagationSingleRunTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, double[] degree0, double twoM, int maxIterations)
        {
            int n0 = nodeIds.Length;
            var idx = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++) idx[nodeIds[i]] = i;

            int[] label = new int[n0];
            for (int i = 0; i < n0; i++) label[i] = i;

            int[] order = new int[n0];
            for (int i = 0; i < n0; i++) order[i] = i;

            var weightByLabel = new Dictionary<int, double>();
            var bestLabels = new List<int>();

            for (int pass = 0; pass < maxIterations; pass++)
            {
                Shuffle(order);
                bool changed = false;

                foreach (int ui in order)
                {
                    uint u = nodeIds[ui];
                    var (alters, weights) = layer.GetNodeAltersWithWeights(u, EdgeTraversal.Both);
                    if (alters.Length == 0) continue;
                    var aSpan = alters.Span;
                    var wSpan = weights.Span;

                    weightByLabel.Clear();
                    for (int k = 0; k < aSpan.Length; k++)
                    {
                        if (!idx.TryGetValue(aSpan[k], out int vi)) continue;
                        double w = wSpan.Length > 0 ? wSpan[k] : 1.0;
                        int l = label[vi];
                        weightByLabel[l] = weightByLabel.GetValueOrDefault(l) + w;
                    }
                    if (weightByLabel.Count == 0) continue;

                    double maxWeight = double.NegativeInfinity;
                    foreach (double w in weightByLabel.Values)
                        if (w > maxWeight) maxWeight = w;

                    bestLabels.Clear();
                    foreach (var (l, w) in weightByLabel)
                        if (w >= maxWeight - Epsilon) bestLabels.Add(l);

                    int newLabel = bestLabels[Misc.Random.Next(bestLabels.Count)];
                    if (newLabel != label[ui])
                    {
                        label[ui] = newLabel;
                        changed = true;
                    }
                }

                if (!changed) break;
            }

            var labelMap = new Dictionary<int, int>();
            var communities = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++)
            {
                if (!labelMap.TryGetValue(label[i], out int c))
                {
                    c = labelMap.Count;
                    labelMap[label[i]] = c;
                }
                communities[nodeIds[i]] = c;
            }

            int[] mapping = new int[n0];
            for (int i = 0; i < n0; i++) mapping[i] = labelMap[label[i]];
            double modularity = ComputeModularityTwoMode(nodeIds, layer, degree0, twoM, mapping, 1.0);
            return (communities, modularity);
        }

        /// <summary>
        /// LPAm (Barber &amp; Clark 2009) for a single 2-mode layer, pseudo-projected: a single
        /// hyperedge-direct local-moving pass (<see cref="RunLocalMovingTwoMode"/>, resolution
        /// fixed at 1.0, matching <see cref="LPAmSingleRun"/>'s convention) with no aggregation
        /// afterwards - count-weighted only, no binary option, same as the other 2-mode community
        /// detection methods. Otherwise behaves like <see cref="LPAmCommunities"/> (same
        /// numRestarts/best-of-restarts behavior).
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LPAmCommunitiesTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, int numRestarts = 1)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            var (degree0, twoM) = ComputeDegreeTwoMode(nodeIds, layer);
            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            if (numRestarts <= 1)
                return LPAmSingleRunTwoMode(nodeIds, layer, degree0, twoM);

            return RunBestOfRestarts(numRestarts,
                () => LPAmSingleRunTwoMode(nodeIds, layer, degree0, twoM));
        }

        private static (Dictionary<uint, int> communities, double modularity) LPAmSingleRunTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, double[] degree0, double twoM)
        {
            int n0 = nodeIds.Length;
            int[] community = RunLocalMovingTwoMode(nodeIds, layer, degree0, twoM, 1.0);

            var commMap = new Dictionary<int, int>(n0);
            var communities = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++)
            {
                if (!commMap.TryGetValue(community[i], out int c))
                {
                    c = commMap.Count;
                    commMap[community[i]] = c;
                }
                communities[nodeIds[i]] = c;
            }

            int[] mapping = new int[n0];
            for (int i = 0; i < n0; i++) mapping[i] = commMap[community[i]];
            double modularity = ComputeModularityTwoMode(nodeIds, layer, degree0, twoM, mapping, 1.0);
            return (communities, modularity);
        }

        #endregion
    }
}