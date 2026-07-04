using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Analysis
{
    /// <summary>
    /// Internal functions for local structural metrics: clustering coefficients and global transitivity.
    /// </summary>
    internal static class LocalStructureFunctions
    {
        #region Methods (internal)

        /// <summary>
        /// K-core decomposition (Batagelj & Zaversnik, 2003). Returns the coreness (k-shell index)
        /// of each node: the highest k such that the node survives in the k-core.
        /// Treats all edges as undirected (union of in/out neighbors) and ignores edge weights.
        /// </summary>
        internal static Dictionary<uint, int> Coreness(uint[] nodeIds, List<ILayerOneMode> layers)
        {
            int n = nodeIds.Length;
            if (n == 0 || layers.Count == 0) return new Dictionary<uint, int>(0);

            var idx = new Dictionary<uint, int>(n);
            for (int i = 0; i < n; i++) idx[nodeIds[i]] = i;

            // Union of neighbors across all layers (both directions)
            var neighborSets = new HashSet<int>[n];
            for (int i = 0; i < n; i++) neighborSets[i] = new HashSet<int>();

            foreach (var layer in layers)
                foreach (uint u in nodeIds)
                {
                    int ui = idx[u];
                    foreach (uint v in layer.GetNodeAlters(u, EdgeTraversal.Both))
                        if (v != u && idx.TryGetValue(v, out int vi))
                            neighborSets[ui].Add(vi);
                }

            int[][] adj = new int[n][];
            int[] deg = new int[n];
            for (int i = 0; i < n; i++)
            {
                adj[i] = neighborSets[i].Count > 0 ? neighborSets[i].ToArray() : Array.Empty<int>();
                deg[i] = adj[i].Length;
            }

            int maxDeg = 0;
            for (int i = 0; i < n; i++) if (deg[i] > maxDeg) maxDeg = deg[i];

            int[] bin = new int[maxDeg + 1];
            for (int i = 0; i < n; i++) bin[deg[i]]++;

            int start = 0;
            for (int d = 0; d <= maxDeg; d++) { int cnt = bin[d]; bin[d] = start; start += cnt; }

            int[] vert = new int[n];
            int[] pos = new int[n];
            int[] tmp = (int[])bin.Clone();
            for (int i = 0; i < n; i++) { pos[i] = tmp[deg[i]]; vert[pos[i]] = i; tmp[deg[i]]++; }

            int[] core = new int[n];
            for (int i = 0; i < n; i++)
            {
                int v = vert[i];
                core[v] = deg[v];
                foreach (int u in adj[v])
                {
                    if (deg[u] > deg[v])
                    {
                        int du = deg[u];
                        int pu = pos[u];
                        int pw = bin[du];
                        int w = vert[pw];
                        if (u != w) { pos[u] = pw; pos[w] = pu; vert[pw] = u; vert[pu] = w; }
                        bin[du]++;
                        deg[u]--;
                    }
                }
            }

            var result = new Dictionary<uint, int>(n);
            for (int i = 0; i < n; i++) result[nodeIds[i]] = core[i];
            return result;
        }

        /// <summary>
        /// Dispatches to the correct clustering coefficient formula based on method (or auto-detects).
        /// </summary>
        internal static Dictionary<uint, double> ClusteringCoefficient(uint[] nodeIds, List<ILayerOneMode> layers, ClusteringMethod method)
        {
            if (method == ClusteringMethod.Auto)
                method = DetectMethod(layers);

            return method switch
            {
                ClusteringMethod.Fagiolo => ClusteringFagiolo(nodeIds, layers),
                ClusteringMethod.Barrat => ClusteringBarrat(nodeIds, layers),
                ClusteringMethod.Onnela => ClusteringOnnela(nodeIds, layers),
                _ => ClusteringWattsStrogatz(nodeIds, layers),
            };
        }

        /// <summary>
        /// Global clustering coefficient (transitivity): Σ t(u) / Σ C(k(u),2),
        /// treating all edges as undirected. Equals 3T / Σ C(k,2) where T is the
        /// number of distinct triangles.
        /// </summary>
        internal static double Transitivity(uint[] nodeIds, List<ILayerOneMode> layers)
        {
            double sumT = 0, sumP = 0;
            foreach (uint u in nodeIds)
            {
                var N = CombinedNeighborSet(u, layers, EdgeTraversal.Both);
                int k = N.Count;
                if (k < 2)
                    continue;
                sumP += k * (k - 1) / 2.0;
                sumT += CountClosedPairs(u, N, layers);
            }
            return sumP > 0 ? sumT / sumP : 0.0;
        }

        #endregion

        #region Methods (private — per-formula)

        internal static ClusteringMethod DetectMethod(List<ILayerOneMode> layers)
        {
            bool allSymmetric = layers.All(l => l.IsSymmetric);
            bool anyValued = layers.Any(l => !l.IsBinary);
            if (anyValued && allSymmetric)
                return ClusteringMethod.Barrat;
            if (!allSymmetric)
                return ClusteringMethod.Fagiolo;
            return ClusteringMethod.WattsStrogatz;
        }

        /// <summary>
        /// Watts-Strogatz (1998): treats network as undirected.
        /// C(u) = t(u) / C(k,2)  where t(u) = distinct edges among N(u).
        /// </summary>
        private static Dictionary<uint, double> ClusteringWattsStrogatz(uint[] nodeIds, List<ILayerOneMode> layers)
        {
            var result = new Dictionary<uint, double>(nodeIds.Length);
            foreach (uint u in nodeIds)
            {
                var N = CombinedNeighborSet(u, layers, EdgeTraversal.Both);
                int k = N.Count;
                if (k < 2) { result[u] = 0.0; continue; }
                double t = CountClosedPairs(u, N, layers);
                result[u] = t / (k * (k - 1) / 2.0);
            }
            return result;
        }

        /// <summary>
        /// Fagiolo (2007): directed clustering coefficient using symmetrized adjacency B = A + A^T.
        /// B_{uv} ∈ {0,1,2}: 0=no tie, 1=one-way, 2=mutual.
        /// t(u) = [Σ_j Σ_k B_uj * B_jk * B_ku] / 2
        /// C(u) = t(u) / (d_tot * (d_tot - 1) - 2 * b)
        /// where d_tot = k_in + k_out, b = reciprocal ties.
        /// For undirected binary layers this reduces to the Watts-Strogatz formula.
        /// </summary>
        private static Dictionary<uint, double> ClusteringFagiolo(uint[] nodeIds, List<ILayerOneMode> layers)
        {
            var result = new Dictionary<uint, double>(nodeIds.Length);
            foreach (uint u in nodeIds)
            {
                var outN = CombinedNeighborSet(u, layers, EdgeTraversal.Out);
                var inN = CombinedNeighborSet(u, layers, EdgeTraversal.In);
                int d_tot = outN.Count + inN.Count;
                if (d_tot < 2) { result[u] = 0.0; continue; }

                int b = 0;
                foreach (uint v in outN) if (inN.Contains(v)) b++;

                var N_sym = new HashSet<uint>(outN);
                N_sym.UnionWith(inN);

                // Precompute B_{u,v} for all v in N_sym (avoids repeated GetEdgeValue calls)
                var b_u = new Dictionary<uint, double>(N_sym.Count);
                foreach (uint v in N_sym)
                    b_u[v] = SymWeight(u, v, layers);

                double t = 0;
                foreach (uint v in N_sym)
                {
                    double b_uv = b_u[v];
                    // N_sym(v): all nodes reachable from v in either direction
                    var N_sym_v = CombinedNeighborSet(v, layers, EdgeTraversal.Both);
                    foreach (uint w in N_sym_v)
                    {
                        if (!N_sym.Contains(w)) continue;
                        t += b_uv * SymWeight(v, w, layers) * b_u.GetValueOrDefault(w);
                    }
                }
                t /= 2.0;  // Fagiolo's formula divides by 2

                double denom = (double)d_tot * (d_tot - 1) - 2.0 * b;
                result[u] = denom > 0 ? t / denom : 0.0;
            }
            return result;
        }

        /// <summary>
        /// Barrat et al. (2004): weighted clustering for valued undirected networks.
        /// Weights each closed triple by the mean edge weight of u's two edges to its triangle partners.
        /// C_w(u) = Σ_{ordered (j,h) ∈ N(u), j-h connected} (w_uj + w_uh)/2  /  (s(u) * (k(u)-1))
        /// where s(u) = node strength (sum of edge weights).
        /// Denominator maximum = s(u)*(k(u)-1), so C_w ∈ [0,1].
        /// </summary>
        private static Dictionary<uint, double> ClusteringBarrat(uint[] nodeIds, List<ILayerOneMode> layers)
        {
            var result = new Dictionary<uint, double>(nodeIds.Length);
            foreach (uint u in nodeIds)
            {
                var N = CombinedNeighborSet(u, layers, EdgeTraversal.Out);
                int k = N.Count;
                if (k < 2) {
                    result[u] = 0.0;
                    continue;
                }

                var edgeWeights = CombinedEdgeWeights(u, layers, EdgeTraversal.Out);
                double s_u = edgeWeights.Values.Sum();
                if (s_u == 0) {
                    result[u] = 0.0;
                    continue;
                }

                // Sum over ordered pairs (j,h): both (j,h) and (h,j) are counted separately
                double triW = 0;
                foreach (uint v in N)
                    foreach (var layer in layers)
                        foreach (uint w in layer.GetNodeAlters(v, EdgeTraversal.Out))
                            if (w != u && N.Contains(w))
                                triW += (edgeWeights.GetValueOrDefault(v) +
                                         edgeWeights.GetValueOrDefault(w)) / 2.0;
                // No divide-by-2: Barrat sums over ordered (j,h) pairs
                result[u] = triW / (s_u * (k - 1));
            }
            return result;
        }

        /// <summary>
        /// Onnela et al. (2005): weighted clustering using geometric mean of all three triangle edge weights.
        /// Weights are normalized by the global maximum weight across all specified layers.
        /// C_O(u) = Σ_{ordered (j,h) ∈ N(u), j-h connected} (w̃_uj * w̃_uh * w̃_jh)^(1/3) / (k(u)*(k(u)-1))
        /// where w̃_ij = w_ij / max(w).
        /// </summary>
        private static Dictionary<uint, double> ClusteringOnnela(uint[] nodeIds, List<ILayerOneMode> layers)
        {
            double maxW = MaxEdgeWeight(layers);
            if (maxW == 0) maxW = 1;

            var result = new Dictionary<uint, double>(nodeIds.Length);
            foreach (uint u in nodeIds)
            {
                var N = CombinedNeighborSet(u, layers, EdgeTraversal.Out);
                int k = N.Count;
                if (k < 2) {
                    result[u] = 0.0;
                    continue;
                }

                var edgeWeights = CombinedEdgeWeights(u, layers, EdgeTraversal.Out);

                // Sum over ordered pairs (j,h)
                double t = 0;
                foreach (uint v in N)
                    foreach (var layer in layers)
                        foreach (uint w in layer.GetNodeAlters(v, EdgeTraversal.Out))
                            if (w != u && N.Contains(w))
                            {
                                double w_uv = edgeWeights.GetValueOrDefault(v) / maxW;
                                double w_uw = edgeWeights.GetValueOrDefault(w) / maxW;
                                double w_vw = CombinedEdgeWeight(v, w, layers) / maxW;
                                t += Math.Pow(w_uv * w_uw * w_vw, 1.0 / 3.0);
                            }
                // No divide-by-2: Onnela sums over ordered (j,h) pairs
                result[u] = t / ((double)k * (k - 1));
            }
            return result;
        }

        #endregion

        #region Methods (private — helpers)

        /// <summary>
        /// Returns the union of neighbors of u across all layers using the given traversal.
        /// </summary>
        private static HashSet<uint> CombinedNeighborSet(uint u, List<ILayerOneMode> layers, EdgeTraversal traversal)
        {
            var set = new HashSet<uint>();
            foreach (var layer in layers)
                foreach (uint v in layer.GetNodeAlters(u, traversal))
                    set.Add(v);
            return set;
        }

        /// <summary>
        /// Counts distinct edges among N(u): for each v in N, iterates v's neighbors
        /// using Both traversal and checks membership in N. Divides by 2 because each
        /// edge v-w is seen from both v's and w's perspective.
        /// </summary>
        private static double CountClosedPairs(uint u, HashSet<uint> N, List<ILayerOneMode> layers)
        {
            double count = 0;
            foreach (uint v in N)
                foreach (var layer in layers)
                    foreach (uint w in layer.GetNodeAlters(v, EdgeTraversal.Both))
                        if (w != u && N.Contains(w))
                            count++;
            return count / 2.0;
        }

        /// <summary>
        /// B_{uv} = A_{uv} + A_{vu}: 0, 1, or 2. Used by Fagiolo's directed formula.
        /// Sums across all specified layers.
        /// </summary>
        private static double SymWeight(uint u, uint v, List<ILayerOneMode> layers)
        {
            int count = 0;
            if (layers.Any(l => l.GetEdgeValue(u, v) > 0)) count++;
            if (layers.Any(l => l.GetEdgeValue(v, u) > 0)) count++;
            return count;
        }

        /// <summary>
        /// Returns a dictionary of edge weights from u to each neighbor, summed across layers.
        /// Binary layers contribute 1.0 per edge.
        /// </summary>
        private static Dictionary<uint, double> CombinedEdgeWeights(uint u, List<ILayerOneMode> layers, EdgeTraversal traversal)
        {
            var weights = new Dictionary<uint, double>();
            foreach (var layer in layers)
            {
                var (alters, wts) = layer.GetNodeAltersWithWeights(u, traversal);
                bool hasWeights = wts.Length > 0;
                for (int i = 0; i < alters.Length; i++)
                {
                    double w = hasWeights ? wts.Span[i] : 1.0;
                    weights[alters.Span[i]] = weights.GetValueOrDefault(alters.Span[i]) + w;
                }
            }
            return weights;
        }

        private static double CombinedEdgeWeight(uint u, uint v, List<ILayerOneMode> layers)
        {
            double total = 0;
            foreach (var layer in layers)
                total += layer.GetEdgeValue(u, v);
            return total;
        }

        private static double MaxEdgeWeight(List<ILayerOneMode> layers)
        {
            double max = 0;
            foreach (var layer in layers)
                if (!layer.IsBinary)
                    foreach (var (_, _, values) in layer.GetAllEgoData())
                        foreach (float v in values.Span)
                            if (v > max)
                                max = v;
            return max;
        }

        #endregion
    }
}