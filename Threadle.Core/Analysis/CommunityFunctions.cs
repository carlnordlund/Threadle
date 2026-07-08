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

            var idx = new Dictionary<uint, int>(n0);
            for (int i = 0; i < n0; i++) idx[nodeIds[i]] = i;

            var adjDict = new Dictionary<int, double>[n0];
            for (int i = 0; i < n0; i++) adjDict[i] = new Dictionary<int, double>();

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

            var (neighbors0, weights0) = ToArrays(adjDict, n0);
            double[] degree0 = new double[n0];
            for (int i = 0; i < n0; i++) degree0[i] = SumArray(weights0[i]);
            double twoM = SumArray(degree0);

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
            var sigmaTot = new double[n];
            for (int i = 0; i < n; i++) sigmaTot[i] = degree[i];

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

        #endregion
    }
}