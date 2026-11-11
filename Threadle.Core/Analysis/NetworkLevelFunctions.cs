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

            if (projection == ProjectionMethod.Count && ReferenceEquals(jVals, kVals))
            {
                // Closed-form per-hyperedge reduction. For a hyperedge's members with values
                // x_1..x_k (only members present in jVals count; a value-less member is excluded
                // exactly as the per-pair check below would exclude it), let S = Σx_i, Q = Σx_i².
                // Summed once per unordered pair, each counted from both directional samples the
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
                // Pair enumeration from each node's de-duplicated co-members, in parallel over nodes (up to
                // 'maxthreads'); per-thread sums merged under a lock. Floating-point sums are then added in
                // a thread-dependent order, so the last digits can differ between thread counts.
                var mergeLock = new object();
                System.Threading.Tasks.Parallel.For(0, nodeIds.Length, UserSettings.GetParallelOptions(),
                    () => new double[6],
                    (ui, loopState, acc) =>
                    {
                        uint u = nodeIds[ui];
                        bool haveUJ = jVals.TryGetValue(u, out double uJ);
                        bool haveUK = kVals.TryGetValue(u, out double uK);
                        if (!haveUJ && !haveUK) return acc;
                        var (alters, weights) = layer.GetNodeAltersWithWeights(u, EdgeTraversal.Both);
                        for (int i = 0; i < alters.Length; i++)
                        {
                            uint v = alters.Span[i];
                            if (v <= u) continue; // each unordered pair processed once, from the lower-id member
                            double w = (projection == ProjectionMethod.Count && weights.Length > 0) ? weights.Span[i] : 1.0;
                            if (haveUJ && kVals.TryGetValue(v, out double vK))
                                Add(acc, uJ, vK, w);
                            if (haveUK && jVals.TryGetValue(v, out double vJ))
                                Add(acc, vJ, uK, w);
                        }
                        return acc;
                    },
                    acc =>
                    {
                        lock (mergeLock)
                        {
                            sumJK += acc[0]; sumJ += acc[1]; sumK += acc[2];
                            sumJ2 += acc[3]; sumK2 += acc[4]; M += acc[5];
                        }
                    });

                static void Add(double[] a, double j, double k, double weight)
                {
                    a[0] += weight * j * k; a[1] += weight * j; a[2] += weight * k;
                    a[3] += weight * j * j; a[4] += weight * k * k; a[5] += weight;
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
        /// for an undirected 1-mode layer (see <see cref="CategoricalAssortativity"/>). Count mode
        /// uses a per-hyperedge closed form, O(sum of hyperedge sizes); Binary mode must count a pair
        /// once however many hyperedges it shares, so it enumerates each node's de-duplicated
        /// co-members, O(sum of hyperedge sizes squared).
        /// </summary>
        internal static double CategoricalAssortativityTwoMode(ILayer layer, Dictionary<uint, string> cats, uint[] nodeIds, ProjectionMethod projection)
        {
            var sourceCounts = new Dictionary<string, double>();
            var targetCounts = new Dictionary<string, double>();
            var matchCounts = new Dictionary<string, double>();
            double M = 0;

            if (projection == ProjectionMethod.Count)
            {
                // Closed form per hyperedge, O(sum of hyperedge sizes) instead of enumerating member
                // pairs: with c(a) members of category a among the hyperedge's k categorized members,
                // its unordered co-member pairs number k(k-1)/2, of which c(a)(c(a)-1)/2 lie within
                // category a, and each member of category a is an endpoint of k-1 of them. Summed over
                // hyperedges this is exactly the per-pair tally below with weight = shared-hyperedge
                // count (only the sum of the source and target tallies is used further down, so the
                // id-order split between them does not matter). All quantities are integer-valued, so
                // the sums are exact in double precision and the result equals the pairwise one.
                var histogram = new Dictionary<string, int>();
                foreach (var (_, memberIds) in ((ILayerTwoMode)layer).GetAllHyperedgeData())
                {
                    histogram.Clear();
                    int k = 0;
                    foreach (uint m in memberIds)
                    {
                        if (!cats.TryGetValue(m, out string? c)) continue;
                        histogram[c] = histogram.GetValueOrDefault(c) + 1;
                        k++;
                    }
                    if (k < 2) continue;
                    M += (double)k * (k - 1) / 2;
                    foreach (var (c, cnt) in histogram)
                    {
                        sourceCounts[c] = sourceCounts.GetValueOrDefault(c) + (double)cnt * (k - 1);
                        if (cnt > 1)
                            matchCounts[c] = matchCounts.GetValueOrDefault(c) + (double)cnt * (cnt - 1) / 2;
                    }
                }
            }
            else
            {
                // Binary: each node's de-duplicated co-members, in parallel over nodes (up to
                // 'maxthreads'); per-thread tallies merged under a lock. All tallies are integer-valued,
                // so the result does not depend on the thread count.
                var mergeLock = new object();
                System.Threading.Tasks.Parallel.For(0, nodeIds.Length, UserSettings.GetParallelOptions(),
                    () => (Src: new Dictionary<string, double>(), Tgt: new Dictionary<string, double>(), Match: new Dictionary<string, double>(), M: 0.0),
                    (ui, loopState, local) =>
                    {
                        uint u = nodeIds[ui];
                        if (!cats.TryGetValue(u, out string? cU)) return local;
                        var (alters, weights) = layer.GetNodeAltersWithWeights(u, EdgeTraversal.Both);
                        for (int i = 0; i < alters.Length; i++)
                        {
                            uint v = alters.Span[i];
                            if (v <= u) continue; // each unordered pair processed once, from the lower-id member
                            if (!cats.TryGetValue(v, out string? cV)) continue;
                            local.M += 1.0;
                            local.Src[cU] = local.Src.GetValueOrDefault(cU) + 1.0;
                            local.Tgt[cV] = local.Tgt.GetValueOrDefault(cV) + 1.0;
                            if (cU == cV)
                                local.Match[cU] = local.Match.GetValueOrDefault(cU) + 1.0;
                        }
                        return local;
                    },
                    local =>
                    {
                        lock (mergeLock)
                        {
                            M += local.M;
                            foreach (var (c, x) in local.Src) sourceCounts[c] = sourceCounts.GetValueOrDefault(c) + x;
                            foreach (var (c, x) in local.Tgt) targetCounts[c] = targetCounts.GetValueOrDefault(c) + x;
                            foreach (var (c, x) in local.Match) matchCounts[c] = matchCounts.GetValueOrDefault(c) + x;
                        }
                    });
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
            var bfsContext = GraphAlgorithms.BFSContext.Create(nodeIndex, nodeIds, oneModes, twoModesDynamic, twoModesStatic, traversal);
            System.Threading.Tasks.Parallel.For(0, nodeIds.Length, UserSettings.GetParallelOptions(),
                () => new GraphAlgorithms.BFSScratch(),
                (i, loopState, scratch) =>
                {
                    var dist = GraphAlgorithms.BFS(nodeIds[i], bfsContext, scratch);
                    int max = 0;
                    foreach (int d in dist.Values)
                        if (d > max) max = d;
                    ecc[i] = max;
                    return scratch;
                },
                _ => { });

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
        /// <see cref="DiameterExactSymmetric"/> (iFUB combined with BoundingDiameters) - exact, usually
        /// needing far fewer BFS runs than nodes on real-world graphs (no improved worst-case bound, e.g. a path graph still needs most
        /// nodes examined - the win is empirical/typical-case, not asymptotic). Otherwise (a
        /// directed layer queried with Out/In traversal, where the bounds' exactness proof doesn't
        /// apply) falls back to the same exhaustive all-pairs pass as the default mode, just
        /// without keeping the sum/count needed for AvgDistance - same cost as today in that case,
        /// only the narrower output. Either way, diameterOnly's result always omits
        /// AvgDistance/PairsConsidered and adds Algorithm ("iFUB+BoundingDiameters" or "AllPairs"), so the output
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
                        diameterOnlyValue = DiameterExactSymmetric(component, componentIndex, oneModesFast, twoModesDynamicFast, twoModesStaticFast, traversal);
                        algorithm = "iFUB+BoundingDiameters";
                    }
                    else
                    {
                        long maxDistFallback = 0;
                        var mergeLockFallback = new object();
                        var bfsContextFallback = GraphAlgorithms.BFSContext.Create(componentIndex, component, oneModesFast, twoModesDynamicFast, twoModesStaticFast, traversal);
                        System.Threading.Tasks.Parallel.For(0, component.Length, UserSettings.GetParallelOptions(),
                            () => (Max: 0L, Scratch: new GraphAlgorithms.BFSScratch()),
                            (i, loopState, local) =>
                            {
                                var dist = GraphAlgorithms.BFS(component[i], bfsContextFallback, local.Scratch);
                                foreach (int d in dist.Values)
                                    if (d > local.Max) local.Max = d;
                                return local;
                            },
                            local =>
                            {
                                lock (mergeLockFallback)
                                {
                                    if (local.Max > maxDistFallback) maxDistFallback = local.Max;
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
                var bfsContext = GraphAlgorithms.BFSContext.Create(componentIndex, component, oneModes, twoModesDynamic, twoModesStatic, traversal);
                System.Threading.Tasks.Parallel.For(0, component.Length, UserSettings.GetParallelOptions(),
                    () => (Max: 0L, Sum: 0L, Pairs: 0L, Scratch: new GraphAlgorithms.BFSScratch()),
                    (i, loopState, local) =>
                    {
                        uint src = component[i];
                        var dist = GraphAlgorithms.BFS(src, bfsContext, local.Scratch);
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
            if (sources.Length > 0)
            {
                var bfsContext = GraphAlgorithms.BFSContext.Create(componentIndex, component, oneModes, twoModesDynamic, twoModesStatic, traversal);
                System.Threading.Tasks.Parallel.For(0, sources.Length, UserSettings.GetParallelOptions(),
                    () => new GraphAlgorithms.BFSScratch(),
                    (i, loopState, scratch) =>
                    {
                        uint src = sources[i];
                        var dist = GraphAlgorithms.BFS(src, bfsContext, scratch);
                        long sum = 0;
                        long count = 0;
                        foreach (var (node, d) in dist)
                        {
                            if (node == src) continue;
                            sum += d;
                            count++;
                        }
                        perSourceMeans[i] = count > 0 ? (double)sum / count : 0.0;
                        return scratch;
                    },
                    _ => { });
            }

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
            var context = GraphAlgorithms.BFSContext.Create(nodeIndex, nodeIds, oneModes, twoModesDynamic, twoModesStatic, EdgeTraversal.Both);
            var (order, starts, _) = GraphAlgorithms.ComponentsInOrder(context);
            // The first largest component, nodes in discovery order (as the previous per-seed BFS returned them)
            int best = 0;
            for (int c = 1; c < starts.Length - 1; c++)
                if (starts[c + 1] - starts[c] > starts[best + 1] - starts[best])
                    best = c;
            if (starts.Length < 2)
                return [];
            var component = new uint[starts[best + 1] - starts[best]];
            for (int i = 0; i < component.Length; i++)
                component[i] = nodeIds[order[starts[best] + i]];
            return component;
        }

        /// <summary>
        /// Double-sweep heuristic: BFS from a random node in the component to find a likely-
        /// peripheral node, then BFS from that node. Returns that peripheral node, its full
        /// distance map, and its eccentricity (the max value in that map) - a cheap, often-exact
        /// lower bound on the true diameter on its own (used this way by
        /// <see cref="DiameterSampled"/>).
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
        /// Exact diameter of a component with symmetric distances, combining two exact methods that share one
        /// set of eccentricity bounds, so that it is fast where either of them is:
        /// - iFUB (Crescenzi, Grossi, Habib, Lanzi &amp; Marino 2013): BFS from a central root (double-sweep
        ///   midpoint), then BFS from the nodes of the root's BFS levels, outermost first; once every node at
        ///   level i or beyond is done, no pair can be further apart than 2(i - 1).
        /// - BoundingDiameters (Takes &amp; Kosters 2011): every node keeps a lower and an upper bound on its
        ///   eccentricity (after a BFS from v with eccentricity e, a node at distance d has eccentricity at least
        ///   max(d, e - d) and at most e + d); the diameter lies between the largest lower bound and the largest
        ///   upper bound (and at most 2e); BFS sources alternate between the remaining node with the largest
        ///   upper bound and the one with the smallest lower bound (ties: highest degree), and nodes that can no
        ///   longer move either diameter bound are dropped.
        /// Every BFS, whichever method chose it, tightens all bounds; iFUB skips nodes whose upper bound cannot
        /// raise the current diameter (a skipped node is as good as done for its stopping rule). Each round runs
        /// one BFS per available thread ('maxthreads'), half chosen by iFUB and half by BoundingDiameters, and
        /// the computation stops as soon as either stopping rule holds. iFUB alone needs a BFS from every node of
        /// its outermost levels, which on some dense graphs means tens of thousands of BFS; bounds alone stall on
        /// graphs where nearly all nodes have the same eccentricity. The combination costs at most about twice
        /// the better of the two.
        /// </summary>
        private static long DiameterExactSymmetric(uint[] component, Dictionary<uint, int> componentIndex, List<ILayerOneMode> oneModes, List<LayerTwoMode> twoModesDynamic, List<LayerTwoModeStatic> twoModesStatic, EdgeTraversal traversal)
        {
            int n = component.Length;
            if (n <= 1)
                return 0;
            var context = GraphAlgorithms.BFSContext.Create(componentIndex, component, oneModes, twoModesDynamic, twoModesStatic, traversal);

            // Degree proxy for tie-breaking: number of neighbour entries
            var degree = new long[n];
            for (int i = 0; i < n; i++)
            {
                uint id = component[i];
                foreach (var layer in oneModes)
                    degree[i] += layer.GetNodeAlters(id, traversal).Length;
                for (int li = 0; li < context.TwoModesStatic.Length; li++)
                {
                    var st = context.TwoModesStatic[li];
                    for (int k = st.NodeOffsets[i]; k < st.NodeOffsets[i + 1]; k++)
                    {
                        int h = st.NodeHyperedges[k];
                        degree[i] += st.HyperedgeOffsets[h + 1] - st.HyperedgeOffsets[h] - 1;
                    }
                }
                foreach (var layer in twoModesDynamic)
                    degree[i] += layer.GetNodeAlters(id, EdgeTraversal.Both).Length;
            }

            var lower = new int[n];
            var upper = new int[n];
            Array.Fill(upper, int.MaxValue);
            var active = new bool[n];
            Array.Fill(active, true);
            var done = new bool[n];
            int activeCount = n;
            long dl = 0, du = long.MaxValue;
            int threads = UserSettings.MaxDegreeOfParallelism > 0 ? UserSettings.MaxDegreeOfParallelism : Environment.ProcessorCount;
            threads = Math.Max(1, threads);
            var scratches = new GraphAlgorithms.BFSScratch[threads];
            for (int t = 0; t < threads; t++) scratches[t] = new GraphAlgorithms.BFSScratch();

            // Applies one BFS (source, distances) to all bounds; returns the source's eccentricity
            int Apply(int source, int[] dist)
            {
                int e = 0;
                for (int w = 0; w < n; w++)
                    if (dist[w] > e) e = dist[w];
                done[source] = true;
                dl = Math.Max(dl, e);
                du = Math.Min(du, 2L * e);
                for (int w = 0; w < n; w++)
                {
                    if (!active[w]) continue;
                    int d = dist[w];
                    int lo = Math.Max(d, e - d);
                    if (lo > lower[w]) lower[w] = lo;
                    int up = e + d;
                    if (up < upper[w]) upper[w] = up;
                    if (lower[w] > dl) dl = lower[w];
                }
                return e;
            }
            void Prune()
            {
                long maxUpper = dl;
                for (int w = 0; w < n; w++)
                {
                    if (!active[w]) continue;
                    if (lower[w] == upper[w] || (upper[w] <= dl && 2L * lower[w] >= du))
                    {
                        active[w] = false;
                        activeCount--;
                        continue;
                    }
                    if (upper[w] > maxUpper) maxUpper = upper[w];
                }
                du = Math.Min(du, maxUpper);
            }
            int[] Bfs(int source, int scratch = 0) => GraphAlgorithms.BFS(component[source], context, scratches[scratch]).RawDist;
            static int Farthest(int[] dist, int fallback)
            {
                int best = fallback, e = 0;
                for (int w = 0; w < dist.Length; w++)
                    if (dist[w] > e) { e = dist[w]; best = w; }
                return best;
            }

            // iFUB's central root: double sweep from a random node, root = midpoint of the r-y path
            int x = Misc.Random.Next(n);
            int[] dx = Bfs(x);
            Apply(x, dx);
            int r = Farthest(dx, x);
            int[] distR = (int[])Bfs(r).Clone();
            int eccR = Apply(r, distR);
            int y = Farthest(distR, r);
            int[] distY = Bfs(y);
            Apply(y, distY);
            int half = eccR / 2, m = r;
            for (int w = 0; w < n; w++)
                if (distR[w] == half && distY[w] == eccR - half) { m = w; break; }
            int[] distM = (int[])Bfs(m).Clone();
            int eccM = Apply(m, distM);
            Prune();

            var levels = new List<int>[eccM + 1];
            for (int i = 0; i <= eccM; i++) levels[i] = [];
            for (int w = 0; w < n; w++)
                levels[distM[w]].Add(w);
            int level = eccM, pos = 0;
            bool pickHigh = true;
            var chosen = new List<int>(threads);
            int ifubQuota = Math.Max(1, (threads + 1) / 2);

            while (dl < du)
            {
                chosen.Clear();
                // iFUB: next nodes of the outermost unfinished level that could still raise the diameter
                bool ifubFinished = false;
                while (chosen.Count < ifubQuota)
                {
                    if (pos >= levels[level].Count)
                    {
                        if (chosen.Count > 0) break;            // decide the level once this round's BFS are applied
                        if (dl >= 2L * (level - 1)) { ifubFinished = true; break; }
                        level--;
                        pos = 0;
                        continue;
                    }
                    int w = levels[level][pos++];
                    if (done[w] || upper[w] <= dl) continue;
                    chosen.Add(w);
                }
                if (ifubFinished)
                    return dl;
                // BoundingDiameters: fill the round, alternating largest upper / smallest lower bound
                while (chosen.Count < threads)
                {
                    int best = -1;
                    for (int w = 0; w < n; w++)
                    {
                        if (!active[w] || done[w] || chosen.Contains(w)) continue;
                        if (best < 0) { best = w; continue; }
                        bool better = pickHigh
                            ? upper[w] > upper[best] || (upper[w] == upper[best] && degree[w] > degree[best])
                            : lower[w] < lower[best] || (lower[w] == lower[best] && degree[w] > degree[best]);
                        if (better) best = w;
                    }
                    if (best < 0) break;
                    chosen.Add(best);
                    pickHigh = !pickHigh;
                }
                if (chosen.Count == 0)
                    break;

                var dists = new int[chosen.Count][];
                System.Threading.Tasks.Parallel.For(0, chosen.Count, UserSettings.GetParallelOptions(), t =>
                    dists[t] = Bfs(chosen[t], t));
                for (int t = 0; t < chosen.Count; t++)
                    Apply(chosen[t], dists[t]);
                Prune();
            }
            return dl;
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