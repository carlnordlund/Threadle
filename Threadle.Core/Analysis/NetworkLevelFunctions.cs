using Threadle.Core.Model;

namespace Threadle.Core.Analysis
{
    /// <summary>
    /// Internal functions for network-level (macro) descriptive statistics.
    /// </summary>
    internal static class NetworkLevelFunctions
    {
        #region Methods (internal)

        /// <summary>
        /// Degree assortativity (Newman 2002): Pearson correlation between degrees of
        /// connected node pairs. For directed layers uses out-degree of source and
        /// in-degree of target; for undirected uses degree of both endpoints.
        /// </summary>
        internal static double DegreeAssortativity(uint[] nodeIds, ILayerOneMode layer)
        {
            bool directed = !layer.IsSymmetric;
            var jVals = new Dictionary<uint, double>(nodeIds.Length);
            var kVals = new Dictionary<uint, double>(nodeIds.Length);
            foreach (uint u in nodeIds)
            {
                jVals[u] = layer.GetOutDegree(u);
                kVals[u] = directed ? layer.GetInDegree(u) : layer.GetOutDegree(u);
            }
            return PearsonEdgeCorrelation(layer, jVals, kVals);
        }

        /// <summary>
        /// Continuous attribute assortativity: Pearson correlation of a numeric node
        /// attribute across connected node pairs. Nodes with missing attribute are skipped.
        /// </summary>
        internal static double ContinuousAssortativity(ILayerOneMode layer, Dictionary<uint, double> vals)
            => PearsonEdgeCorrelation(layer, vals, vals);

        /// <summary>
        /// Categorical attribute assortativity (Newman 2003): measures the tendency of
        /// nodes to connect to others sharing the same category.
        /// r = (Σ_c e_cc − Σ_c a_c b_c) / sqrt((1−Σ_c a_c²)(1−Σ_c b_c²))
        /// For undirected layers a_c = b_c. Nodes with missing attribute are skipped.
        /// </summary>
        internal static double CategoricalAssortativity(ILayerOneMode layer, Dictionary<uint, string> cats)
        {
            var sourceCounts = new Dictionary<string, long>();
            var targetCounts = new Dictionary<string, long>();
            var matchCounts = new Dictionary<string, long>();
            long M = 0;

            foreach (var (egoId, alters, _) in layer.GetAllEgoData())
            {
                if (!cats.TryGetValue(egoId, out string? cJ)) continue;
                foreach (uint v in alters.Span)
                {
                    if (!cats.TryGetValue(v, out string? cK)) continue;
                    M++;
                    sourceCounts[cJ] = sourceCounts.GetValueOrDefault(cJ) + 1;
                    targetCounts[cK] = targetCounts.GetValueOrDefault(cK) + 1;
                    if (cJ == cK)
                        matchCounts[cJ] = matchCounts.GetValueOrDefault(cJ) + 1;
                }
            }

            if (M == 0) return 0.0;

            double sumEcc = matchCounts.Values.Sum() / (double)M;
            double sumA2, sumB2, sumAB;

            if (layer.IsSymmetric)
            {
                // Undirected: merge both endpoint counts; a_c == b_c
                var combined = new Dictionary<string, long>(sourceCounts);
                foreach (var (c, cnt) in targetCounts)
                    combined[c] = combined.GetValueOrDefault(c) + cnt;
                double denom = 2.0 * M;
                sumA2 = sumB2 = sumAB = combined.Values.Sum(v => Math.Pow(v / denom, 2));
            }
            else
            {
                var allCats = new HashSet<string>(sourceCounts.Keys);
                allCats.UnionWith(targetCounts.Keys);
                sumAB = allCats.Sum(c =>
                    sourceCounts.GetValueOrDefault(c) / (double)M *
                    targetCounts.GetValueOrDefault(c) / (double)M);
                sumA2 = sourceCounts.Values.Sum(v => Math.Pow(v / (double)M, 2));
                sumB2 = targetCounts.Values.Sum(v => Math.Pow(v / (double)M, 2));
            }

            double denominator = Math.Sqrt((1 - sumA2) * (1 - sumB2));
            return denominator == 0 ? 0.0 : (sumEcc - sumAB) / denominator;
        }

        /// <summary>
        /// MAN dyad census plus arc and dyadic reciprocity for a single 1-mode layer.
        /// For directed layers iterates all arcs in O(E); for undirected all non-null
        /// dyads are mutual by definition.
        /// </summary>
        internal static Dictionary<string, object> DyadCensus(int n, ILayerOneMode layer)
        {
            long totalDyads = (long)n * (n - 1) / 2;

            if (layer.IsSymmetric)
            {
                long M = (long)layer.NbrEdges;
                long N = totalDyads - M;
                return new Dictionary<string, object>
                {
                    ["Mutual"] = M,
                    ["Asymmetric"] = 0L,
                    ["Null"] = N,
                    ["Total"] = totalDyads,
                    ["ArcReciprocity"] = 1.0,
                    ["DyadicReciprocity"] = M > 0 ? 1.0 : 0.0
                };
            }

            // Directed: each arc appears once in GetAllEgoData (outbound only)
            long totalArcs = 0;
            long mutualArcs = 0;
            foreach (var (egoId, alters, _) in layer.GetAllEgoData())
                foreach (uint v in alters.Span)
                {
                    totalArcs++;
                    if (layer.GetEdgeValue(v, egoId) > 0)
                        mutualArcs++;
                }

            // Each mutual dyad contributes 2 arcs to mutualArcs
            long M_d = mutualArcs / 2;
            long A_d = totalArcs - mutualArcs;
            long N_d = totalDyads - M_d - A_d;

            return new Dictionary<string, object>
            {
                ["Mutual"] = M_d,
                ["Asymmetric"] = A_d,
                ["Null"] = N_d,
                ["Total"] = totalDyads,
                ["ArcReciprocity"] = totalArcs > 0 ? (double)mutualArcs / totalArcs : 0.0,
                ["DyadicReciprocity"] = M_d + A_d > 0 ? (double)M_d / (M_d + A_d) : 0.0
            };
        }
        #endregion

        #region Methods (private)
        /// <summary>
        /// Pearson correlation of j-values and k-values across all edges. Iterates
        /// GetAllEgoData() once; skips edges where either endpoint is missing a value.
        /// Handles both directed (one arc per pair) and undirected (one entry per edge).
        /// </summary>
        private static double PearsonEdgeCorrelation(ILayerOneMode layer,
            Dictionary<uint, double> jVals, Dictionary<uint, double> kVals)
        {
            double sumJK = 0, sumJ = 0, sumK = 0, sumJ2 = 0, sumK2 = 0;
            long M = 0;

            foreach (var (egoId, alters, _) in layer.GetAllEgoData())
            {
                if (!jVals.TryGetValue(egoId, out double j)) continue;
                foreach (uint v in alters.Span)
                {
                    if (!kVals.TryGetValue(v, out double k)) continue;
                    sumJK += j * k;
                    sumJ += j;
                    sumK += k;
                    sumJ2 += j * j;
                    sumK2 += k * k;
                    M++;
                }
            }

            if (M == 0) return 0.0;
            double denom = Math.Sqrt((M * sumJ2 - sumJ * sumJ) * (M * sumK2 - sumK * sumK));
            return denom == 0 ? 0.0 : (M * sumJK - sumJ * sumK) / denom;
        }
        #endregion
    }
}