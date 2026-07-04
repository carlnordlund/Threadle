using Threadle.Core.Model;

namespace Threadle.Core.Analysis
{
    /// <summary>
    /// Internal functions for network-level (macro) descriptive statistics.
    /// </summary>
    internal static class NetworkLevelFunctions
    {
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
    }
}