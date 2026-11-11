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
            uint[] nodeIds, List<ILayerOneMode> layers, double resolution = 1.0, double randomness = 0.01, int numRestarts = 1, int maxSweeps = 0)
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
                return LeidenSingleRun(nodeIds, neighbors0, weights0, degree0, twoM, resolution, randomness, maxSweeps);

            return RunBestOfRestarts(numRestarts,
                () => LeidenSingleRun(nodeIds, neighbors0, weights0, degree0, twoM, resolution, randomness, maxSweeps));
        }

        /// <summary>
        /// A single Leiden pass: multi-level local-moving + refinement + aggregation starting from
        /// the given (shared, never mutated in place) level-0 adjacency. Safe to call concurrently
        /// for independent restarts against the same arrays.
        /// </summary>
        private static (Dictionary<uint, int> communities, double modularity) LeidenSingleRun(
            uint[] nodeIds, int[][] neighbors0, double[][] weights0, double[] degree0, double twoM, double resolution, double randomness, int maxSweeps)
        {
            int n0 = nodeIds.Length;
            int[] identity = new int[n0];
            for (int i = 0; i < n0; i++) identity[i] = i;

            int[] mapping = RunLeidenLevelsFrom(n0, identity, n0, neighbors0, weights0, degree0, twoM, resolution, randomness, seedCommunity: null, maxSweeps: maxSweeps);

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
            double randomness, int[]? seedCommunity, int maxSweeps = 0)
        {
            int[] mapping = (int[])initialMapping.Clone();

            while (true)
            {
                int[] community = new int[curN];
                if (seedCommunity == null)
                    for (int i = 0; i < curN; i++) community[i] = i;
                else
                    Array.Copy(seedCommunity, community, curN);

                bool improved = LocalMovingPhase(curN, curNeighbors, curWeights, curDegree, twoM, resolution, community, maxSweeps);
                int[] refined = RefinePartition(curN, curNeighbors, curWeights, curDegree, twoM, resolution, community, randomness);

                var (commMap, newN) = RenumberByFirstAppearance(refined);

                for (int i = 0; i < n0; i++)
                    mapping[i] = commMap[refined[mapping[i]]];

                if (!improved || newN == curN)
                    break;

                int[] superOf = new int[curN];
                for (int u = 0; u < curN; u++) superOf[u] = commMap[refined[u]];

                // Seed the next level's local-moving from the P-groups computed at this level
                // (every super-node's members share one P value, since refinement never crosses
                // a P boundary), so aggregation continues from Leiden's own prior progress instead
                // of restarting from singletons — same efficiency/quality trick as the paper.
                // (P-group ids numbered by first appearance along u, as the previous dictionary did)
                var (pGroupToId, _) = RenumberByFirstAppearance(community);
                int[] nextSeed = new int[newN];
                for (int u = 0; u < curN; u++)
                    nextSeed[superOf[u]] = pGroupToId[community[u]];

                (curNeighbors, curWeights) = Aggregate(curN, curNeighbors, curWeights, superOf, newN);
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
            uint[] nodeIds, List<ILayerOneMode> layers, double resolution = 1.0, int numRestarts = 1, int maxSweeps = 0)
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
                return LouvainSingleRun(nodeIds, neighbors0, weights0, degree0, twoM, resolution, maxSweeps);

            return RunBestOfRestarts(numRestarts,
                () => LouvainSingleRun(nodeIds, neighbors0, weights0, degree0, twoM, resolution, maxSweeps));
        }

        /// <summary>
        /// A single Louvain pass: multi-level local-moving + aggregation starting from the given
        /// (shared, never mutated in place — each aggregation level builds brand-new arrays) level-0
        /// adjacency. Safe to call concurrently for independent restarts against the same arrays.
        /// </summary>
        private static (Dictionary<uint, int> communities, double modularity) LouvainSingleRun(
            uint[] nodeIds, int[][] neighbors0, double[][] weights0, double[] degree0, double twoM, double resolution, int maxSweeps)
        {
            int n0 = nodeIds.Length;
            int[] identity = new int[n0];
            for (int i = 0; i < n0; i++) identity[i] = i;

            int[] mapping = RunLouvainLevelsFrom(n0, identity, n0, neighbors0, weights0, degree0, twoM, resolution, maxSweeps: maxSweeps);

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
            int[][] curNeighbors, double[][] curWeights, double[] curDegree, double twoM, double resolution, bool parallel = false, int maxSweeps = 0)
        {
            int[] mapping = (int[])initialMapping.Clone();

            while (true)
            {
                int[] community = new int[curN];
                for (int i = 0; i < curN; i++) community[i] = i;

                bool improved = parallel
                    ? ParallelLocalMovingPhase(curN, curNeighbors, curWeights, curDegree, twoM, resolution, community, maxSweeps)
                    : LocalMovingPhase(curN, curNeighbors, curWeights, curDegree, twoM, resolution, community, maxSweeps);

                var (commMap, newN) = RenumberByFirstAppearance(community);

                for (int i = 0; i < n0; i++)
                    mapping[i] = commMap[community[mapping[i]]];

                if (!improved || newN == curN)
                    break;

                int[] superOf = new int[curN];
                for (int u = 0; u < curN; u++) superOf[u] = commMap[community[u]];
                (curNeighbors, curWeights) = Aggregate(curN, curNeighbors, curWeights, superOf, newN);
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

            var weightByLabel = new WeightAccumulator(n0);
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
                        weightByLabel.Add(label[nbrs[k]], wts[k]);

                    double maxWeight = double.NegativeInfinity;
                    for (int t = 0; t < weightByLabel.Count; t++)
                    {
                        double w = weightByLabel[weightByLabel.KeyAt(t)];
                        if (w > maxWeight) maxWeight = w;
                    }

                    bestLabels.Clear();
                    for (int t = 0; t < weightByLabel.Count; t++)
                    {
                        int l = weightByLabel.KeyAt(t);
                        if (weightByLabel[l] >= maxWeight - Epsilon) bestLabels.Add(l);
                    }

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

            var candidateWeight = new WeightAccumulator(n);
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
                    candidateWeight.Add(refined[v], wts[k]);
                }
                if (candidateWeight.Count == 0) continue; // no eligible neighbor: stays singleton

                sigmaTot[u] -= degree[u];

                candidateIds.Clear();
                candidateGains.Clear();
                double bestGain = 0.0; // staying singleton has gain 0 by definition
                for (int t = 0; t < candidateWeight.Count; t++)
                {
                    int c = candidateWeight.KeyAt(t);
                    double gain = candidateWeight[c] - resolution * sigmaTot[c] * degree[u] / twoM;
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
        /// Reusable "weight per key" accumulator replacing a Dictionary&lt;int, double&gt; that is cleared
        /// and refilled for every node: a weight array indexed by key, plus the keys touched since the
        /// last <see cref="Clear"/> in first-touched order. A dictionary that is only ever cleared and
        /// added to enumerates in insertion order, so iterating <see cref="Keys"/> visits keys in exactly
        /// the order the dictionary did, and each key's weight is summed in the same order - results,
        /// including tie-breaks, are identical. Keys must be in 0 .. capacity-1. Touched-ness is tracked
        /// with a generation stamp, so zero or negative weights can't hide a key.
        /// </summary>
        private sealed class WeightAccumulator(int capacity)
        {
            private readonly double[] _weight = new double[capacity];
            private readonly int[] _stamp = new int[capacity];
            private readonly int[] _keys = new int[capacity];
            private int _count;
            private int _gen = 1;

            internal int Count => _count;
            internal int KeyAt(int i) => _keys[i];
            internal double this[int key] => _stamp[key] == _gen ? _weight[key] : 0.0;

            internal void Add(int key, double w)
            {
                if (_stamp[key] != _gen)
                {
                    _stamp[key] = _gen;
                    _weight[key] = w;
                    _keys[_count++] = key;
                }
                else
                    _weight[key] += w;
            }

            internal void Clear()
            {
                _count = 0;
                if (++_gen == int.MaxValue)
                {
                    Array.Clear(_stamp);
                    _gen = 1;
                }
            }
        }

        /// <summary>
        /// Renumbers community ids (each in 0 .. ids.Length-1) to 0 .. C-1 in order of first appearance
        /// along <paramref name="ids"/> - the same numbering a first-seen Dictionary&lt;int, int&gt; gives.
        /// Returns the old-id -&gt; new-id map (-1 for unused ids) and C.
        /// </summary>
        private static (int[] map, int count) RenumberByFirstAppearance(int[] ids)
        {
            int[] map = new int[ids.Length];
            Array.Fill(map, -1);
            int count = 0;
            foreach (int id in ids)
                if (map[id] < 0) map[id] = count++;
            return (map, count);
        }

        /// <summary>
        /// Builds the next level's graph by collapsing each community into one node: for super-node cu,
        /// the summed weight to every super-node cv, from all edges (u, v) with u in cu and v in cv.
        /// Produces exactly what the previous Dictionary&lt;int, double&gt;[]-based aggregation produced:
        /// each row's neighbours in the order the dictionary first saw them (members of cu in increasing
        /// node order, then each member's neighbours in order) and each weight summed in that same order.
        /// <paramref name="superOf"/>[u] is u's super-node (0 .. newN-1).
        /// </summary>
        private static (int[][] neighbors, double[][] weights) Aggregate(int curN, int[][] curNeighbors, double[][] curWeights, int[] superOf, int newN)
        {
            // Members of each super-node, in increasing node order (stable counting sort)
            int[] start = new int[newN + 1];
            for (int u = 0; u < curN; u++) start[superOf[u] + 1]++;
            for (int c = 0; c < newN; c++) start[c + 1] += start[c];
            int[] members = new int[curN];
            int[] fill = (int[])start.Clone();
            for (int u = 0; u < curN; u++) members[fill[superOf[u]]++] = u;

            var acc = new WeightAccumulator(newN);
            var neighbors = new int[newN][];
            var weights = new double[newN][];
            for (int cu = 0; cu < newN; cu++)
            {
                acc.Clear();
                for (int m = start[cu]; m < start[cu + 1]; m++)
                {
                    int u = members[m];
                    var nbrs = curNeighbors[u];
                    var wts = curWeights[u];
                    for (int k = 0; k < nbrs.Length; k++)
                        acc.Add(superOf[nbrs[k]], wts[k]);
                }
                var nb = new int[acc.Count];
                var wt = new double[acc.Count];
                for (int i = 0; i < acc.Count; i++)
                {
                    nb[i] = acc.KeyAt(i);
                    wt[i] = acc[nb[i]];
                }
                neighbors[cu] = nb;
                weights[cu] = wt;
            }
            return (neighbors, weights);
        }

        /// <summary>
        /// Repeatedly sweeps all nodes (in random order) moving each to whichever neighboring
        /// community (including its own) yields the greatest modularity gain, until a full sweep
        /// produces no moves - or, if <paramref name="maxSweeps"/> is positive, until that many sweeps
        /// have run (the user-facing 'maxsweeps' option: on weakly structured networks local moving
        /// can spend most of its time in long tails of sweeps that move only a few nodes each; stopping
        /// early trades a little modularity for speed). 0 means no limit, the default and the original
        /// behaviour. Returns whether any node ever moved.
        /// </summary>
        private static bool LocalMovingPhase(int n, int[][] neighbors, double[][] weights,
            double[] degree, double twoM, double resolution, int[] community, int maxSweeps = 0)
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
            var neighborCommWeight = new WeightAccumulator(n);

            bool improvedThisPass = true;
            int sweeps = 0;
            while (improvedThisPass && (maxSweeps <= 0 || sweeps++ < maxSweeps))
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
                        neighborCommWeight.Add(community[v], wts[k]);
                    }

                    int bestComm = currentComm;
                    double bestGain = neighborCommWeight[currentComm]
                        - resolution * sigmaTot[currentComm] * degree[u] / twoM;

                    for (int t = 0; t < neighborCommWeight.Count; t++)
                    {
                        int c = neighborCommWeight.KeyAt(t);
                        if (c == currentComm) continue;
                        double kIn = neighborCommWeight[c];
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
            uint[] nodeIds, List<ILayerOneMode> layers, int numRestarts = 1, int maxSweeps = 0)
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
                return LPAmSingleRun(nodeIds, neighbors, weights, degree, twoM, maxSweeps);

            return RunBestOfRestarts(numRestarts,
                () => LPAmSingleRun(nodeIds, neighbors, weights, degree, twoM, maxSweeps));
        }

        /// <summary>
        /// A single LPAm pass against the given (shared, never mutated in place) level-0 adjacency.
        /// Safe to call concurrently for independent restarts against the same arrays.
        /// </summary>
        private static (Dictionary<uint, int> communities, double modularity) LPAmSingleRun(
            uint[] nodeIds, int[][] neighbors, double[][] weights, double[] degree, double twoM, int maxSweeps)
        {
            int n0 = nodeIds.Length;

            int[] community = new int[n0];
            for (int i = 0; i < n0; i++) community[i] = i;

            LocalMovingPhase(n0, neighbors, weights, degree, twoM, 1.0, community, maxSweeps);

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
            // Arrays indexed by community id, summed in the same order as the previous dictionaries
            // (communities in order of first appearance along u), so Q is bit-for-bit the same.
            int maxC = 0;
            for (int u = 0; u < n0; u++) if (mapping[u] > maxC) maxC = mapping[u];
            var sigmaTot = new WeightAccumulator(maxC + 1);
            var sigmaIn = new double[maxC + 1];
            for (int u = 0; u < n0; u++)
            {
                int cu = mapping[u];
                sigmaTot.Add(cu, degree0[u]);
                var nbrs = neighbors0[u];
                var wts = weights0[u];
                for (int k = 0; k < nbrs.Length; k++)
                    if (mapping[nbrs[k]] == cu)
                        sigmaIn[cu] += wts[k];
            }

            double q = 0.0;
            for (int t = 0; t < sigmaTot.Count; t++)
            {
                int c = sigmaTot.KeyAt(t);
                double frac = sigmaTot[c] / twoM;
                q += sigmaIn[c] / twoM - resolution * frac * frac;
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
        /// BrandesBetweenness.EdgeBetweenness, just over this module's own
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
            uint[] nodeIds, List<ILayerOneMode> layers, bool forceWeights = false)
        {
            int n = nodeIds.Length;
            var idx = new Dictionary<uint, int>(n);
            for (int i = 0; i < n; i++) idx[nodeIds[i]] = i;

            // A single binary layer without self-ties normally gives every neighbour exactly once with weight
            // 1.0. Then no weight arrays are allocated (a third of the memory: 4 instead of 12 bytes per entry):
            // every row shares one array of ones at least as long as the longest row, which the algorithms
            // only read up to the row's length, and which nothing modifies. Adding 1.0 is exact, so results are
            // identical. If such a layer still yields a repeated neighbour (e.g. a directed layer with ties in
            // both directions, which add up to weight 2), it is rebuilt with real weights.
            bool unitWeights = !forceWeights && layers.Count == 1 && layers[0].IsBinary && !layers[0].Selfties;

            if (unitWeights && TryBuildUnitRowsFromSortedPackedLayer(nodeIds, layers[0], idx) is int[][] direct)
                return UnitWeightResult(direct);

            // Two passes over the same edge stream: the first counts each node's contributions, the second
            // writes them, in order, into rows of exactly that size. Each row is then merged in place
            // (a neighbour's later contributions are summed into its first slot), so rows end up in
            // exactly the order - and with exactly the sums - the earlier per-node Dictionary<int, double>
            // gave (insertion-ordered, summed in arrival order), without that dictionary's ~40+ bytes per
            // entry and resize garbage, and without holding dictionaries and arrays at the same time.
            int[] count = new int[n];
            ScanAdjacencyContributions(nodeIds, layers, idx, count, null, null);
            var neighbors = new int[n][];
            double[][]? weights = unitWeights ? null : new double[n][];
            int maxRow = 0;
            for (int i = 0; i < n; i++)
            {
                neighbors[i] = count[i] == 0 ? [] : new int[count[i]];
                if (weights != null)
                    weights[i] = count[i] == 0 ? [] : new double[count[i]];
                if (count[i] > maxRow) maxRow = count[i];
            }
            Array.Clear(count);
            ScanAdjacencyContributions(nodeIds, layers, idx, count, neighbors, weights);

            int[] stamp = new int[n];
            int[] slot = new int[n];
            for (int u = 0; u < n; u++)
            {
                int[] nb = neighbors[u];
                double[]? wt = weights?[u];
                int gen = u + 1, len = 0;
                for (int k = 0; k < nb.Length; k++)
                {
                    int v = nb[k];
                    if (stamp[v] != gen)
                    {
                        stamp[v] = gen;
                        slot[v] = len;
                        nb[len] = v;
                        if (wt != null)
                            wt[len] = 0.0 + wt[k]; // as GetValueOrDefault(v) + w did on first sight
                        len++;
                    }
                    else if (wt != null)
                        wt[slot[v]] += wt[k];
                    else
                        return BuildAdjacency(nodeIds, layers, forceWeights: true); // a repeated neighbour: needs real weights
                }
                if (len < nb.Length)
                {
                    Array.Resize(ref neighbors[u], len);
                    Array.Resize(ref weights![u], len);
                }
            }

            if (weights == null)
                return UnitWeightResult(neighbors);
            double[] degree = new double[n];
            for (int i = 0; i < n; i++) degree[i] = SumArray(weights[i]);
            double twoM = SumArray(degree);
            return (neighbors, weights, degree, twoM);
        }

        /// <summary>
        /// Completes a unit-weight adjacency: every row shares one array of ones as long as the longest row,
        /// and each degree is its row length (exactly the sum of its 1.0 weights).
        /// </summary>
        private static (int[][] neighbors, double[][] weights, double[] degree, double twoM) UnitWeightResult(int[][] neighbors)
        {
            int n = neighbors.Length, maxRow = 0;
            foreach (var row in neighbors) if (row.Length > maxRow) maxRow = row.Length;
            var ones = new double[maxRow];
            Array.Fill(ones, 1.0);
            var weights = new double[n][];
            double[] degree = new double[n];
            for (int i = 0; i < n; i++)
            {
                weights[i] = ones;
                degree[i] = neighbors[i].Length;
            }
            return (neighbors, weights, degree, SumArray(degree));
        }

        /// <summary>
        /// Fast path for the most common input, a single packed undirected binary layer without self-ties: when
        /// the layer's node map enumerates egos in ascending id order and every neighbour list is strictly
        /// ascending (checked here), the rows the general two-pass build produces are exactly each node's
        /// neighbour list in that order (node u first receives the egos e &lt; u it is tied to, in ascending
        /// order, then its own neighbours v &gt; u in ascending order), so they can be built directly and in
        /// parallel. Returns null if the conditions do not hold, and the caller falls back to the general build.
        /// </summary>
        private static int[][]? TryBuildUnitRowsFromSortedPackedLayer(uint[] nodeIds, ILayerOneMode layer, Dictionary<uint, int> idx)
        {
            if (layer is not LayerOneModeStatic packed || !packed.IsSymmetric)
                return null;
            int[] offsets = packed.CsrOffsets;
            uint[] nbrIds = packed.CsrNeighborNodeIds;
            var mapper = packed.NodeIdToIndexMapper;
            bool first = true;
            uint previous = 0;
            foreach (var (egoId, li) in mapper)
            {
                if (!first && egoId <= previous) return null;
                first = false;
                previous = egoId;
            }
            int n = nodeIds.Length;
            int[]? direct = null;
            uint maxId = 0;
            foreach (uint id in nodeIds) if (id > maxId) maxId = id;
            if ((long)maxId + 1 <= 4L * n + (1 << 20))
            {
                direct = new int[maxId + 1];
                Array.Fill(direct, -1);
                for (int i = 0; i < n; i++) direct[nodeIds[i]] = i;
            }
            int IndexOf(uint v) => direct != null ? (v < (uint)direct.Length ? direct[v] : -1) : (idx.TryGetValue(v, out int i) ? i : -1);

            var rows = new int[n][];
            bool unsorted = false;
            System.Threading.Tasks.Parallel.For(0, n, UserSettings.GetParallelOptions(), (ui, state) =>
            {
                if (!mapper.TryGetValue(nodeIds[ui], out int li)) { rows[ui] = []; return; }
                int count = 0;
                for (int j = offsets[li]; j < offsets[li + 1]; j++)
                {
                    if (j > offsets[li] && nbrIds[j] <= nbrIds[j - 1]) { Volatile.Write(ref unsorted, true); state.Stop(); return; }
                    if (IndexOf(nbrIds[j]) >= 0) count++;
                }
                var row = count == 0 ? [] : new int[count];
                int k = 0;
                for (int j = offsets[li]; j < offsets[li + 1]; j++)
                {
                    int vi = IndexOf(nbrIds[j]);
                    if (vi >= 0) row[k++] = vi;
                }
                rows[ui] = row;
            });
            return unsorted ? null : rows;
        }

        /// <summary>
        /// Walks every symmetrized contribution <see cref="BuildAdjacency"/> makes, in its fixed order:
        /// per layer, each edge (u, v) from GetAllEgoData adds w to u's row and then to v's row, and on a
        /// symmetric layer with self-ties each node's self-tie then adds 2w to its own row. With
        /// <paramref name="neighbors"/> null it only counts per row (in <paramref name="pos"/>);
        /// otherwise it writes each contribution at the row's next position.
        /// </summary>
        private static void ScanAdjacencyContributions(uint[] nodeIds, List<ILayerOneMode> layers, Dictionary<uint, int> idx,
            int[] pos, int[][]? neighbors, double[][]? weights)
        {
            void Add(int u, int v, double w)
            {
                int p = pos[u]++;
                if (neighbors != null)
                {
                    neighbors[u][p] = v;
                    if (weights != null)
                        weights[u][p] = w;
                }
            }

            foreach (var layer in layers)
            {
                if (layer is LayerOneModeStatic packed)
                {
                    // Same edges in the same order as packed.GetAllEgoData() yields them (egos in mapper
                    // order; on a symmetric layer only alters with a larger id), read straight from the
                    // layer's CSR arrays instead of through a fresh list and array per ego.
                    int[] offsets = packed.CsrOffsets;
                    uint[] nbrIds = packed.CsrNeighborNodeIds;
                    float[]? vals = packed.CsrValues;
                    bool symmetric = packed.IsSymmetric;
                    // Direct id -> index array when ids are dense enough (one array read per edge instead of a
                    // dictionary lookup); same results, as it maps exactly like idx
                    int[]? direct = null;
                    uint maxId = 0;
                    foreach (uint id in nodeIds) if (id > maxId) maxId = id;
                    if ((long)maxId + 1 <= 4L * nodeIds.Length + (1 << 20))
                    {
                        direct = new int[maxId + 1];
                        Array.Fill(direct, -1);
                        for (int i = 0; i < nodeIds.Length; i++) direct[nodeIds[i]] = i;
                    }
                    foreach (var (egoId, li) in packed.NodeIdToIndexMapper)
                    {
                        int ui;
                        if (direct != null) { ui = egoId < (uint)direct.Length ? direct[egoId] : -1; if (ui < 0) continue; }
                        else if (!idx.TryGetValue(egoId, out ui)) continue;
                        for (int j = offsets[li]; j < offsets[li + 1]; j++)
                        {
                            uint alter = nbrIds[j];
                            if (symmetric && alter <= egoId) continue;
                            int vi;
                            if (direct != null) { vi = alter < (uint)direct.Length ? direct[alter] : -1; if (vi < 0) continue; }
                            else if (!idx.TryGetValue(alter, out vi)) continue;
                            double w = vals != null ? vals[j] : 1.0;
                            Add(ui, vi, w);
                            Add(vi, ui, w);
                        }
                    }
                }
                else
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
                            Add(ui, vi, w);
                            Add(vi, ui, w);
                        }
                    }

                // GetAllEgoData() never yields self-loops for symmetric layers: add them explicitly,
                // counted twice per the standard undirected-graph degree convention (same as a normal
                // discovered edge would be, which contributes to the row from both "sides").
                if (!layer.IsDirectional && layer.Selfties)
                    foreach (uint nodeId in nodeIds)
                    {
                        if (!idx.TryGetValue(nodeId, out int ui)) continue;
                        if (!layer.CheckEdgeExists(nodeId, nodeId)) continue;
                        double w = layer.GetEdgeValue(nodeId, nodeId);
                        Add(ui, ui, 2 * w);
                    }
            }
        }

        /// <summary>
        /// A 2-mode layer's node-hyperedge incidence, re-indexed once per community-detection call into the
        /// call's compact node numbering (positions in its nodeIds array), as plain int arrays: node i's
        /// hyperedges are NodeHyperedges[NodeOffsets[i] .. NodeOffsets[i+1]), and hyperedge h's members are
        /// Members[HyperedgeOffsets[h] .. HyperedgeOffsets[h+1]) (compact indices, -1 for a member outside
        /// nodeIds). Both lists keep exactly the order the layer's own GetNodeAltersWithWeights walks them
        /// in (a node's hyperedges in the layer's order, then each hyperedge's members in order), so the
        /// local-moving and label-propagation loops below see co-members - and therefore first encounter
        /// communities and break ties - exactly as before. Also holds each node's count-weighted degree
        /// (the sum of its shared-hyperedge counts with every co-member, i.e. sum over its hyperedges of the
        /// number of other members), computed in one linear pass instead of one quadratic co-member
        /// dictionary per node. Read-only once built, so restarts and parallel workers can share it.
        /// </summary>
        internal sealed class TwoModeIncidence
        {
            internal int[] NodeOffsets = [];
            internal int[] NodeHyperedges = [];
            internal int[] HyperedgeOffsets = [];
            internal int[] Members = [];
            internal double[] Degree = [];
            internal double TwoM;

            internal static TwoModeIncidence Build(uint[] nodeIds, ILayerTwoMode layer)
            {
                int n = nodeIds.Length;
                var idx = new Dictionary<uint, int>(n);
                for (int i = 0; i < n; i++) idx[nodeIds[i]] = i;

                var nodeOffsets = new int[n + 1];
                var nodeHyperedges = new List<int>();
                var hyperedgeOffsets = new List<int> { 0 };
                var members = new List<int>();

                if (layer is LayerTwoModeStatic st)
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (st.TryGetNodeHyperedgeRange(nodeIds[i], out int s, out int e))
                            for (int k = s; k < e; k++)
                                nodeHyperedges.Add(st.GetNodeHyperedgeIndex(k));
                        nodeOffsets[i + 1] = nodeHyperedges.Count;
                    }
                    int h = (int)st.NbrHyperedges;
                    for (int he = 0; he < h; he++)
                    {
                        st.GetHyperedgeRange(he, out int s, out int e);
                        for (int j = s; j < e; j++)
                            members.Add(idx.TryGetValue(st.GetHyperedgeNodeAt(j), out int m) ? m : -1);
                        hyperedgeOffsets.Add(members.Count);
                    }
                }
                else if (layer is LayerTwoMode dyn)
                {
                    // Hyperedges are numbered in the order nodes first reach them
                    var heIndex = new Dictionary<Hyperedge, int>(ReferenceEqualityComparer.Instance);
                    for (int i = 0; i < n; i++)
                    {
                        if (dyn.GetNonEmptyHyperedgeCollection(nodeIds[i]) is HyperedgeCollection hec)
                            foreach (Hyperedge he in hec.HyperEdges)
                            {
                                if (!heIndex.TryGetValue(he, out int hIdx))
                                {
                                    hIdx = heIndex.Count;
                                    heIndex[he] = hIdx;
                                    foreach (uint m in he.NodeIds)
                                        members.Add(idx.TryGetValue(m, out int mi) ? mi : -1);
                                    hyperedgeOffsets.Add(members.Count);
                                }
                                nodeHyperedges.Add(hIdx);
                            }
                        nodeOffsets[i + 1] = nodeHyperedges.Count;
                    }
                }
                else
                    throw new NotSupportedException($"Unsupported 2-mode layer type '{layer.GetType().Name}'.");

                var inc = new TwoModeIncidence
                {
                    NodeOffsets = nodeOffsets,
                    NodeHyperedges = nodeHyperedges.ToArray(),
                    HyperedgeOffsets = hyperedgeOffsets.ToArray(),
                    Members = members.ToArray()
                };
                inc.ComputeDegrees(n);
                return inc;
            }

            /// <summary>
            /// Degree[i] = sum over node i's hyperedge entries of (members - occurrences of i itself): exactly
            /// the sum of the co-member weights GetNodeAltersWithWeights returns. Linear in the incidence size;
            /// a hyperedge listing the same node more than once is handled exactly via a per-hyperedge count,
            /// built only for such hyperedges.
            /// </summary>
            private void ComputeDegrees(int n)
            {
                int hCount = HyperedgeOffsets.Length - 1;
                Dictionary<int, int>[]? selfCounts = null;
                var seen = new HashSet<int>();
                for (int h = 0; h < hCount; h++)
                {
                    seen.Clear();
                    bool dup = false;
                    for (int j = HyperedgeOffsets[h]; j < HyperedgeOffsets[h + 1]; j++)
                        if (Members[j] >= 0 && !seen.Add(Members[j])) { dup = true; break; }
                    if (!dup) continue;
                    selfCounts ??= new Dictionary<int, int>[hCount];
                    var counts = new Dictionary<int, int>();
                    for (int j = HyperedgeOffsets[h]; j < HyperedgeOffsets[h + 1]; j++)
                        if (Members[j] >= 0)
                            counts[Members[j]] = counts.GetValueOrDefault(Members[j]) + 1;
                    selfCounts[h] = counts;
                }

                Degree = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double d = 0.0;
                    for (int k = NodeOffsets[i]; k < NodeOffsets[i + 1]; k++)
                    {
                        int h = NodeHyperedges[k];
                        int size = HyperedgeOffsets[h + 1] - HyperedgeOffsets[h];
                        int self = selfCounts?[h] is Dictionary<int, int> c ? c.GetValueOrDefault(i) : 1;
                        d += size - self;
                    }
                    Degree[i] = d;
                }
                TwoM = SumArray(Degree);
            }

            /// <summary>
            /// Adds node <paramref name="ui"/>'s shared-hyperedge counts with every co-member into
            /// <paramref name="weightBy"/>, keyed by <paramref name="label"/>[co-member], appending each key to
            /// <paramref name="touched"/> the first time it is seen (in the same first-seen order the previous
            /// dictionary-based code enumerated). Returns the number of keys touched. The caller resets
            /// weightBy for the touched keys afterwards.
            /// </summary>
            internal int AccumulateByLabel(int ui, int[] label, double[] weightBy, int[] touched)
            {
                int count = 0;
                for (int k = NodeOffsets[ui], kEnd = NodeOffsets[ui + 1]; k < kEnd; k++)
                {
                    int h = NodeHyperedges[k];
                    for (int j = HyperedgeOffsets[h], jEnd = HyperedgeOffsets[h + 1]; j < jEnd; j++)
                    {
                        int vi = Members[j];
                        if (vi < 0 || vi == ui) continue;
                        int c = label[vi];
                        if (weightBy[c] == 0.0)
                            touched[count++] = c;
                        weightBy[c] += 1.0;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// A single hyperedge-direct local-moving pass over a 2-mode layer: the same
        /// sweep-until-no-moves loop and modularity-gain formula as <see cref="LocalMovingPhase"/>,
        /// but each node's weight toward a candidate community is accumulated directly from the
        /// layer's incidence (<see cref="TwoModeIncidence"/>), grouped by each co-affiliate's *current*
        /// community - so no pairwise adjacency is ever built. Each node's cost per sweep is the total
        /// size of its hyperedges (inherently quadratic in hyperedge size summed over all nodes); the
        /// accumulation uses one reusable per-community array instead of a co-member dictionary per node
        /// per sweep, with identical results. This is the only "count"-weighted tie-strength
        /// convention offered for 2-mode community detection (see the type's remarks on why
        /// "binary" isn't offered here).
        /// </summary>
        private static int[] RunLocalMovingTwoMode(TwoModeIncidence inc, double[] degree, double twoM, double resolution, int maxSweeps = 0)
        {
            int n = degree.Length;

            int[] community = new int[n];
            for (int i = 0; i < n; i++) community[i] = i;

            var sigmaTot = new double[n];
            for (int i = 0; i < n; i++) sigmaTot[i] = degree[i];

            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Shuffle(order);

            var weightBy = new double[n];
            var touched = new int[n];

            bool improvedThisPass = true;
            int sweeps = 0;
            while (improvedThisPass && (maxSweeps <= 0 || sweeps++ < maxSweeps))
            {
                improvedThisPass = false;
                foreach (int ui in order)
                {
                    int currentComm = community[ui];
                    sigmaTot[currentComm] -= degree[ui];

                    int nTouched = inc.AccumulateByLabel(ui, community, weightBy, touched);

                    int bestComm = currentComm;
                    double bestGain = weightBy[currentComm]
                        - resolution * sigmaTot[currentComm] * degree[ui] / twoM;

                    for (int t = 0; t < nTouched; t++)
                    {
                        int c = touched[t];
                        if (c == currentComm) continue;
                        double gain = weightBy[c] - resolution * sigmaTot[c] * degree[ui] / twoM;
                        if (gain > bestGain + Epsilon)
                        {
                            bestGain = gain;
                            bestComm = c;
                        }
                    }
                    for (int t = 0; t < nTouched; t++)
                        weightBy[touched[t]] = 0.0;

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

        // ── Parallel Louvain (PLM) ──────────────────────────────────────────────────────────
        //
        // The parallel Louvain method of Staudt & Meyerhenke (2016, NetworKit's PLM): the same
        // multi-level local moving + aggregation as Louvain, but each local-moving sweep processes
        // nodes in parallel across up to 'maxthreads' threads. All threads read and update one shared
        // community array and one shared per-community degree total (sigmaTot, updated atomically),
        // without locking around a node's whole evaluate-and-move step: a node may decide using a
        // neighbour's community or a community total that another thread changes a moment later.
        // Louvain tolerates this well - the resulting modularity is close to sequential Louvain's -
        // but results depend on thread timing, so they can differ between runs even with the same
        // seed, unless maxthreads is 1. Because simultaneous moves can make nodes swap back and forth,
        // sweeps per level are capped (PlmMaxSweepsPerLevel, as NetworKit caps them). Aggregation and
        // the modularity calculation are the same, sequential, code as Louvain's.

        /// <summary>Upper bound on parallel local-moving sweeps per level (see the section remarks).</summary>
        private const int PlmMaxSweepsPerLevel = 32;

        /// <summary>Atomically adds <paramref name="value"/> to <paramref name="target"/> (compare-and-swap loop).</summary>
        private static void AtomicAdd(ref double target, double value)
        {
            double current = Volatile.Read(ref target);
            while (true)
            {
                double replaced = Interlocked.CompareExchange(ref target, current + value, current);
                if (replaced.Equals(current))
                    return;
                current = replaced;
            }
        }

        /// <summary>Per-thread scratch for a parallel local-moving sweep: per-community weights and the communities touched.</summary>
        private sealed class PlmScratch(int n)
        {
            internal readonly double[] WeightBy = new double[n];
            internal readonly int[] Touched = new int[n];
            // 1-mode only: marks a community as touched for the current node (Mark[c] == Generation), so a
            // zero or negative edge weight can't hide it the way a "weight is still 0" test would
            internal readonly int[] Mark = new int[n];
            internal int Generation;

            /// <summary>Starts a new node's marks (scratches are reused across sweeps, so the counter can grow large).</summary>
            internal int NextGeneration()
            {
                if (++Generation == int.MaxValue)
                {
                    Array.Clear(Mark);
                    Generation = 1;
                }
                return Generation;
            }
        }

        /// <summary>
        /// Hands out <see cref="PlmScratch"/> instances to Parallel.For tasks and takes them back when a task
        /// ends, so one local-moving phase allocates about one scratch per thread in total, instead of a new
        /// one (three n-sized arrays) for every task of every sweep. A scratch is always left clean
        /// (WeightBy all zero, marks older than any future generation), so reuse doesn't change results.
        /// </summary>
        private sealed class PlmScratchPool(int n)
        {
            private readonly System.Collections.Concurrent.ConcurrentBag<PlmScratch> _bag = [];
            internal PlmScratch Rent() => _bag.TryTake(out var s) ? s : new PlmScratch(n);
            internal void Return(PlmScratch s) => _bag.Add(s);
        }

        /// <summary>
        /// One node's PLM decision, shared by the 1-mode and 2-mode sweeps: given its per-community weights
        /// (already accumulated in scratch, for nTouched communities), picks the community with the best
        /// modularity gain - evaluating its own community with itself removed - and, if that differs from its
        /// current one, moves it (atomic sigmaTot updates). Resets the scratch weights. Returns whether it moved.
        /// </summary>
        private static bool PlmDecideAndMove(int u, int nTouched, PlmScratch scratch, double[] degree, double twoM, double resolution, int[] community, double[] sigmaTot)
        {
            double[] weightBy = scratch.WeightBy;
            int[] touched = scratch.Touched;
            int currentComm = Volatile.Read(ref community[u]);
            double dU = degree[u];

            int bestComm = currentComm;
            double bestGain = weightBy[currentComm] - resolution * (Volatile.Read(ref sigmaTot[currentComm]) - dU) * dU / twoM;
            for (int t = 0; t < nTouched; t++)
            {
                int c = touched[t];
                if (c == currentComm) continue;
                double gain = weightBy[c] - resolution * Volatile.Read(ref sigmaTot[c]) * dU / twoM;
                if (gain > bestGain + Epsilon)
                {
                    bestGain = gain;
                    bestComm = c;
                }
            }
            for (int t = 0; t < nTouched; t++)
                weightBy[touched[t]] = 0.0;

            if (bestComm == currentComm)
                return false;
            AtomicAdd(ref sigmaTot[currentComm], -dU);
            AtomicAdd(ref sigmaTot[bestComm], dU);
            Volatile.Write(ref community[u], bestComm);
            return true;
        }

        /// <summary>
        /// Parallel counterpart of <see cref="LocalMovingPhase"/> for PLM: sweeps all nodes in parallel
        /// (in a random order shuffled once, split across threads) until a sweep moves no node or
        /// <see cref="PlmMaxSweepsPerLevel"/> sweeps have run. Returns whether any node ever moved.
        /// </summary>
        private static bool ParallelLocalMovingPhase(int n, int[][] neighbors, double[][] weights,
            double[] degree, double twoM, double resolution, int[] community, int maxSweeps = 0)
        {
            var sigmaTot = new double[n];
            for (int i = 0; i < n; i++) sigmaTot[community[i]] += degree[i];

            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Shuffle(order);

            bool improvedAny = false;
            var pool = new PlmScratchPool(n);
            int sweepCap = maxSweeps > 0 ? maxSweeps : PlmMaxSweepsPerLevel;
            for (int sweep = 0; sweep < sweepCap; sweep++)
            {
                int moved = 0;
                System.Threading.Tasks.Parallel.For(0, n, UserSettings.GetParallelOptions(),
                    () => (Scratch: pool.Rent(), Moved: 0),
                    (i, loopState, local) =>
                    {
                        int u = order[i];
                        var nbrs = neighbors[u];
                        var wts = weights[u];
                        double[] weightBy = local.Scratch.WeightBy;
                        int[] touched = local.Scratch.Touched;
                        int[] mark = local.Scratch.Mark;
                        int gen = local.Scratch.NextGeneration();
                        int nTouched = 0;
                        for (int k = 0; k < nbrs.Length; k++)
                        {
                            int v = nbrs[k];
                            if (v == u) continue; // self-loop: no inter-community linkage signal
                            int c = Volatile.Read(ref community[v]);
                            if (mark[c] != gen)
                            {
                                mark[c] = gen;
                                touched[nTouched++] = c;
                            }
                            weightBy[c] += wts[k];
                        }
                        if (PlmDecideAndMove(u, nTouched, local.Scratch, degree, twoM, resolution, community, sigmaTot))
                            local.Moved++;
                        return local;
                    },
                    local =>
                    {
                        Interlocked.Add(ref moved, local.Moved);
                        pool.Return(local.Scratch);
                    });
                if (moved == 0)
                    break;
                improvedAny = true;
            }
            return improvedAny;
        }

        /// <summary>
        /// Parallel counterpart of <see cref="RunLocalMovingTwoMode"/> for PLM on a 2-mode layer: level 0,
        /// accumulated directly from the layer's incidence, with each thread using its own scratch arrays.
        /// </summary>
        private static int[] ParallelLocalMovingTwoMode(TwoModeIncidence inc, double[] degree, double twoM, double resolution, int maxSweeps = 0)
        {
            int n = degree.Length;
            int[] community = new int[n];
            for (int i = 0; i < n; i++) community[i] = i;
            var sigmaTot = (double[])degree.Clone();

            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Shuffle(order);

            var pool = new PlmScratchPool(n);
            int sweepCap = maxSweeps > 0 ? maxSweeps : PlmMaxSweepsPerLevel;
            for (int sweep = 0; sweep < sweepCap; sweep++)
            {
                int moved = 0;
                System.Threading.Tasks.Parallel.For(0, n, UserSettings.GetParallelOptions(),
                    () => (Scratch: pool.Rent(), Moved: 0),
                    (i, loopState, local) =>
                    {
                        int u = order[i];
                        int nTouched = inc.AccumulateByLabel(u, community, local.Scratch.WeightBy, local.Scratch.Touched);
                        if (PlmDecideAndMove(u, nTouched, local.Scratch, degree, twoM, resolution, community, sigmaTot))
                            local.Moved++;
                        return local;
                    },
                    local =>
                    {
                        Interlocked.Add(ref moved, local.Moved);
                        pool.Return(local.Scratch);
                    });
                if (moved == 0)
                    break;
            }
            return community;
        }

        /// <summary>
        /// Runs <paramref name="numRestarts"/> PLM runs one after another (each run is itself parallel, so
        /// running restarts concurrently would only oversubscribe the threads) and keeps the one with the
        /// highest modularity.
        /// </summary>
        private static (Dictionary<uint, int> communities, double modularity) RunBestOfRestartsSequential(
            int numRestarts, Func<(Dictionary<uint, int> communities, double modularity)> singleRun)
        {
            var best = singleRun();
            for (int r = 1; r < numRestarts; r++)
            {
                var candidate = singleRun();
                if (candidate.modularity > best.modularity)
                    best = candidate;
            }
            return best;
        }

        /// <summary>
        /// Parallel Louvain (PLM, see the section remarks) for 1-mode layers, combined and symmetrized
        /// exactly as in <see cref="LouvainCommunities"/>; same resolution semantics. numRestarts runs
        /// are done one after another, keeping the highest-modularity result.
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) PlmCommunities(
            uint[] nodeIds, List<ILayerOneMode> layers, double resolution = 1.0, int numRestarts = 1, int maxSweeps = 0)
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

            return RunBestOfRestartsSequential(Math.Max(1, numRestarts), () =>
            {
                int[] identity = new int[n0];
                for (int i = 0; i < n0; i++) identity[i] = i;
                int[] mapping = RunLouvainLevelsFrom(n0, identity, n0, neighbors0, weights0, degree0, twoM, resolution, parallel: true, maxSweeps: maxSweeps);
                var communities = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) communities[nodeIds[i]] = mapping[i];
                return (communities, ComputeModularity(n0, neighbors0, weights0, degree0, twoM, resolution, mapping));
            });
        }

        /// <summary>
        /// Parallel Louvain (PLM, see the section remarks) for a single 2-mode layer, pseudo-projected and
        /// count-weighted like <see cref="LouvainCommunitiesTwoMode"/>: level 0 runs in parallel directly
        /// on the layer's incidence, then the first aggregation and all further levels run as in Louvain,
        /// with parallel local moving.
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) PlmCommunitiesTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, double resolution = 1.0, int numRestarts = 1, int maxSweeps = 0)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            var inc = TwoModeIncidence.Build(nodeIds, layer);
            var (degree0, twoM) = (inc.Degree, inc.TwoM);
            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            return RunBestOfRestartsSequential(Math.Max(1, numRestarts), () =>
            {
                int[] level0Community = ParallelLocalMovingTwoMode(inc, degree0, twoM, resolution, maxSweeps);
                var (neighbors1, weights1, degree1, mapping0) = BuildFirstAggregationTwoMode(nodeIds, layer, level0Community);
                int[] mapping = RunLouvainLevelsFrom(n0, mapping0, neighbors1.Length, neighbors1, weights1, degree1, twoM, resolution, parallel: true, maxSweeps: maxSweeps);
                var communities = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) communities[nodeIds[i]] = mapping[i];
                return (communities, ComputeModularityTwoMode(nodeIds, layer, degree0, twoM, mapping, resolution));
            });
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
        // projection= parameter.
        //
        // RunLocalMovingTwoMode itself is NOT the O(Σk_h) "nothing materialized" story a
        // pseudo-projection framing might suggest: it calls GetNodeAltersWithWeights once per
        // node, per sweep, which (confirmed by reading LayerTwoModeStatic.cs/LayerTwoMode.cs
        // directly) is not memoized and rescans a node's full hyperedge-incidence structure on
        // every call - the same O(Σk_h²) aggregate cost degree()/pagerank() carry, repeated every
        // sweep until local moving converges (RunLocalMovingTwoMode's own while(improvedThisPass)
        // loop has no formal bound on sweep count). So level 0's real cost is O(sweeps·Σk_h²), not
        // O(Σk_h); this is why 2-mode community detection does NOT get the same clean asymptotic
        // *time* advantage diameter()/components()/shortestpathsattributes() do, even though it
        // genuinely avoids materializing anything (the memory claim still holds).
        //
        // A hypothetical binary option would cost about the same at local moving (same per-node
        // dedup technique, same order). What it would add is at the AGGREGATION step
        // (BuildFirstAggregationTwoMode) specifically: its per-hyperedge community histogram
        // collapses away individual pair identity, so a correct binary coarse edge weight would
        // need one further O(Σk_h²) pass (paid once, not per sweep) to recover cross-hyperedge
        // pair identity - real, but smaller than local moving's own recurring cost, which is paid
        // either way. CommunityDetectionGirvanNewman has no 2-mode counterpart (edge betweenness
        // has no hyperedge analogue).

        /// <summary>
        /// Louvain (Blondel et al. 2008) for a single 2-mode layer, pseudo-projected: see the
        /// "2-mode community detection" remarks above. Degree and tie strength are both
        /// count-weighted (shared-hyperedge count, matching eigenvectorcentrality()'s existing
        /// convention) - no binary option. Otherwise behaves like <see cref="LouvainCommunities"/>
        /// (same resolution semantics, same numRestarts/best-of-restarts behavior).
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LouvainCommunitiesTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, double resolution = 1.0, int numRestarts = 1, int maxSweeps = 0)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            var inc = TwoModeIncidence.Build(nodeIds, layer);
            var (degree0, twoM) = (inc.Degree, inc.TwoM);
            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            if (numRestarts <= 1)
                return LouvainSingleRunTwoMode(nodeIds, layer, inc, degree0, twoM, resolution, maxSweeps);

            return RunBestOfRestarts(numRestarts,
                () => LouvainSingleRunTwoMode(nodeIds, layer, inc, degree0, twoM, resolution, maxSweeps));
        }

        private static (Dictionary<uint, int> communities, double modularity) LouvainSingleRunTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, TwoModeIncidence inc, double[] degree0, double twoM, double resolution, int maxSweeps)
        {
            int n0 = nodeIds.Length;
            int[] level0Community = RunLocalMovingTwoMode(inc, degree0, twoM, resolution, maxSweeps);
            var (neighbors1, weights1, degree1, mapping0) = BuildFirstAggregationTwoMode(nodeIds, layer, level0Community);

            int[] mapping = RunLouvainLevelsFrom(n0, mapping0, neighbors1.Length, neighbors1, weights1, degree1, twoM, resolution, maxSweeps: maxSweeps);

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
            uint[] nodeIds, ILayerTwoMode layer, double resolution = 1.0, double randomness = 0.01, int numRestarts = 1, int maxSweeps = 0)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            randomness = Math.Max(randomness, 1e-6);

            var inc = TwoModeIncidence.Build(nodeIds, layer);
            var (degree0, twoM) = (inc.Degree, inc.TwoM);
            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            if (numRestarts <= 1)
                return LeidenSingleRunTwoMode(nodeIds, layer, inc, degree0, twoM, resolution, randomness, maxSweeps);

            return RunBestOfRestarts(numRestarts,
                () => LeidenSingleRunTwoMode(nodeIds, layer, inc, degree0, twoM, resolution, randomness, maxSweeps));
        }

        private static (Dictionary<uint, int> communities, double modularity) LeidenSingleRunTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, TwoModeIncidence inc, double[] degree0, double twoM, double resolution, double randomness, int maxSweeps)
        {
            int n0 = nodeIds.Length;
            int[] level0Community = RunLocalMovingTwoMode(inc, degree0, twoM, resolution, maxSweeps);
            var (neighbors1, weights1, degree1, mapping0) = BuildFirstAggregationTwoMode(nodeIds, layer, level0Community);

            int[] mapping = RunLeidenLevelsFrom(n0, mapping0, neighbors1.Length, neighbors1, weights1, degree1, twoM, resolution, randomness, seedCommunity: null, maxSweeps: maxSweeps);

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

            var inc = TwoModeIncidence.Build(nodeIds, layer);
            var (degree0, twoM) = (inc.Degree, inc.TwoM);
            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            if (numRestarts <= 1)
                return LabelPropagationSingleRunTwoMode(nodeIds, layer, inc, degree0, twoM, maxIterations);

            return RunBestOfRestarts(numRestarts,
                () => LabelPropagationSingleRunTwoMode(nodeIds, layer, inc, degree0, twoM, maxIterations));
        }

        private static (Dictionary<uint, int> communities, double modularity) LabelPropagationSingleRunTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, TwoModeIncidence inc, double[] degree0, double twoM, int maxIterations)
        {
            int n0 = nodeIds.Length;

            int[] label = new int[n0];
            for (int i = 0; i < n0; i++) label[i] = i;

            int[] order = new int[n0];
            for (int i = 0; i < n0; i++) order[i] = i;

            // Per-label weights in a reusable array (reset after each node), labels in first-seen order
            var weightByLabel = new double[n0];
            var touched = new int[n0];
            var bestLabels = new List<int>();

            for (int pass = 0; pass < maxIterations; pass++)
            {
                Shuffle(order);
                bool changed = false;

                foreach (int ui in order)
                {
                    int nTouched = inc.AccumulateByLabel(ui, label, weightByLabel, touched);
                    if (nTouched == 0) continue;

                    double maxWeight = double.NegativeInfinity;
                    for (int t = 0; t < nTouched; t++)
                        if (weightByLabel[touched[t]] > maxWeight) maxWeight = weightByLabel[touched[t]];

                    bestLabels.Clear();
                    for (int t = 0; t < nTouched; t++)
                        if (weightByLabel[touched[t]] >= maxWeight - Epsilon) bestLabels.Add(touched[t]);
                    for (int t = 0; t < nTouched; t++)
                        weightByLabel[touched[t]] = 0.0;

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
            uint[] nodeIds, ILayerTwoMode layer, int numRestarts = 1, int maxSweeps = 0)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0);

            var inc = TwoModeIncidence.Build(nodeIds, layer);
            var (degree0, twoM) = (inc.Degree, inc.TwoM);
            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0);
            }

            if (numRestarts <= 1)
                return LPAmSingleRunTwoMode(nodeIds, layer, inc, degree0, twoM, maxSweeps);

            return RunBestOfRestarts(numRestarts,
                () => LPAmSingleRunTwoMode(nodeIds, layer, inc, degree0, twoM, maxSweeps));
        }

        private static (Dictionary<uint, int> communities, double modularity) LPAmSingleRunTwoMode(
            uint[] nodeIds, ILayerTwoMode layer, TwoModeIncidence inc, double[] degree0, double twoM, int maxSweeps)
        {
            int n0 = nodeIds.Length;
            int[] community = RunLocalMovingTwoMode(inc, degree0, twoM, 1.0, maxSweeps);

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