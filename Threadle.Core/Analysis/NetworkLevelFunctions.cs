using System;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Analysis
{
    /// <summary>
    /// Internal functions for network-level (macro) descriptive statistics.
    /// </summary>
    internal static class NetworkLevelFunctions
    {
        #region Methods (internal)

        /// <summary>
        /// Holland-Leinhardt triadic census for a single 1-mode layer: counts of the 16 isomorphism
        /// classes of directed triads (003, 012, 102, 021D, 021U, 021C, 111D, 111U, 030T, 030C, 201,
        /// 120D, 120U, 120C, 210, 300). For symmetric layers every present dyad is mutual, so only
        /// 003/102/201/300 can be non-zero.
        /// Two passes over neighbor sets (union of in/out alters): (1) for every dyad with an edge,
        /// the number of third nodes disconnected from both endpoints is added to 012/102 via
        /// inclusion-exclusion, avoiding an O(n) scan per dyad; (2) for every node acting as a
        /// candidate "hub", each unordered pair of its neighbors is classified as a wedge (only the
        /// hub connects to both — counted once, from the hub) or a triangle (all three mutually
        /// connected — counted once, only when the hub has the smallest node id of the three).
        /// The empty triad count (003) is the combinatorial remainder. Reference: Holland & Leinhardt
        /// (1970) doi:10.1086/224727; Batagelj & Mrvar (2001) triadic census algorithm.
        /// </summary>
        internal static Dictionary<string, object> TriadicCensus(uint[] nodeIds, ILayerOneMode layer)
        {
            int n = nodeIds.Length;
            var counts = new Dictionary<string, long>
            {
                ["012"] = 0,
                ["102"] = 0,
                ["021D"] = 0,
                ["021U"] = 0,
                ["021C"] = 0,
                ["111D"] = 0,
                ["111U"] = 0,
                ["201"] = 0,
                ["030T"] = 0,
                ["030C"] = 0,
                ["120D"] = 0,
                ["120U"] = 0,
                ["120C"] = 0,
                ["210"] = 0,
                ["300"] = 0
            };
            long total = n >= 3 ? (long)n * (n - 1) * (n - 2) / 6 : 0;
            if (total == 0)
            {
                var empty = BuildResult(counts, 0, total);
                empty["Method"] = "Exact";
                return empty;
            }
            var nbr = new Dictionary<uint, HashSet<uint>>(n);
            foreach (uint v in nodeIds)
            {
                var set = new HashSet<uint>(layer.GetNodeAlters(v, EdgeTraversal.Both));
                set.Remove(v); // ignore self-ties
                nbr[v] = set;
            }
            // Pass 1: for every dyad with an edge, bucket the triples where the third node is
            // isolated from both endpoints into 012 (asymmetric dyad) or 102 (mutual dyad).
            foreach (uint v in nodeIds)
            {
                var Nv = nbr[v];
                foreach (uint u in Nv)
                {
                    if (u <= v) continue; // canonical order: process each dyad once
                    var Nu = nbr[u];
                    int unionSize = Nv.Count + Nu.Count - CountIntersection(Nv, Nu);
                    int thirdCandidates = unionSize - 2; // exclude v and u themselves
                    long isolated = (n - 2) - thirdCandidates;
                    if (isolated <= 0) continue;
                    bool mutual = layer.GetEdgeValue(v, u) > 0 && layer.GetEdgeValue(u, v) > 0;
                    counts[mutual ? "102" : "012"] += isolated;
                }
            }
            // Pass 2: for every node as candidate hub, classify each pair of its neighbors as a
            // wedge (hub is the sole shared node) or a triangle (all three interconnected).
            foreach (uint v in nodeIds)
            {
                var Nv = nbr[v];
                if (Nv.Count < 2) continue;
                uint[] arr = [.. Nv];
                for (int i = 0; i < arr.Length; i++)
                {
                    uint u = arr[i];
                    for (int j = i + 1; j < arr.Length; j++)
                    {
                        uint w = arr[j];
                        if (!nbr[u].Contains(w))
                            counts[ClassifyWedge(layer, v, u, w)]++;
                        else if (v < u && v < w)
                            counts[ClassifyTriangle(layer, v, u, w)]++;
                    }
                }
            }
            long n003 = total - counts.Values.Sum();
            var result = BuildResult(counts, n003, total);
            result["Method"] = "Exact";
            return result;
        }

        /// <summary>
        /// Canonical display/iteration order for the 16 triad types, matching <see cref="BuildResult"/>.
        /// </summary>
        private static readonly string[] TriadTypeOrder =
        [
            "003", "012", "102", "021D", "021U", "021C", "111D", "111U",
            "030T", "030C", "201", "120D", "120U", "120C", "210", "300"
        ];

        /// <summary>
        /// Estimates the triadic census by classifying <paramref name="sampleSize"/> uniformly-random
        /// node triples (drawn with replacement) instead of enumerating the network. Each type's count

        /// is estimated as Total * p̂ (the sampled proportion) and reported as a double — an estimate,
        /// not an exact tally, so it is not rounded to an integer. (Rounding would risk landing the
        /// reported value just outside its own confidence interval whenever Total is small relative to
        /// sampleSize, since the interval can then be narrower than the 0.5 rounding error.)
        /// The confidence interval uses the Wilson score interval rather than the naive Wald interval
        /// (p̂ ± z·sqrt(p̂(1-p̂)/m)): the Wald form collapses to zero width whenever a type is observed
        /// zero times in the sample — which is the common case for rare types, since e.g. a true
        /// proportion of 1/57155 has a ~92% chance of zero hits in a sample of 5000 — falsely reporting
        /// certainty that the count is exactly zero. Wilson stays a proper, non-degenerate interval at
        /// that boundary. The reported "StandardErrors" entry is the implied half-width of that same
        /// interval divided by z, so it stays consistent with the reported bounds. Because empty (003)
        /// triads dominate real sparse networks, rare types will still have wide intervals unless the
        /// sample is large — that is a property of the sparsity, not the estimator.
        /// </summary>
        internal static Dictionary<string, object> TriadicCensusSampled(uint[] nodeIds, ILayerOneMode layer, int sampleSize)
        {
            int n = nodeIds.Length;
            long total = n >= 3 ? (long)n * (n - 1) * (n - 2) / 6 : 0;
            var counts = new Dictionary<string, long>(TriadTypeOrder.Length);
            foreach (string type in TriadTypeOrder) counts[type] = 0;
            //var counts = new Dictionary<string, long>
            //{
            //    ["003"] = 0,
            //    ["012"] = 0,
            //    ["102"] = 0,
            //    ["021D"] = 0,
            //    ["021U"] = 0,
            //    ["021C"] = 0,
            //    ["111D"] = 0,
            //    ["111U"] = 0,
            //    ["201"] = 0,
            //    ["030T"] = 0,
            //    ["030C"] = 0,
            //    ["120D"] = 0,
            //    ["120U"] = 0,
            //    ["120C"] = 0,
            //    ["210"] = 0,
            //    ["300"] = 0
            //};
            if (total > 0)
            {
                for (int s = 0; s < sampleSize; s++)
                {
                    uint a = nodeIds[Misc.Random.Next(n)];
                    uint b, c;
                    do { b = nodeIds[Misc.Random.Next(n)]; } while (b == a);
                    do { c = nodeIds[Misc.Random.Next(n)]; } while (c == a || c == b);
                    counts[ClassifyTriad(layer, a, b, c)]++;
                }
            }
            //const double z95 = 1.959963984540054;
            //var estimates = new Dictionary<string, long>(counts.Count);
            //var standardErrors = new Dictionary<string, object>(counts.Count);
            //var ciLower = new Dictionary<string, object>(counts.Count);
            //var ciUpper = new Dictionary<string, object>(counts.Count);
            //foreach (var (type, sampled) in counts)
            const double z = 1.959963984540054;
            double zsq = z * z;
            int m = Math.Max(sampleSize, 1);
            double denom = 1.0 + zsq / m;
            
            var result = new Dictionary<string, object>(TriadTypeOrder.Length + 6);
            var standardErrors = new Dictionary<string, object>(TriadTypeOrder.Length);
            var ciLower = new Dictionary<string, object>(TriadTypeOrder.Length);
            var ciUpper = new Dictionary<string, object>(TriadTypeOrder.Length);
            foreach (string type in TriadTypeOrder)
            {
                //double p = sampleSize > 0 ? (double)sampled / sampleSize : 0.0;
                //double estCount = p * total;
                //double seCount = Math.Sqrt(p * (1 - p) / Math.Max(sampleSize, 1)) * total;
                //estimates[type] = (long)Math.Round(estCount);
                //standardErrors[type] = seCount;
                //ciLower[type] = Math.Max(0.0, estCount - z95 * seCount);
                //ciUpper[type] = Math.Min((double)total, estCount + z95 * seCount);
                double p = (double)counts[type] / m;
                result[type] = p * total;
                
                // Wilson score interval on p, then scaled to counts.
                double center = (p + zsq / (2.0 * m)) / denom;
                double halfWidth = (z / denom) * Math.Sqrt(p * (1 - p) / m + zsq / (4.0 * m * m));
                double pLow = Math.Max(0.0, center - halfWidth);
                double pHigh = Math.Min(1.0, center + halfWidth);
                
                standardErrors[type] = halfWidth / z * total;
                ciLower[type] = pLow * total;
                ciUpper[type] = pHigh * total;
            }
            //var result = BuildResult(estimates, estimates["003"], total);
            result["Total"] = total;
            result["Method"] = "Sampled";
            result["SampleSize"] = sampleSize;
            result["StandardErrors"] = standardErrors;
            result["ConfidenceIntervalLower"] = ciLower;
            result["ConfidenceIntervalUpper"] = ciUpper;
            return result;
        }

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

        /// <summary>Number of elements common to both sets, iterating the smaller one.</summary>
        private static int CountIntersection(HashSet<uint> a, HashSet<uint> b)
        {
            var (small, large) = a.Count <= b.Count ? (a, b) : (b, a);
            int count = 0;
            foreach (uint x in small)
                if (large.Contains(x)) count++;
            return count;
        }

        /// <summary>
        /// The relation between a directed pair (x,y): whether the tie is mutual, a single
        /// arc x-&gt;y (Forward), or a single arc y-&gt;x (Backward). Assumes some tie is known to exist.
        /// </summary>
        private enum PairRelation { Mutual, Forward, Backward }
        private static PairRelation PairState(ILayerOneMode layer, uint x, uint y)
        {
            bool xy = layer.GetEdgeValue(x, y) > 0;
            bool yx = layer.GetEdgeValue(y, x) > 0;
            if (xy && yx) return PairRelation.Mutual;
            return xy ? PairRelation.Forward : PairRelation.Backward;
        }

        /// <summary>
        /// Given the (already-known-asymmetric) relations of a hub to two other nodes, returns
        /// "D" if the hub emits both arcs (divergent/out-star), "U" if it receives both (convergent/
        /// in-star), or "C" if one arc goes each way (chain).
        /// </summary>
        private static char HubDirectionSuffix(PairRelation hubToFirst, PairRelation hubToSecond)
        {
            if (hubToFirst == PairRelation.Forward && hubToSecond == PairRelation.Forward) return 'D';
            if (hubToFirst == PairRelation.Backward && hubToSecond == PairRelation.Backward) return 'U';
            return 'C';
        }

        /// <summary>
        /// Classifies a wedge: hub is the only node connected to both s1 and s2 (s1-s2 has no tie).
        /// Two mutual dyads → 201. One mutual + one asymmetric → 111D (asymmetric arc points into
        /// the hub) or 111U (points out of the hub). Two asymmetric arcs → 021D/U/C via
        /// <see cref="HubDirectionSuffix"/>.
        /// </summary>
        private static string ClassifyWedge(ILayerOneMode layer, uint hub, uint s1, uint s2)
        {
            var r1 = PairState(layer, hub, s1);
            var r2 = PairState(layer, hub, s2);
            int mutualCount = (r1 == PairRelation.Mutual ? 1 : 0) + (r2 == PairRelation.Mutual ? 1 : 0);
            if (mutualCount == 2) return "201";
            if (mutualCount == 1)
            {
                var asym = r1 == PairRelation.Mutual ? r2 : r1;
                return asym == PairRelation.Backward ? "111D" : "111U";
            }
            return "021" + HubDirectionSuffix(r1, r2);
        }

        /// <summary>
        /// Classifies a fully-connected triple {a,b,c} (every pair has a tie). Three mutual dyads
        /// → 300; two mutual → 210 (a single isomorphism class regardless of the asymmetric arc's
        /// direction); one mutual → 120D/U/C, using the node outside the mutual pair as hub and
        /// <see cref="HubDirectionSuffix"/>; zero mutual → 030T (transitive: some node has
        /// within-triad out-degree 2) or 030C (cyclic: every node has out-degree 1).
        /// </summary>
        private static string ClassifyTriangle(ILayerOneMode layer, uint a, uint b, uint c)
        {
            var ab = PairState(layer, a, b);
            var ac = PairState(layer, a, c);
            var bc = PairState(layer, b, c);
            int mutualCount = (ab == PairRelation.Mutual ? 1 : 0) + (ac == PairRelation.Mutual ? 1 : 0) + (bc == PairRelation.Mutual ? 1 : 0);
            switch (mutualCount)
            {
                case 3: return "300";
                case 2: return "210";
                case 1:
                    uint hub, p, q;
                    if (ab == PairRelation.Mutual) { hub = c; p = a; q = b; }
                    else if (ac == PairRelation.Mutual) { hub = b; p = a; q = c; }
                    else { hub = a; p = b; q = c; }
                    return "120" + HubDirectionSuffix(PairState(layer, hub, p), PairState(layer, hub, q));
                default:
                    int outA = 0, outB = 0, outC = 0;
                    if (ab == PairRelation.Forward) outA++; else outB++;
                    if (ac == PairRelation.Forward) outA++; else outC++;
                    if (bc == PairRelation.Forward) outB++; else outC++;
                    return outA == 2 || outB == 2 || outC == 2 ? "030T" : "030C";
            }
        }

        /// <summary>
        /// Whether a dyad has no tie in either direction. Distinct from <see cref="PairState"/>,
        /// which assumes a tie is already known to exist (true for every caller in the exact
        /// enumeration, but not for an arbitrary sampled triple).
        /// </summary>
        private static bool DyadIsNull(ILayerOneMode layer, uint x, uint y)
            => layer.GetEdgeValue(x, y) <= 0 && layer.GetEdgeValue(y, x) <= 0;

        /// <summary>
        /// General-purpose classifier for an arbitrary node triple (used by the sampled census,
        /// where — unlike the exact enumeration — nothing is known in advance about which, if
        /// any, of the three dyads have ties). Dispatches on how many of the three dyads are null:
        /// three → 003; two → 012/102 (the one present dyad, asymmetric or mutual); one → the
        /// wedge formed by the two present dyads, via <see cref="ClassifyWedge"/>; zero → the full
        /// triangle, via <see cref="ClassifyTriangle"/>.
        /// </summary>
        private static string ClassifyTriad(ILayerOneMode layer, uint a, uint b, uint c)
        {
            bool abNull = DyadIsNull(layer, a, b);
            bool acNull = DyadIsNull(layer, a, c);
            bool bcNull = DyadIsNull(layer, b, c);
            int nullCount = (abNull ? 1 : 0) + (acNull ? 1 : 0) + (bcNull ? 1 : 0);
            switch (nullCount)
            {
                case 3: return "003";
                case 2:
                    uint x, y;
                    if (!abNull) { x = a; y = b; } else if (!acNull) { x = a; y = c; } else { x = b; y = c; }
                    return layer.GetEdgeValue(x, y) > 0 && layer.GetEdgeValue(y, x) > 0 ? "102" : "012";
                case 1:
                    // The null dyad's excluded node is the hub (adjacent to both remaining nodes).
                    if (bcNull) return ClassifyWedge(layer, a, b, c);
                    if (acNull) return ClassifyWedge(layer, b, a, c);
                    return ClassifyWedge(layer, c, a, b);
                default:
                    return ClassifyTriangle(layer, a, b, c);
            }
        }

        /// <summary>Assembles the final triad-census dictionary, including the empty-triad count and total.</summary>
        private static Dictionary<string, object> BuildResult(Dictionary<string, long> counts, long n003, long total)
        {
            return new Dictionary<string, object>
            {
                ["003"] = n003,
                ["012"] = counts["012"],
                ["102"] = counts["102"],
                ["021D"] = counts["021D"],
                ["021U"] = counts["021U"],
                ["021C"] = counts["021C"],
                ["111D"] = counts["111D"],
                ["111U"] = counts["111U"],
                ["030T"] = counts["030T"],
                ["030C"] = counts["030C"],
                ["201"] = counts["201"],
                ["120D"] = counts["120D"],
                ["120U"] = counts["120U"],
                ["120C"] = counts["120C"],
                ["210"] = counts["210"],
                ["300"] = counts["300"],
                ["Total"] = total
            };
        }

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