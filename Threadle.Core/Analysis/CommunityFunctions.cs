using Threadle.Core.Model;
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
        /// Louvain modularity optimization (Blondel et al. 2008): multi-level local-moving +
        /// aggregation. Directed layers are symmetrized (arc u→v and v→u both accumulate into an
        /// undirected weight, same convention as <see cref="LocalStructureFunctions.StructuralHoles"/>)
        /// since modularity optimization is inherently an undirected-graph formulation. Multiple
        /// layers combine via summed weight; binary layers contribute 1.0 per edge. Self-ties
        /// contribute twice to a node's degree, per the standard undirected-graph convention.
        /// Returns the community index (0-based, compacted) for every node plus the achieved
        /// modularity Q using the given resolution (1.0 = standard modularity; &gt;1 favors more,
        /// smaller communities; &lt;1 favors fewer, larger ones — Reichardt &amp; Bornholdt 2006).
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LouvainCommunities(
            uint[] nodeIds, List<ILayerOneMode> layers, double resolution = 1.0)
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

            // mapping[i] = the current-level node index that original node i currently belongs to.
            int[] mapping = new int[n0];
            for (int i = 0; i < n0; i++) mapping[i] = i;

            int curN = n0;
            int[][] curNeighbors = neighbors0;
            double[][] curWeights = weights0;
            double[] curDegree = degree0;

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

            var communities = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++) communities[nodeIds[i]] = mapping[i];

            double modularity = ComputeModularity(n0, neighbors0, weights0, degree0, twoM, resolution, mapping);
            return (communities, modularity);
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
        /// results are comparable across community detection methods.
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LabelPropagationCommunities(
            uint[] nodeIds, List<ILayerOneMode> layers, int maxIterations = 100)
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
        /// exactly as in <see cref="LouvainCommunities"/>.
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LPAmCommunities(
            uint[] nodeIds, List<ILayerOneMode> layers)
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
        /// plus the achieved modularity Q.
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity) LeidenCommunities(
            uint[] nodeIds, List<ILayerOneMode> layers, double resolution = 1.0, double randomness = 0.01)
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

            int[] mapping = new int[n0];
            for (int i = 0; i < n0; i++) mapping[i] = i;

            int curN = n0;
            int[][] curNeighbors = neighbors0;
            double[][] curWeights = weights0;
            double[] curDegree = degree0;
            int[]? seedCommunity = null; // null => start level 0 from singletons

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

            var communities = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++) communities[nodeIds[i]] = mapping[i];

            double modularity = ComputeModularity(n0, neighbors0, weights0, degree0, twoM, resolution, mapping);
            return (communities, modularity);
        }

        /// <summary>
        /// Infomap (Rosvall &amp; Bergstrom 2008): multi-level local-moving + aggregation, structurally
        /// identical to <see cref="LouvainCommunities"/>, but every node move is evaluated by its
        /// effect on the two-level map-equation codelength rather than modularity — the expected
        /// number of bits per step needed to describe a random walker's trajectory when nodes are
        /// named within per-module codebooks (reused across modules) plus a shared "which module"
        /// codebook for crossing between them. This asks where flow gets trapped, a genuinely
        /// different structural signal from modularity's "where is edge density higher than a
        /// null-model baseline" — so results, and even the objective's direction, are not directly
        /// comparable: lower Codelength is better, the opposite of Modularity (which is still
        /// reported here too, purely for cross-method comparability, not something this method
        /// optimizes). Like Louvain, this is a greedy local search and can occasionally settle in a
        /// local optimum, particularly on small or highly symmetric structures.
        /// <para/>
        /// Unlike the other three community detection methods, this one accepts 2-mode layers
        /// directly — see <see cref="BuildAdjacencyMixed"/> — because a random walk (unlike
        /// modularity's null model) has a well-defined formulation on hyperedges without ever
        /// materializing a projected layer: from an actor node, a step picks an incident hyperedge
        /// weighted by its (size-1), then a member of that hyperedge other than the ego, exactly
        /// matching <see cref="Threadle.Core.Model.ILayerTwoMode.PickRandomAlterO1"/>'s sampling
        /// rule — so results are the same as if the 2-mode layer had been projected first, without
        /// ever materializing that projection as a stored layer. That said, computing the *exact*
        /// co-membership weight between every pair of members of a hyperedge still costs O(hyperedge
        /// size squared) for that hyperedge — the same cost as materializing the projection, just not
        /// persisted — so this can be expensive for 2-mode layers with very large hyperedges (e.g. a
        /// "works at company X" affiliation with thousands of members), even though no projected
        /// layer is ever created.
        /// </summary>
        internal static (Dictionary<uint, int> communities, double modularity, double codelengthBits) InfomapCommunities(
            uint[] nodeIds, List<ILayer> layers)
        {
            int n0 = nodeIds.Length;
            if (n0 == 0)
                return (new Dictionary<uint, int>(0), 0.0, 0.0);

            var (neighbors0, weights0, degree0, twoM) = BuildAdjacencyMixed(nodeIds, layers);

            if (twoM <= 0)
            {
                var singletons = new Dictionary<uint, int>(n0);
                for (int i = 0; i < n0; i++) singletons[nodeIds[i]] = i;
                return (singletons, 0.0, 0.0);
            }

            int[] mapping = new int[n0];
            for (int i = 0; i < n0; i++) mapping[i] = i;

            int curN = n0;
            int[][] curNeighbors = neighbors0;
            double[][] curWeights = weights0;
            double[] curDegree = degree0;

            while (true)
            {
                int[] community = new int[curN];
                for (int i = 0; i < curN; i++) community[i] = i;

                bool improved = LocalMovingPhaseInfomap(curN, curNeighbors, curWeights, curDegree, twoM, community);

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

            var communities = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++) communities[nodeIds[i]] = mapping[i];

            double modularity = ComputeModularity(n0, neighbors0, weights0, degree0, twoM, 1.0, mapping);
            double codelength = ComputeCodelength(n0, neighbors0, weights0, degree0, twoM, mapping);
            return (communities, modularity, codelength);
        }

        #endregion

        #region Methods (private)

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
        }

        /// <summary>
        /// Infomap's analog of <see cref="LocalMovingPhase"/>: repeatedly sweeps all nodes (in
        /// random order), moving each to whichever neighboring module (including its own) minimizes
        /// the resulting two-level map-equation codelength, until a full sweep produces no moves.
        /// Tracks, per module, both its total weighted degree (sigmaTot, same convention as
        /// <see cref="LocalMovingPhase"/>) and its internal "doubled" edge weight (sigmaIn, same
        /// convention as <see cref="ComputeModularity"/>), plus the network-wide S = Σ sigmaIn(c).
        /// Since Σ_c Σtot(c) always equals twoM regardless of partition, the network's total exit
        /// probability reduces to the single scalar 1 - S/twoM, so S is all that needs tracking
        /// globally — no separate sweep over every module is needed to score a candidate move; see
        /// <see cref="InfomapCandidateCost"/>. Returns whether any node ever moved.
        /// </summary>
        private static bool LocalMovingPhaseInfomap(int n, int[][] neighbors, double[][] weights,
            double[] degree, double twoM, int[] community)
        {
            var sigmaTot = new double[n];
            for (int i = 0; i < n; i++) sigmaTot[community[i]] += degree[i];

            var sigmaIn = new double[n];
            for (int u = 0; u < n; u++)
            {
                int cu = community[u];
                var nbrs0 = neighbors[u];
                var wts0 = weights[u];
                for (int k = 0; k < nbrs0.Length; k++)
                    if (community[nbrs0[k]] == cu) sigmaIn[cu] += wts0[k];
            }

            double S = 0.0;
            for (int i = 0; i < n; i++) S += sigmaIn[i];

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

                    neighborCommWeight.Clear();
                    var nbrs = neighbors[u];
                    var wts = weights[u];
                    for (int k = 0; k < nbrs.Length; k++)
                    {
                        int v = nbrs[k];
                        if (v == u) continue; // self-loop: no inter-module linkage signal
                        int c = community[v];
                        neighborCommWeight[c] = neighborCommWeight.GetValueOrDefault(c) + wts[k];
                    }

                    double kInCurrent = neighborCommWeight.GetValueOrDefault(currentComm);
                    sigmaTot[currentComm] -= degree[u];
                    sigmaIn[currentComm] -= 2.0 * kInCurrent;
                    S -= 2.0 * kInCurrent;

                    int bestComm = currentComm;
                    double bestCost = InfomapCandidateCost(sigmaTot[currentComm], sigmaIn[currentComm], kInCurrent, degree[u], twoM, S);

                    foreach (var (c, kIn) in neighborCommWeight)
                    {
                        if (c == currentComm) continue;
                        double cost = InfomapCandidateCost(sigmaTot[c], sigmaIn[c], kIn, degree[u], twoM, S);
                        if (cost < bestCost - Epsilon)
                        {
                            bestCost = cost;
                            bestComm = c;
                        }
                    }

                    double kInBest = bestComm == currentComm ? kInCurrent : neighborCommWeight[bestComm];
                    sigmaTot[bestComm] += degree[u];
                    sigmaIn[bestComm] += 2.0 * kInBest;
                    S += 2.0 * kInBest;

                    if (bestComm != currentComm)
                    {
                        community[u] = bestComm;
                        improvedThisPass = true;
                        improvedAny = true;
                    }
                }
            }
            return improvedAny;
        }

        /// <summary>
        /// Codelength contribution of hypothetically moving a node with degree
        /// <paramref name="degU"/> into a module currently holding sigmaTotC/sigmaInC, given it
        /// shares edge weight <paramref name="kIn"/> with that module's current members, and the
        /// network-wide S = Σ sigmaIn(c) from just *before* this hypothetical move (i.e. with the
        /// node already removed from wherever it previously was). Only the terms that can differ
        /// between candidate modules for the same node are included — comparing this value across
        /// candidates is meaningful, the absolute value is not. Lower is better.
        /// </summary>
        private static double InfomapCandidateCost(double sigmaTotC, double sigmaInC, double kIn, double degU, double twoM, double S)
        {
            double sTotAfter = sigmaTotC + degU;
            double sInAfter = sigmaInC + 2.0 * kIn;
            double sAfter = S + 2.0 * kIn;

            double qTotalAfter = 1.0 - sAfter / twoM;
            double qExitAfter = (sTotAfter - sInAfter) / twoM;
            double pCircAfter = qExitAfter + sTotAfter / twoM;

            return Plogp(qTotalAfter) - 2.0 * Plogp(qExitAfter) + Plogp(pCircAfter);
        }

        /// <summary>x·log2(x) for x > 0, else 0 (the standard plogp convention used throughout the
        /// map equation, since a probability of exactly 0 contributes no description length).</summary>
        private static double Plogp(double x) => x > Epsilon ? x * Math.Log2(x) : 0.0;

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
        /// Computes the two-level map-equation codelength (bits per step) for the final per-node
        /// community mapping, directly from the original (level-0) graph — same "trace through the
        /// final mapping, don't track incrementally across aggregation levels" approach as
        /// <see cref="ComputeModularity"/>. Uses the compact identity L = plogp(q↷) - 2·Σ_c
        /// plogp(q_c↷) - Σ_α plogp(p_α) + Σ_c plogp(p_c↻), algebraically equivalent to the standard
        /// q↷H(Q) + Σ_c p_c↻H(P^c) form (verified against a direct implementation of that definition
        /// during development) but avoiding the need to compute per-module entropies explicitly.
        /// Lower is better.
        /// </summary>
        private static double ComputeCodelength(int n0, int[][] neighbors0, double[][] weights0,
            double[] degree0, double twoM, int[] mapping)
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

            double qTotal = 0.0;
            double sumPlogpQExit = 0.0;
            double sumPlogpPCirc = 0.0;
            foreach (var (c, stot) in sigmaTot)
            {
                double sin = sigmaIn.GetValueOrDefault(c);
                double qExit = (stot - sin) / twoM;
                double pCirc = qExit + stot / twoM;
                qTotal += qExit;
                sumPlogpQExit += Plogp(qExit);
                sumPlogpPCirc += Plogp(pCirc);
            }

            double sumPlogpP = 0.0;
            for (int u = 0; u < n0; u++)
                sumPlogpP += Plogp(degree0[u] / twoM);

            return Plogp(qTotal) - 2.0 * sumPlogpQExit - sumPlogpP + sumPlogpPCirc;
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
            }

            var (neighbors, weights) = ToArrays(adjDict, n);
            double[] degree = new double[n];
            for (int i = 0; i < n; i++) degree[i] = SumArray(weights[i]);
            double twoM = SumArray(degree);
            return (neighbors, weights, degree, twoM);
        }

        /// <summary>
        /// Like <see cref="BuildAdjacency"/>, but also accepts 2-mode layers — used only by
        /// <see cref="InfomapCommunities"/>. 1-mode layers are handled identically (the same bulk
        /// <c>GetAllEgoData()</c> pass). For a 2-mode layer, the exact projected co-membership weight
        /// between every pair of a hyperedge's members is accumulated directly — the same "iterate
        /// each hyperedge once, accumulate every member pair" approach
        /// <see cref="Threadle.Core.Processing.NetworkProcessor.ProjectTwoModeToOneMode"/> uses for
        /// its Count method, and the same weight definition as
        /// <see cref="Threadle.Core.Model.ILayer.GetNodeAltersWithWeights"/> ("number of shared
        /// hyperedges"). This computes the projected weights on the fly rather than avoiding that
        /// cost: a hyperedge with h members contributes O(h²) work here, same as materializing it as
        /// a projected layer would — no projected layer is created or stored, but the computation is
        /// only cheaper than projecting when the same 2-mode layer would otherwise be projected
        /// repeatedly (e.g. across resolution/aggregation levels here, since level-0 is the only
        /// level this expense is paid at). Large hyperedges (e.g. a "works at company X" affiliation
        /// with thousands of members) make this expensive regardless.
        /// </summary>
        private static (int[][] neighbors, double[][] weights, double[] degree, double twoM) BuildAdjacencyMixed(
            uint[] nodeIds, List<ILayer> layers)
        {
            int n = nodeIds.Length;
            var idx = new Dictionary<uint, int>(n);
            for (int i = 0; i < n; i++) idx[nodeIds[i]] = i;

            var adjDict = new Dictionary<int, double>[n];
            for (int i = 0; i < n; i++) adjDict[i] = new Dictionary<int, double>();

            foreach (var layer in layers)
            {
                if (layer is ILayerOneMode oneMode)
                {
                    foreach (var (egoId, alters, values) in oneMode.GetAllEgoData())
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
                }
                else if (layer is ILayerTwoMode twoMode)
                {
                    foreach (var (hypername, memberIds) in twoMode.GetAllHyperedgeData())
                    {
                        for (int i = 0; i < memberIds.Length; i++)
                        {
                            if (!idx.TryGetValue(memberIds[i], out int ui)) continue;
                            for (int j = i + 1; j < memberIds.Length; j++)
                            {
                                if (!idx.TryGetValue(memberIds[j], out int vi)) continue;
                                adjDict[ui][vi] = adjDict[ui].GetValueOrDefault(vi) + 1.0;
                                adjDict[vi][ui] = adjDict[vi].GetValueOrDefault(ui) + 1.0;
                            }
                        }
                    }
                }
            }

            var (mixedNeighbors, mixedWeights) = ToArrays(adjDict, n);
            double[] mixedDegree = new double[n];
            for (int i = 0; i < n; i++) mixedDegree[i] = SumArray(mixedWeights[i]);
            double mixedTwoM = SumArray(mixedDegree);
            return (mixedNeighbors, mixedWeights, mixedDegree, mixedTwoM);
        }

        #endregion
    }
}
