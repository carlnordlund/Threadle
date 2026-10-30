using System;
using System.Linq;
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
        /// The empty triad count (003) is the combinatorial remainder. Both passes are independent
        /// per source node once the neighbor sets are built, so each runs in parallel across up to
        /// 'maxthreads' threads (see 'setting()'), with thread-local partial counts merged under a
        /// lock. Reference: Holland & Leinhardt (1970) doi:10.1086/224727; Batagelj & Mrvar (2001)
        /// triadic census algorithm.
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
            var mergeLock = new object();

            // Pass 1: for every dyad with an edge, bucket the triples where the third node is
            // isolated from both endpoints into 012 (asymmetric dyad) or 102 (mutual dyad).
            System.Threading.Tasks.Parallel.For(0, n, UserSettings.GetParallelOptions(),
                () => new Dictionary<string, long>(),
                (i, loopState, local) =>
                {
                    uint v = nodeIds[i];
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
                        string key = mutual ? "102" : "012";
                        local[key] = local.GetValueOrDefault(key) + isolated;
                    }
                    return local;
                },
                local =>
                {
                    lock (mergeLock)
                    {
                        foreach (var (key, value) in local)
                            counts[key] = counts.GetValueOrDefault(key) + value;
                    }
                });

            // Pass 2: for every node as candidate hub, classify each pair of its neighbors as a
            // wedge (hub is the sole shared node) or a triangle (all three interconnected).
            System.Threading.Tasks.Parallel.For(0, n, UserSettings.GetParallelOptions(),
                () => new Dictionary<string, long>(),
                (i, loopState, local) =>
                {
                    uint v = nodeIds[i];
                    var Nv = nbr[v];
                    if (Nv.Count < 2) return local;
                    uint[] arr = [.. Nv];
                    for (int ai = 0; ai < arr.Length; ai++)
                    {
                        uint u = arr[ai];
                        for (int bi = ai + 1; bi < arr.Length; bi++)
                        {
                            uint w = arr[bi];
                            string key;
                            if (!nbr[u].Contains(w))
                                key = ClassifyWedge(layer, v, u, w);
                            else if (v < u && v < w)
                                key = ClassifyTriangle(layer, v, u, w);
                            else
                                continue;
                            local[key] = local.GetValueOrDefault(key) + 1;
                        }
                    }
                    return local;
                },
                local =>
                {
                    lock (mergeLock)
                    {
                        foreach (var (key, value) in local)
                            counts[key] = counts.GetValueOrDefault(key) + value;
                    }
                });

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
        /// Degree assortativity for a 2-mode layer, pseudo-projected (traversed node-hyperedge-node
        /// directly, never materializing a 1-mode projection). Degree is always the existing binary
        /// projected degree (<see cref="ILayer.GetNodeAlters"/>'s distinct-alter count, matching
        /// degree()'s own 2-mode convention) regardless of <paramref name="projection"/>; only the
        /// pair-counting convention for the correlation itself (see
        /// <see cref="ContinuousAssortativityTwoMode"/>) depends on it.
        /// </summary>
        internal static double DegreeAssortativityTwoMode(uint[] nodeIds, ILayer layer, ProjectionMethod projection)
        {
            var degVals = new Dictionary<uint, double>(nodeIds.Length);
            foreach (uint u in nodeIds)
                degVals[u] = layer.GetNodeAlters(u, EdgeTraversal.Both).Length;
            return ContinuousAssortativityTwoMode(layer, degVals, degVals, nodeIds, projection);
        }

        /// <summary>
        /// Continuous attribute/degree assortativity for a 2-mode layer, pseudo-projected: every
        /// pair of co-affiliates (members sharing at least one hyperedge) contributes a (j,k) and a
        /// (k,j) sample, mirroring the explicit both-directions symmetrization
        /// <see cref="PearsonEdgeCorrelation"/> uses for undirected 1-mode layers (an affiliation
        /// has no direction either). Each node's co-affiliates and their per-pair weight are
        /// obtained from <see cref="ILayer.GetNodeAltersWithWeights"/>, which already deduplicates
        /// a pair across hyperedges (binary) and already tallies the shared-hyperedge count
        /// (count) - so no separate pair-deduplication structure is needed here. When
        /// <paramref name="projection"/> is Binary, every pair counts with weight 1 regardless of
        /// how many hyperedges it shares; when Count, a pair's weight is its shared-hyperedge
        /// count (matching getedge()'s count-weighted projection). Each unordered pair is visited
        /// once, from its lower-id member (O(sum of hyperedge sizes squared) total, no persistent
        /// materialized structure) - EXCEPT for Count with <paramref name="jVals"/> and
        /// <paramref name="kVals"/> being the same dictionary (true for both current callers,
        /// <see cref="DegreeAssortativityTwoMode"/> and attribute assortativity), where a cheaper
        /// O(sum of hyperedge sizes) closed-form path is used instead (see below) - Count-weight
        /// contributions from different hyperedges are simply additive, so, unlike Binary, no
        /// cross-hyperedge pair-identity dedup is ever needed, which is exactly what licenses
        /// folding each hyperedge's full contribution from two per-hyperedge running sums instead
        /// of visiting every pair.
        ///
        /// Count mode's weighting has no materialized-projection equivalent to test against:
        /// <see cref="PearsonEdgeCorrelation"/> (the 1-mode baseline this delegates to for a
        /// materialized layer) ignores an edge's own weight/value entirely - it treats every
        /// existing edge as one unweighted sample. So Binary mode's result does coincide with
        /// running assortativity() on a materialized Binary projection (every pair is unweighted
        /// either way), but Count mode's deliberately-weighted result does not coincide with
        /// running assortativity() on a materialized Count projection (that path is unweighted by
        /// construction) - two different, individually correct quantities, not a discrepancy to
        /// chase if they disagree in a test.
        /// </summary>
        internal static double ContinuousAssortativityTwoMode(ILayer layer, Dictionary<uint, double> jVals, Dictionary<uint, double> kVals, uint[] nodeIds, ProjectionMethod projection)
        {
            double sumJK = 0, sumJ = 0, sumK = 0, sumJ2 = 0, sumK2 = 0, M = 0;

            void AddSample(double j, double k, double weight)
            {
                sumJK += weight * j * k;
                sumJ += weight * j;
                sumK += weight * k;
                sumJ2 += weight * j * j;
                sumK2 += weight * k * k;
                M += weight;
            }

            if (projection == ProjectionMethod.Count && ReferenceEquals(jVals, kVals))
            {
                // Closed-form per-hyperedge reduction. For a hyperedge's members with values
                // x_1..x_k (only members present in jVals count; a value-less member is excluded
                // exactly as the per-pair check below would exclude it), let S = Σx_i, Q = Σx_i².
                // Summed once per unordered pair, each counted from both AddSample calls the
                // per-pair path below would have made (one per direction, since jVals==kVals):
                // sumJK += S² - Q; sumJ/sumK += (k-1)*S; sumJ2/sumK2 += (k-1)*Q; M += k*(k-1).
                foreach (var (_, memberIds) in ((ILayerTwoMode)layer).GetAllHyperedgeData())
                {
                    double s = 0, q = 0;
                    int k = 0;
                    foreach (uint m in memberIds)
                    {
                        if (!jVals.TryGetValue(m, out double x)) continue;
                        s += x;
                        q += x * x;
                        k++;
                    }
                    if (k < 2) continue;
                    sumJK += s * s - q;
                    sumJ += (k - 1) * s;
                    sumK += (k - 1) * s;
                    sumJ2 += (k - 1) * q;
                    sumK2 += (k - 1) * q;
                    M += (double)k * (k - 1);
                }
            }
            else
            {
                foreach (uint u in nodeIds)
                {
                    bool haveUJ = jVals.TryGetValue(u, out double uJ);
                    bool haveUK = kVals.TryGetValue(u, out double uK);
                    if (!haveUJ && !haveUK) continue;
                    var (alters, weights) = layer.GetNodeAltersWithWeights(u, EdgeTraversal.Both);
                    for (int i = 0; i < alters.Length; i++)
                    {
                        uint v = alters.Span[i];
                        if (v <= u) continue; // each unordered pair processed once, from the lower-id member
                        double w = (projection == ProjectionMethod.Count && weights.Length > 0) ? weights.Span[i] : 1.0;
                        if (haveUJ && kVals.TryGetValue(v, out double vK))
                            AddSample(uJ, vK, w);
                        if (haveUK && jVals.TryGetValue(v, out double vJ))
                            AddSample(vJ, uK, w);
                    }
                }
            }

            if (M == 0) return 0.0;
            double denom = Math.Sqrt((M * sumJ2 - sumJ * sumJ) * (M * sumK2 - sumK * sumK));
            return denom == 0 ? 0.0 : (M * sumJK - sumJ * sumK) / denom;
        }

        /// <summary>
        /// Categorical attribute assortativity (Newman 2003) for a 2-mode layer, pseudo-projected:
        /// every pair of co-affiliates contributes once (<paramref name="projection"/> Binary) or
        /// once per shared hyperedge (Count), the same weighting convention used by
        /// <see cref="ContinuousAssortativityTwoMode"/>. Unlike the continuous case, a pair is
        /// counted once (not mirrored into two directional samples): an affiliation has no
        /// direction, and the nominal mixing formula's own a_c/b_c normalization (over 2M, the
        /// total category-weighted degree) already accounts for both endpoints, exactly as it does
        /// for an undirected 1-mode layer (see <see cref="CategoricalAssortativity"/>).
        /// </summary>
        internal static double CategoricalAssortativityTwoMode(ILayer layer, Dictionary<uint, string> cats, uint[] nodeIds, ProjectionMethod projection)
        {
            var sourceCounts = new Dictionary<string, double>();
            var targetCounts = new Dictionary<string, double>();
            var matchCounts = new Dictionary<string, double>();
            double M = 0;

            foreach (uint u in nodeIds)
            {
                if (!cats.TryGetValue(u, out string? cU)) continue;
                var (alters, weights) = layer.GetNodeAltersWithWeights(u, EdgeTraversal.Both);
                for (int i = 0; i < alters.Length; i++)
                {
                    uint v = alters.Span[i];
                    if (v <= u) continue; // each unordered pair processed once, from the lower-id member
                    if (!cats.TryGetValue(v, out string? cV)) continue;
                    double w = (projection == ProjectionMethod.Count && weights.Length > 0) ? weights.Span[i] : 1.0;
                    M += w;
                    sourceCounts[cU] = sourceCounts.GetValueOrDefault(cU) + w;
                    targetCounts[cV] = targetCounts.GetValueOrDefault(cV) + w;
                    if (cU == cV)
                        matchCounts[cU] = matchCounts.GetValueOrDefault(cU) + w;
                }
            }

            if (M == 0) return 0.0;
            double sumEcc = matchCounts.Values.Sum() / M;
            var combined = new Dictionary<string, double>(sourceCounts);
            foreach (var (c, cnt) in targetCounts)
                combined[c] = combined.GetValueOrDefault(c) + cnt;
            double denom2 = 2.0 * M;
            double sumA2 = combined.Values.Sum(v => Math.Pow(v / denom2, 2));
            double denominator = 1 - sumA2;
            return denominator == 0 ? 0.0 : (sumEcc - sumA2) / denominator;
        }

        /// <summary>
        /// MAN dyad census plus arc and dyadic reciprocity for a single 1-mode layer.
        /// For directed layers iterates all arcs in O(E); for undirected all non-null
        /// dyads are mutual by definition. A dyad is by definition a pair of two distinct
        /// nodes, so a self-tie (on a layer where selfties are allowed) never counts as one.
        /// </summary>
        internal static Dictionary<string, object> DyadCensus(uint[] nodeIds, ILayerOneMode layer)
        {
            int n = nodeIds.Length;
            long totalDyads = (long)n * (n - 1) / 2;

            if (layer.IsSymmetric)
            {
                long M = (long)layer.NbrEdges;
                if (layer.Selfties)
                    foreach (uint id in nodeIds)
                        if (layer.CheckEdgeExists(id, id))
                            M--;
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
                    if (v == egoId)
                        continue;
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

        /// <summary>
        /// MAN dyad census for a 2-mode layer, pseudo-projected: an affiliation has no direction,
        /// so this reduces to the same all-mutual shape as an undirected 1-mode layer (Asymmetric
        /// always 0, reciprocity measures trivially 1 whenever any Mutual dyad exists). Mutual is
        /// the exact count of distinct pairs sharing at least one hyperedge: the sum of each
        /// node's binary projected degree (<see cref="ILayer.GetNodeAlters"/>'s distinct-alter
        /// count, matching degree()'s own 2-mode convention) is, by the handshake lemma, exactly
        /// twice the number of distinct pairs - exact, no sampling. NOT O(sum of hyperedge sizes),
        /// though: GetNodeAlters re-scans a node's hyperedges from scratch on every call with no
        /// cross-call memoization, so summing it over every node costs O(sum of hyperedge sizes
        /// squared) - the same order as materializing a full binary projection, just never
        /// persisted. This is inherent to needing every node's true *deduplicated* degree (the
        /// same reason density()'s exact 2-mode path has this cost too), not a missed
        /// optimization.
        /// </summary>
        internal static Dictionary<string, object> DyadCensusTwoMode(uint[] nodeIds, ILayer layer)
        {
            int n = nodeIds.Length;
            long totalDyads = (long)n * (n - 1) / 2;
            long sumDegree = 0;
            foreach (uint id in nodeIds)
                sumDegree += layer.GetNodeAlters(id, EdgeTraversal.Both).Length;
            long M = sumDegree / 2;
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

        /// <summary>
        /// Per-node eccentricity: for each node, the length of the longest shortest path to any
        /// node reachable from it (0 for a node with no reachable others). Unlike <see cref="Diameter"/>,
        /// this is computed for every node individually rather than restricted to the largest
        /// weakly connected component — a node in a smaller component simply gets that component's
        /// eccentricity, and an isolated node gets 0. For directed layers, 'traversal' controls
        /// which direction BFS follows. Runs a full BFS from every node in parallel across up to
        /// 'maxthreads' threads (see 'setting()'); each source writes to its own array slot, so no
        /// locking is needed on the hot path.
        /// </summary>
        internal static Dictionary<uint, int> Eccentricity(uint[] nodeIds, ILayer layer, EdgeTraversal traversal)
        {
            GraphAlgorithms.SplitLayers([layer], out var oneModes, out var twoModesDynamic, out var twoModesStatic);
            var nodeIndex = new Dictionary<uint, int>(nodeIds.Length);
            for (int i = 0; i < nodeIds.Length; i++) nodeIndex[nodeIds[i]] = i;
            int[] ecc = new int[nodeIds.Length];
            System.Threading.Tasks.Parallel.For(0, nodeIds.Length, UserSettings.GetParallelOptions(), i =>
            {
                var dist = GraphAlgorithms.BFS(nodeIds[i], nodeIndex, nodeIds, oneModes, twoModesDynamic, twoModesStatic, traversal);
                int max = 0;
                foreach (int d in dist.Values)
                    if (d > max) max = d;
                ecc[i] = max;
            });

            var result = new Dictionary<uint, int>(nodeIds.Length);
            for (int i = 0; i < nodeIds.Length; i++)
                result[nodeIds[i]] = ecc[i];
            return result;
        }

        /// <summary>
        /// Exact network diameter and average path length for a single 1-mode layer, restricted
        /// to the largest weakly connected component (ignoring edge direction when finding that
        /// component — the convention used by igraph/NetworkX, since both statistics are only
        /// meaningful within a connected set of nodes). A full BFS is run from every node of the
        /// giant component — not an arbitrary subset — in parallel across up to 'maxthreads'
        /// threads (see 'setting()'); each source's reachable distances fold into shared
        /// max/sum/count accumulators under a lock. Diameter is the longest shortest path seen
        /// among reachable ordered pairs; AvgDistance is the mean over those same pairs. For
        /// directed layers, 'traversal' controls which direction BFS follows — unreachable pairs
        /// (possible even within the giant component when following direction) are simply
        /// excluded from both statistics, with PairsConsidered reporting how many were used.
        ///
        /// <paramref name="diameterOnly"/> (default false) trades AvgDistance/PairsConsidered
        /// away for a faster diameter-only computation: AvgDistance inherently needs a full BFS
        /// from every node regardless (no shortcut exists for a sum/mean the way one exists for a
        /// max), so this is a genuine opt-in, not a transparent speed-up of the default mode,
        /// which is completely unchanged. When true and the distances involved are symmetric (a
        /// 2-mode layer, a symmetric 1-mode layer, or an explicit EdgeTraversal.Both query), uses
        /// <see cref="DiameterIFUB"/> - exact, but often examining only a small fraction of nodes
        /// on real-world graphs (no improved worst-case bound, e.g. a path graph still needs most
        /// nodes examined - the win is empirical/typical-case, not asymptotic). Otherwise (a
        /// directed layer queried with Out/In traversal, where iFUB's exactness proof doesn't
        /// apply) falls back to the same exhaustive all-pairs pass as the default mode, just
        /// without keeping the sum/count needed for AvgDistance - same cost as today in that case,
        /// only the narrower output. Either way, diameterOnly's result always omits
        /// AvgDistance/PairsConsidered and adds Algorithm ("iFUB" or "AllPairs"), so the output
        /// shape is predictable regardless of which internal path actually ran.
        /// </summary>
        internal static Dictionary<string, object> Diameter(uint[] nodeIds, ILayer layer, EdgeTraversal traversal, bool diameterOnly = false)
        {
            uint[] component = LargestComponent(nodeIds, layer);
            int compSize = component.Length;
            var componentIndex = new Dictionary<uint, int>(component.Length);
            for (int i = 0; i < component.Length; i++) componentIndex[component[i]] = i;

            if (diameterOnly)
            {
                long diameterOnlyValue = 0;
                string algorithm = "AllPairs";
                if (compSize > 1)
                {
                    GraphAlgorithms.SplitLayers([layer], out var oneModesFast, out var twoModesDynamicFast, out var twoModesStaticFast);
                    bool effectivelySymmetric = layer is ILayerTwoMode
                        || traversal == EdgeTraversal.Both
                        || (layer is ILayerOneMode lom && lom.IsSymmetric);

                    if (effectivelySymmetric)
                    {
                        diameterOnlyValue = DiameterIFUB(component, componentIndex, oneModesFast, twoModesDynamicFast, twoModesStaticFast, traversal);
                        algorithm = "iFUB";
                    }
                    else
                    {
                        long maxDistFallback = 0;
                        var mergeLockFallback = new object();
                        System.Threading.Tasks.Parallel.For(0, component.Length, UserSettings.GetParallelOptions(),
                            () => 0L,
                            (i, loopState, localMax) =>
                            {
                                var dist = GraphAlgorithms.BFS(component[i], componentIndex, component, oneModesFast, twoModesDynamicFast, twoModesStaticFast, traversal);
                                foreach (int d in dist.Values)
                                    if (d > localMax) localMax = d;
                                return localMax;
                            },
                            localMax =>
                            {
                                lock (mergeLockFallback)
                                {
                                    if (localMax > maxDistFallback) maxDistFallback = localMax;
                                }
                            });
                        diameterOnlyValue = maxDistFallback;
                        algorithm = "AllPairs";
                    }
                }

                return new Dictionary<string, object>
                {
                    ["Method"] = "Exact",
                    ["Algorithm"] = algorithm,
                    ["Diameter"] = (int)diameterOnlyValue,
                    ["ComponentSize"] = compSize,
                    ["TotalNodes"] = nodeIds.Length
                };
            }

            long maxDist = 0;
            long sumDist = 0;
            long pairs = 0;

            if (compSize > 1)
            {
                GraphAlgorithms.SplitLayers([layer], out var oneModes, out var twoModesDynamic, out var twoModesStatic);
                var mergeLock = new object();
                System.Threading.Tasks.Parallel.For(0, component.Length, UserSettings.GetParallelOptions(),
                    () => (Max: 0L, Sum: 0L, Pairs: 0L),
                    (i, loopState, local) =>
                    {
                        uint src = component[i];
                        var dist = GraphAlgorithms.BFS(src, componentIndex, component, oneModes, twoModesDynamic, twoModesStatic, traversal);
                        foreach (var (node, d) in dist)
                        {
                            if (node == src) continue;
                            if (d > local.Max) local.Max = d;
                            local.Sum += d;
                            local.Pairs++;
                        }
                        return local;
                    },
                    local =>
                    {
                        lock (mergeLock)
                        {
                            if (local.Max > maxDist) maxDist = local.Max;
                            sumDist += local.Sum;
                            pairs += local.Pairs;
                        }
                    });
            }

            return new Dictionary<string, object>
            {
                ["Method"] = "Exact",
                ["Diameter"] = (int)maxDist,
                ["AvgDistance"] = pairs > 0 ? (double)sumDist / pairs : 0.0,
                ["PairsConsidered"] = pairs,
                ["ComponentSize"] = compSize,
                ["TotalNodes"] = nodeIds.Length
            };
        }

        /// <summary>
        /// Sampled estimate of network diameter and average path length, for networks too large
        /// for the exact full-BFS pass. Restricted to the largest weakly connected component, same
        /// as <see cref="Diameter"/>. AvgDistance is estimated from 'sampleSize' random sources
        /// drawn from that component: each source's own mean distance to its reachable nodes is
        /// one independent observation (its own BFS, not correlated raw pairwise distances), so the
        /// reported StandardError and 95% ConfidenceInterval use the normal approximation of a mean
        /// over those per-source means — not the Wilson score interval TriadicCensusSampled uses,
        /// which is specific to binomial proportions and would not apply to this continuous
        /// estimand. Diameter itself cannot be estimated this way: sampling a mean estimator never
        /// reliably sees the long tail that defines a maximum. Instead, 'numSweeps' parallel
        /// double-sweep trials (pick a random node, BFS to its farthest node u, BFS again from u)
        /// each yield a lower bound on the true diameter — often exact in practice, though not
        /// guaranteed, especially for directed layers — and the largest bound seen across trials is
        /// reported as DiameterLowerBound, deliberately not as a confidence interval, since it is an
        /// extreme-value statistic rather than a mean. The sweep trials and the sampled-source pass
        /// below both draw from Misc.Random, which is thread-static rather than seeded per worker
        /// thread, so exact reproducibility via randomseed() is only guaranteed when maxthreads is
        /// set to 1.
        /// </summary>
        internal static Dictionary<string, object> DiameterSampled(uint[] nodeIds, ILayer layer, EdgeTraversal traversal, int sampleSize, int numSweeps)
        {
            uint[] component = LargestComponent(nodeIds, layer);
            int compSize = component.Length;
            var componentIndex = new Dictionary<uint, int>(component.Length);
            for (int i = 0; i < component.Length; i++) componentIndex[component[i]] = i;
            GraphAlgorithms.SplitLayers([layer], out var oneModes, out var twoModesDynamic, out var twoModesStatic);

            long diameterLowerBound = 0;
            if (compSize > 1 && numSweeps > 0)
            {
                var sweepLock = new object();
                System.Threading.Tasks.Parallel.For(0, numSweeps, UserSettings.GetParallelOptions(),
                    () => 0L,
                    (i, loopState, localBest) =>
                    {
                        var (_, _, ecc) = DoubleSweepRoot(component, componentIndex, oneModes, twoModesDynamic, twoModesStatic, traversal);
                        return Math.Max(localBest, (long)ecc);
                    },
                    localBest =>
                    {
                        lock (sweepLock)
                        {
                            if (localBest > diameterLowerBound) diameterLowerBound = localBest;
                        }
                    });
            }

            uint[] sources = compSize > 0 ? CentralityFunctions.SampleNodes(component, sampleSize) : [];
            var perSourceMeans = new double[sources.Length];
            System.Threading.Tasks.Parallel.For(0, sources.Length, UserSettings.GetParallelOptions(), i =>
            {
                uint src = sources[i];
                var dist = GraphAlgorithms.BFS(src, componentIndex, component, oneModes, twoModesDynamic, twoModesStatic, traversal);
                long sum = 0;
                long count = 0;
                foreach (var (node, d) in dist)
                {
                    if (node == src) continue;
                    sum += d;
                    count++;
                }
                perSourceMeans[i] = count > 0 ? (double)sum / count : 0.0;
            });

            int k = perSourceMeans.Length;
            double mean = k > 0 ? perSourceMeans.Average() : 0.0;
            double variance = k > 1 ? perSourceMeans.Sum(m => (m - mean) * (m - mean)) / (k - 1) : 0.0;
            double se = k > 1 ? Math.Sqrt(variance / k) : 0.0;
            const double z = 1.959963984540054;

            return new Dictionary<string, object>
            {
                ["Method"] = "Sampled",
                ["DiameterLowerBound"] = (int)diameterLowerBound,
                ["NumSweeps"] = numSweeps,
                ["AvgDistance"] = mean,
                ["StandardError"] = se,
                ["ConfidenceIntervalLower"] = Math.Max(0.0, mean - z * se),
                ["ConfidenceIntervalUpper"] = mean + z * se,
                ["SampleSize"] = k,
                ["ComponentSize"] = compSize,
                ["TotalNodes"] = nodeIds.Length
            };
        }
        #endregion

        #region Methods (private)

        /// <summary>
        /// Finds the largest weakly connected component (edges treated as undirected, same as
        /// Functions.ConnectedComponents) via plain BFS/union. Used to restrict Diameter and
        /// DiameterSampled to the giant component, since diameter and average distance are only
        /// meaningful within a connected set of nodes.
        /// </summary>
        private static uint[] LargestComponent(uint[] nodeIds, ILayer layer)
        {
            GraphAlgorithms.SplitLayers([layer], out var oneModes, out var twoModesDynamic, out var twoModesStatic);
            var nodeIndex = new Dictionary<uint, int>(nodeIds.Length);
            for (int i = 0; i < nodeIds.Length; i++) nodeIndex[nodeIds[i]] = i;
            var scratch = new GraphAlgorithms.BFSReachScratch();
            var visited = new HashSet<uint>();
            uint[] bestComponent = [];
            foreach (uint start in nodeIds)
            {
                if (!visited.Add(start)) continue;
                var component = GraphAlgorithms.BFSReachedNodes(start, scratch, nodeIndex, nodeIds, oneModes, twoModesDynamic, twoModesStatic, EdgeTraversal.Both);
                foreach (uint v in component)
                    if (v != start) visited.Add(v);
                if (component.Count > bestComponent.Length)
                    bestComponent = component.ToArray();
            }
            return bestComponent;
        }

        /// <summary>
        /// Double-sweep heuristic: BFS from a random node in the component to find a likely-
        /// peripheral node, then BFS from that node. Returns that peripheral node, its full
        /// distance map, and its eccentricity (the max value in that map) - a cheap, often-exact
        /// lower bound on the true diameter on its own (used this way by
        /// <see cref="DiameterSampled"/>), and a good starting root for <see cref="DiameterIFUB"/>'s
        /// level-by-level refinement when distances are symmetric.
        /// </summary>
        private static (uint root, GraphAlgorithms.BFSDistances distFromRoot, int ecc) DoubleSweepRoot(uint[] component, Dictionary<uint, int> componentIndex, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
        {
            uint x = component[Misc.Random.Next(component.Length)];
            var dist1 = GraphAlgorithms.BFS(x, componentIndex, component, oneModes, twoModesDynamic, twoModesStatic, traversal);
            uint r = x;
            int best1 = 0;
            foreach (var (node, d) in dist1)
                if (d > best1) { best1 = d; r = node; }

            var distFromRoot = GraphAlgorithms.BFS(r, componentIndex, component, oneModes, twoModesDynamic, twoModesStatic, traversal);
            int ecc = 0;
            foreach (int d in distFromRoot.Values)
                if (d > ecc) ecc = d;
            return (r, distFromRoot, ecc);
        }

        /// <summary>
        /// Exact diameter via the iFUB algorithm (Crescenzi, Grossi, Lanzi &amp; Marino 2012):
        /// picks a likely-peripheral root via one double-sweep trial (<see cref="DoubleSweepRoot"/>),
        /// then processes BFS-tree levels (by distance from that root) from farthest to nearest,
        /// computing each unprocessed node's exact eccentricity (one more BFS per node,
        /// parallelized within the level) and tracking a running lower bound, stopping as soon as
        /// that lower bound meets the upper bound implied by the triangle inequality through the
        /// root: for root r with eccentricity eccR, any node x satisfies ecc(x) &lt;= dist(r,x) +
        /// eccR, so once every node at level i has a known exact eccentricity, any still-
        /// unprocessed node (level &lt; i) has ecc &lt;= (i-1)+eccR - if the running lower bound
        /// already meets that, it IS the exact diameter and the loop stops. This bound only holds
        /// when distances are symmetric; callers must check that before calling this, it is not
        /// re-checked here. Often examines only a small fraction of a component's nodes on
        /// real-world graphs; no improved worst-case bound (e.g. a path graph needs most nodes
        /// examined regardless) - exact either way, just not always fast.
        /// </summary>
        private static long DiameterIFUB(uint[] component, Dictionary<uint, int> componentIndex, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
        {
            var (_, distFromRoot, eccR) = DoubleSweepRoot(component, componentIndex, oneModes, twoModesDynamic, twoModesStatic, traversal);

            var levels = new Dictionary<int, List<uint>>();
            foreach (var (node, d) in distFromRoot)
            {
                if (!levels.TryGetValue(d, out var list))
                    levels[d] = list = [];
                list.Add(node);
            }

            long lb = eccR;
            var mergeLock = new object();
            for (int i = eccR; i >= 0; i--)
            {
                if (levels.TryGetValue(i, out var levelNodes) && levelNodes.Count > 0)
                {
                    System.Threading.Tasks.Parallel.For(0, levelNodes.Count, UserSettings.GetParallelOptions(),
                        () => 0L,
                        (j, loopState, localBest) =>
                        {
                            var dist = GraphAlgorithms.BFS(levelNodes[j], componentIndex, component, oneModes, twoModesDynamic, twoModesStatic, traversal);
                            int ecc = 0;
                            foreach (int d in dist.Values)
                                if (d > ecc) ecc = d;
                            return Math.Max(localBest, (long)ecc);
                        },
                        localBest =>
                        {
                            lock (mergeLock)
                            {
                                if (localBest > lb) lb = localBest;
                            }
                        });
                }

                if (i == 0) break;
                long ub = (i - 1) + eccR;
                if (lb >= ub) break;
            }
            return lb;
        }

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
        /// For directed layers, GetAllEgoData yields one arc per directed tie, which already
        /// matches Newman's directed formula (one (j,k) observation per arc: source's j-value,
        /// target's k-value). For undirected layers, GetAllEgoData yields each edge only once
        /// (from its lower-id endpoint, the usual dedup convention), but Newman's undirected
        /// formula requires counting every edge from BOTH endpoints - (j,k) and (k,j) - since
        /// an undirected edge has no inherent direction to break the symmetry. Skipping that
        /// second observation doesn't just halve the sample, it changes the correlation itself
        /// (e.g. a pure star graph would come out as r=0 instead of the correct r=-1), so the
        /// mirrored sample is added explicitly for symmetric layers.
        /// </summary>
        private static double PearsonEdgeCorrelation(ILayerOneMode layer,
            Dictionary<uint, double> jVals, Dictionary<uint, double> kVals)
        {
            double sumJK = 0, sumJ = 0, sumK = 0, sumJ2 = 0, sumK2 = 0;
            long M = 0;
            bool symmetric = layer.IsSymmetric;

            void AddSample(double j, double k)
            {
                sumJK += j * k;
                sumJ += j;
                sumK += k;
                sumJ2 += j * j;
                sumK2 += k * k;
                M++;
            }

            foreach (var (egoId, alters, _) in layer.GetAllEgoData())
            {
                bool haveEgoJ = jVals.TryGetValue(egoId, out double egoJ);
                bool haveEgoKVal = kVals.TryGetValue(egoId, out double egoK);
                bool haveEgoK = symmetric && haveEgoKVal;
                if (!haveEgoJ && !haveEgoK) continue;

                foreach (uint v in alters.Span)
                {
                    if (haveEgoJ && kVals.TryGetValue(v, out double altK))
                        AddSample(egoJ, altK);
                    if (haveEgoK && jVals.TryGetValue(v, out double altJ))
                        AddSample(altJ, egoK);
                }
            }

            if (M == 0) return 0.0;
            double denom = Math.Sqrt((M * sumJ2 - sumJ * sumJ) * (M * sumK2 - sumK * sumK));
            return denom == 0 ? 0.0 : (M * sumJK - sumJ * sumK) / denom;
        }
        #endregion
    }
}