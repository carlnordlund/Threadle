using System.Globalization;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.Core.Analysis
{
    public record ShortestPathResult(int Distance, uint[]? Path);

    /// <summary>
    /// A collection of public-facing (and fairly simple) network analysis functions accessible by the frontend.
    /// </summary>
    public static class Analyses
    {
        #region Methods (public)

        /// <summary>
        /// Detects communities via Leiden (Traag, Waltman &amp; van Eck 2019) for the specified 1-mode
        /// layer(s), storing the community index as an integer node attribute. Like
        /// CommunityDetectionLouvain, this alternates local-moving and aggregation, but inserts a
        /// randomized refinement step before each aggregation that guarantees every resulting
        /// community is internally connected — a node is only ever merged into a sub-community it
        /// shares a direct edge with, fixing a known Louvain flaw where aggregation can occasionally
        /// strand a node in a community it has no real path through. randomness (default 0.01, θ in
        /// the original paper) controls how much refinement's merge choices deviate from picking the
        /// single best candidate — values near 0 are effectively greedy, larger values explore more
        /// and can escape local optima that a purely greedy method gets stuck in across repeated
        /// runs. resolution behaves as in CommunityDetectionLouvain (this uses the same
        /// resolution-scaled modularity objective, not the CPM objective from the original paper, so
        /// results are directly comparable to Louvain's). Directed layers are symmetrized and
        /// multiple layers combine via summed edge weight, matching CommunityDetectionLouvain.
        /// layerNames may instead be a single 2-mode layer on its own, not combined with other
        /// layers (pseudo-projected: traversed node-hyperedge-node directly, without materializing
        /// a 1-mode projection; tie strength is always count-weighted, matching
        /// eigenvectorcentrality()'s convention — no binary option. Local moving itself (shared by
        /// count and a hypothetical binary alike) already costs O(Σ hyperedge sizes²) per sweep,
        /// not O(Σ hyperedge sizes): it calls the non-memoized GetNodeAltersWithWeights once per
        /// node per sweep, the same aggregate cost degree()/pagerank() carry, repeated every sweep
        /// until convergence — so level 0's real cost is O(sweeps·Σ hyperedge sizes²), not the
        /// linear cost a "nothing materialized" framing alone might suggest. Binary would cost
        /// about the same there; what it would add is at aggregation (BuildFirstAggregationTwoMode's
        /// per-hyperedge histogram discards individual pair identity), needing one further
        /// O(Σ hyperedge sizes²) pass to recover it — real, but smaller than local moving's own
        /// recurring cost, which is paid either way). On a 2-mode layer, only the first
        /// (hyperedge-direct) level skips the usual refinement step, so its internal-connectivity
        /// guarantee is not established for communities formed entirely within that first pass —
        /// refinement (and its connectivity guarantee) applies normally from the first aggregation
        /// onward. Returns a summary with NbrCommunities, CommunitySizes (descending),
        /// the achieved Modularity score Q, and NumRestarts. When numRestarts &gt; 1, runs that
        /// many independent restarts in parallel across up to 'maxthreads' threads (see
        /// 'setting()') and keeps the highest-modularity result — a way to escape local optima
        /// that a single run can get stuck in. Exact reproducibility via randomseed() across
        /// restarts is only guaranteed when maxthreads is set to 1.
        /// </summary>
        public static OperationResult<Dictionary<string, object>> CommunityDetectionLeiden(Network network, string[]? layerNames, string? attrName = null, double resolution = 1.0, double randomness = 0.01, int numRestarts = 1)
        {
            if (!TryResolveLayersForCommunityDetection(network, layerNames, out var oneModes, out var twoMode, out var err))
                return err!;

            attrName = ResolveCommunityAttrName(layerNames, attrName);
            uint[] nodeIds = network.Nodeset.NodeIdArray;
            var (communities, modularity) = twoMode != null
                ? CommunityFunctions.LeidenCommunitiesTwoMode(nodeIds, twoMode, resolution, randomness, numRestarts)
                : CommunityFunctions.LeidenCommunities(nodeIds, oneModes, resolution, randomness, numRestarts);
            return StoreCommunityResult(network, attrName, communities, modularity, numRestarts);
        }

        /// <summary>
        /// Detects communities via Louvain modularity optimization (Blondel et al. 2008) for the
        /// specified 1-mode layer(s), storing the community index as an integer node attribute.
        /// Directed layers are symmetrized (arcs in both directions accumulate into an undirected
        /// weight) since modularity optimization is inherently undirected — the same convention
        /// used by Constraint/EffectiveSize. Multiple layers combine via summed edge weight.
        /// layerNames may instead be a single 2-mode layer on its own, not combined with other
        /// layers (pseudo-projected: traversed node-hyperedge-node directly, without materializing
        /// a 1-mode projection; tie strength is always count-weighted, matching
        /// eigenvectorcentrality()'s convention — no binary option. Local moving itself (shared by
        /// count and a hypothetical binary alike) already costs O(Σ hyperedge sizes²) per sweep,
        /// not O(Σ hyperedge sizes): it calls the non-memoized GetNodeAltersWithWeights once per
        /// node per sweep, the same aggregate cost degree()/pagerank() carry, repeated every sweep
        /// until convergence — so level 0's real cost is O(sweeps·Σ hyperedge sizes²), not the
        /// linear cost a "nothing materialized" framing alone might suggest. Binary would cost
        /// about the same there; what it would add is at aggregation (BuildFirstAggregationTwoMode's
        /// per-hyperedge histogram discards individual pair identity), needing one further
        /// O(Σ hyperedge sizes²) pass to recover it — real, but smaller than local moving's own
        /// recurring cost, which is paid either way).
        /// resolution (default 1.0 = standard modularity) scales the null-model term: values above
        /// 1 favor more, smaller communities; values below 1 favor fewer, larger ones (Reichardt &amp;
        /// Bornholdt 2006). Returns a summary with NbrCommunities, CommunitySizes (descending), the
        /// achieved Modularity score Q, and NumRestarts. When numRestarts &gt; 1, runs that many
        /// independent restarts in parallel across up to 'maxthreads' threads (see 'setting()') and
        /// keeps the highest-modularity result — a way to escape local optima that a single run can
        /// get stuck in. Exact reproducibility via randomseed() across restarts is only guaranteed
        /// when maxthreads is set to 1.
        /// </summary>
        public static OperationResult<Dictionary<string, object>> CommunityDetectionLouvain(Network network, string[]? layerNames, string? attrName = null, double resolution = 1.0, int numRestarts = 1)
        {
            if (!TryResolveLayersForCommunityDetection(network, layerNames, out var oneModes, out var twoMode, out var err))
                return err!;

            attrName = ResolveCommunityAttrName(layerNames, attrName);
            uint[] nodeIds = network.Nodeset.NodeIdArray;
            var (communities, modularity) = twoMode != null
                ? CommunityFunctions.LouvainCommunitiesTwoMode(nodeIds, twoMode, resolution, numRestarts)
                : CommunityFunctions.LouvainCommunities(nodeIds, oneModes, resolution, numRestarts);
            return StoreCommunityResult(network, attrName, communities, modularity, numRestarts);
        }

        /// <summary>
        /// Detects communities via asynchronous label propagation (Raghavan, Albert &amp; Kumara 2007)
        /// for the specified 1-mode layer(s), storing the community index as an integer node
        /// attribute. Unlike Louvain, this does not optimize modularity — each node repeatedly
        /// adopts the most common label among its current neighbors until labels stop changing or
        /// maxIterations passes are reached (a cap is required since, unlike Louvain's local-moving
        /// phase, label propagation has no guarantee of convergence). Directed layers are
        /// symmetrized and multiple layers combine via summed edge weight, matching
        /// CommunityDetectionLouvain. layerNames may instead be a single 2-mode layer on its own,
        /// not combined with other layers (pseudo-projected: traversed node-hyperedge-node
        /// directly, without materializing a 1-mode projection; tie strength is always
        /// count-weighted — no binary option, for the same reason as CommunityDetectionLouvain).
        /// Returns a summary with NbrCommunities, CommunitySizes (descending), the achieved
        /// Modularity score Q of the resulting partition (reported for comparability with other
        /// methods, not something this method optimizes for), and NumRestarts. When numRestarts &gt; 1,
        /// runs that many independent restarts in parallel across up to 'maxthreads' threads (see
        /// 'setting()') and keeps the one with the highest modularity — a secondary selection
        /// criterion here, since LPA itself doesn't target modularity. Exact reproducibility via
        /// randomseed() across restarts is only guaranteed when maxthreads is set to 1.
        /// </summary>
        public static OperationResult<Dictionary<string, object>> CommunityDetectionLabelPropagation(Network network, string[]? layerNames, string? attrName = null, int maxIterations = 100, int numRestarts = 1)
        {
            if (!TryResolveLayersForCommunityDetection(network, layerNames, out var oneModes, out var twoMode, out var err))
                return err!;

            attrName = ResolveCommunityAttrName(layerNames, attrName);
            uint[] nodeIds = network.Nodeset.NodeIdArray;
            var (communities, modularity) = twoMode != null
                ? CommunityFunctions.LabelPropagationCommunitiesTwoMode(nodeIds, twoMode, maxIterations, numRestarts)
                : CommunityFunctions.LabelPropagationCommunities(nodeIds, oneModes, maxIterations, numRestarts);
            return StoreCommunityResult(network, attrName, communities, modularity, numRestarts);
        }

        /// <summary>
        /// Detects communities via LPAm (Barber &amp; Clark 2009) for the specified 1-mode layer(s),
        /// storing the community index as an integer node attribute. Like CommunityDetectionLabelPropagation,
        /// every node starts with a unique label and repeatedly adopts a label from among its
        /// neighbors — but here each move is only accepted if it actually increases modularity,
        /// rather than simply following the majority. This fixes vanilla label propagation's
        /// tendency to collapse a well-connected network into one "monster community": a move that
        /// would merge everything together has negative modularity gain once the resulting
        /// community's internal density stops exceeding the null model, so it is never taken. Unlike
        /// CommunityDetectionLouvain, LPAm runs a single level with no aggregation step afterwards,
        /// so it typically finds more, smaller communities than Louvain since it cannot escape local
        /// optima the same way. Directed layers are symmetrized and multiple layers combine via
        /// summed edge weight, matching CommunityDetectionLouvain. layerNames may instead be a
        /// single 2-mode layer on its own, not combined with other layers (pseudo-projected:
        /// traversed node-hyperedge-node directly, without materializing a 1-mode projection; tie
        /// strength is always count-weighted — no binary option, for the same reason as
        /// CommunityDetectionLouvain). Returns a summary with NbrCommunities, CommunitySizes
        /// (descending), the achieved Modularity score Q, and NumRestarts. When numRestarts &gt; 1,
        /// runs that many independent restarts in parallel across up to 'maxthreads' threads (see
        /// 'setting()') and keeps the highest-modularity result. Exact reproducibility via
        /// randomseed() across restarts is only guaranteed when maxthreads is set to 1.
        /// </summary>
        public static OperationResult<Dictionary<string, object>> CommunityDetectionLPAm(Network network, string[]? layerNames, string? attrName = null, int numRestarts = 1)
        {
            if (!TryResolveLayersForCommunityDetection(network, layerNames, out var oneModes, out var twoMode, out var err))
                return err!;

            attrName = ResolveCommunityAttrName(layerNames, attrName);
            uint[] nodeIds = network.Nodeset.NodeIdArray;
            var (communities, modularity) = twoMode != null
                ? CommunityFunctions.LPAmCommunitiesTwoMode(nodeIds, twoMode, numRestarts)
                : CommunityFunctions.LPAmCommunities(nodeIds, oneModes, numRestarts);
            return StoreCommunityResult(network, attrName, communities, modularity, numRestarts);
        }

        /// <summary>
        /// Detects communities via Girvan-Newman (2002) for the specified 1-mode layer(s), storing
        /// the community index as an integer node attribute. Unlike CommunityDetectionLouvain/
        /// Leiden/LabelPropagation/LPAm, which build communities up from singletons (agglomerative),
        /// this tears the network down one edge at a time (divisive): repeatedly finds the edge with
        /// the highest edge betweenness among the currently-remaining edges, removes it, and scores
        /// the resulting connected-component partition by modularity against the *original*,
        /// un-cut graph. The partition with the highest modularity seen across every removal step is
        /// returned. Directed layers are symmetrized and multiple layers combine via summed edge
        /// weight, matching every other community detection method; only 1-mode layers are accepted
        /// — project 2-mode layers first. Because a full edge-betweenness recomputation is needed
        /// after every single edge removal, this is substantially more expensive than the
        /// agglomerative methods — O(V·E²) in the worst case — so it is best suited to smaller
        /// networks; the per-removal betweenness pass itself runs in parallel across up to
        /// 'maxthreads' threads (see 'setting()'). Deterministic given the graph (ties for highest
        /// betweenness are broken by lowest node-index pair), so unlike the other methods this has no
        /// numRestarts. Returns NbrCommunities, CommunitySizes (descending), and the achieved
        /// Modularity score. Reference: Girvan &amp; Newman (2002) doi:10.1073/pnas.122653799.
        /// </summary>
        public static OperationResult<Dictionary<string, object>> CommunityDetectionGirvanNewman(Network network, string[]? layerNames, string? attrName = null, double resolution = 1.0)
        {
            if (!TryResolveOneModeLayersForCommunityDetection(network, layerNames, out var oneModes, out var err))
                return err!;

            attrName = ResolveCommunityAttrName(layerNames, attrName);
            uint[] nodeIds = network.Nodeset.NodeIdArray;
            var (communities, modularity) = CommunityFunctions.GirvanNewmanCommunities(nodeIds, oneModes, resolution);
            return StoreCommunityResult(network, attrName, communities, modularity, numRestarts: 1);
        }

        /// <summary>
        /// Computes the Holland-Leinhardt triadic census for a single 1-mode layer: counts of the
        /// 16 isomorphism classes of directed triads (003, 012, 102, 021D, 021U, 021C, 111D, 111U,
        /// 030T, 030C, 201, 120D, 120U, 120C, 210, 300), plus the total number of triples and a
        /// "Method" field ("Exact" or "Sampled"). For symmetric layers every present dyad is mutual,
        /// so only 003/102/201/300 can be non-zero.
        /// When sampleSize is 0 (default), computes the exact census — cost scales with the sum of
        /// squared node degrees, not network size, so this can still be expensive for networks with
        /// high-degree hubs; both passes run in parallel across up to 'maxthreads' threads (see
        /// 'setting()'). When sampleSize > 0, instead classifies that many uniformly-random
        /// node triples and extrapolates: the result additionally carries "SampleSize" and, per
        /// triad type, a "StandardErrors", "ConfidenceIntervalLower" and "ConfidenceIntervalUpper"
        /// entry (95% Wilson score interval on the estimated count — chosen over the naive Wald
        /// interval because it stays non-degenerate when a type is observed zero times in the
        /// sample, which is routine for rare types). Because empty (003) triads dominate real
        /// sparse networks, rare types will still have wide intervals unless the sample is large.
        /// Reference: Holland &amp; Leinhardt (1970) doi:10.1086/224727; Batagelj &amp; Mrvar (2001).
        /// </summary>
        public static OperationResult<Dictionary<string, object>> TriadicCensus(Network network, string layerName, int sampleSize = 0)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult<Dictionary<string, object>>.Fail("NodesMissing", "Network has no nodes.");
            var lr = network.GetOneModeLayerForRead(layerName);
            if (!lr.Success)
                return OperationResult<Dictionary<string, object>>.Fail(lr.Code, lr.Message);
            var result = sampleSize > 0
                ? NetworkLevelFunctions.TriadicCensusSampled(network.Nodeset.NodeIdArray, lr.Value!, sampleSize)
                : NetworkLevelFunctions.TriadicCensus(network.Nodeset.NodeIdArray, lr.Value!);
            return OperationResult<Dictionary<string, object>>.Ok(result);
        }

        /// <summary>
        /// Calculates each node's eccentricity (the length of the longest shortest path to any
        /// node reachable from it) in the specified 1-mode layer, storing the result as an int
        /// node attribute. Unlike Diameter, this is computed for every node individually — not
        /// restricted to the largest weakly connected component — so a node in a smaller component
        /// gets its own component's eccentricity, and an isolated node gets 0. For directed
        /// layers, 'traversal' controls which direction BFS follows. The per-node BFS passes run
        /// in parallel across up to 'maxthreads' threads (see 'setting()').
        /// </summary>
        public static OperationResult Eccentricity(Network network, string layerName, string? attrName = null, EdgeTraversal traversal = EdgeTraversal.Out)
        {
            var lr = network.GetLayer(layerName);
            if (!lr.Success)
                return OperationResult.Fail(lr.Code, lr.Message);
            uint[] nodeIds = network.Nodeset.NodeIdArray;
            var ecc = NetworkLevelFunctions.Eccentricity(nodeIds, lr.Value!, traversal);
            attrName = string.IsNullOrEmpty(attrName) ? "eccentricity" : attrName;
            var attrDict = ecc.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Int);
        }

        /// <summary>
        /// Network diameter and average path length for a single 1-mode layer, restricted to the
        /// largest weakly connected component (ComponentSize vs TotalNodes are both reported for
        /// transparency) — the convention used by igraph/NetworkX, since both statistics are only
        /// meaningful within a connected set of nodes. When sampleSize is 0 (default), computes
        /// both exactly via a full BFS from every node in the giant component, run in parallel
        /// across up to 'maxthreads' threads (see 'setting()'); the result carries Diameter,
        /// AvgDistance, PairsConsidered and Method = "Exact". When sampleSize &gt; 0, AvgDistance is
        /// instead estimated from that many random sources in the giant component, each source's
        /// own mean distance to its reachable nodes treated as one independent observation — the
        /// result carries a StandardError and 95% confidence interval (normal approximation of a
        /// mean) for AvgDistance. Diameter cannot be estimated the same way — sampling a mean
        /// estimator never reliably sees the long tail that defines a maximum — so the sampled
        /// result instead reports a DiameterLowerBound from 'numSweeps' parallel double-sweep
        /// trials (pick a random node, BFS to its farthest node u, BFS again from u — the largest
        /// distance seen across trials is a lower bound on the true diameter, often exact in
        /// practice but not guaranteed, especially for directed layers): a lower bound, not a
        /// confidence interval, since it is an extreme-value statistic rather than a mean. For
        /// directed layers, 'traversal' controls which direction BFS follows; unreachable pairs
        /// (possible even within the giant component when following direction) are simply
        /// excluded from both statistics.
        ///
        /// diameterOnly (default false, only consulted when sampleSize is 0) trades AvgDistance/
        /// PairsConsidered away for a faster diameter-only computation, since AvgDistance
        /// inherently needs a full BFS from every node regardless of whether only the diameter is
        /// wanted - a genuine opt-in, not a transparent speed-up of the default (unchanged) mode.
        /// When the layer/traversal combination is effectively symmetric (a 2-mode layer, a
        /// symmetric 1-mode layer, or traversal=Both), uses the iFUB algorithm (Crescenzi, Grossi,
        /// Lanzi &amp; Marino 2012) - exact, but often examining only a small fraction of a
        /// component's nodes on real-world graphs (no improved worst-case bound - e.g. a path
        /// graph still needs most nodes examined, so this is an empirical/typical-case win, not an
        /// asymptotic one). Otherwise falls back to the same exhaustive pass as the default mode,
        /// same cost, just without AvgDistance/PairsConsidered in the result. Either way, the
        /// result also carries Algorithm ("iFUB" or "AllPairs") so a caller can see which path ran.
        /// </summary>
        public static OperationResult<Dictionary<string, object>> Diameter(Network network, string layerName, int sampleSize = 0, int numSweeps = 4, EdgeTraversal traversal = EdgeTraversal.Out, bool diameterOnly = false)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult<Dictionary<string, object>>.Fail("NodesMissing", "Network has no nodes.");
            var lr = network.GetLayer(layerName);
            if (!lr.Success)
                return OperationResult<Dictionary<string, object>>.Fail(lr.Code, lr.Message);
            var diameterResult = sampleSize > 0
                ? NetworkLevelFunctions.DiameterSampled(network.Nodeset.NodeIdArray, lr.Value!, traversal, sampleSize, numSweeps)
                : NetworkLevelFunctions.Diameter(network.Nodeset.NodeIdArray, lr.Value!, traversal, diameterOnly);
            return OperationResult<Dictionary<string, object>>.Ok(diameterResult);
        }

        /// <summary>
        /// Small-world coefficient sigma (Humphries &amp; Gurney 2008) for a single 1-mode layer:
        /// (C_observed / C_random) / (L_observed / L_random), where C is the average local
        /// clustering coefficient and L is the average shortest-path length, and the "random"
        /// reference is the mean of 'nullTrials' independent degree-preserving rewirings of the
        /// layer (rewire()'s default 2-edge swap, discarded after measuring — never added to the
        /// network). sigma &gt; 1 indicates small-world structure: markedly more clustered than a
        /// comparable random graph, without a correspondingly longer average path length.
        /// Clustering uses whichever formula clusteringcoefficient() would auto-select for the
        /// layer's type; average path length is diameter()'s AvgDistance, restricted to the
        /// largest weakly connected component in both the observed and each random-reference
        /// network, for a like-for-like comparison. For directed layers, 'traversal' controls
        /// which direction BFS follows for the path-length measurements (clustering has its own
        /// direction-aware formula regardless). Reports only sigma, not Telesford et al. (2011)'s
        /// omega, which additionally needs a ring-lattice reference Threadle does not currently
        /// generate.
        /// </summary>
        public static OperationResult<Dictionary<string, object>> SmallWorldCoefficient(Network network, string layerName, int nullTrials = 10, EdgeTraversal traversal = EdgeTraversal.Out)
        {
            if (nullTrials < 1)
                return OperationResult<Dictionary<string, object>>.Fail("InvalidArgument", "'nullTrials' must be at least 1.");
            var lr = network.GetOneModeLayerForRead(layerName);
            if (!lr.Success)
                return OperationResult<Dictionary<string, object>>.Fail(lr.Code, lr.Message);
            ILayerOneMode layer = lr.Value!;
            uint[] nodeIds = network.Nodeset.NodeIdArray;

            ClusteringMethod method = LocalStructureFunctions.DetectMethod([layer]);
            double cObserved = AverageClustering(nodeIds, layer, method);
            double lObserved = (double)NetworkLevelFunctions.Diameter(nodeIds, layer, traversal)["AvgDistance"];

            double cRandSum = 0.0, lRandSum = 0.0;
            for (int t = 0; t < nullTrials; t++)
            {
                LayerOneMode randomLayer = NetworkProcessor.BuildRewiredLayer(layer, nodeIds, layerName + "-nullmodel", 0, false, 0, out _, out _);
                cRandSum += AverageClustering(nodeIds, randomLayer, method);
                lRandSum += (double)NetworkLevelFunctions.Diameter(nodeIds, randomLayer, traversal)["AvgDistance"];
            }
            double cRandom = cRandSum / nullTrials;
            double lRandom = lRandSum / nullTrials;

            double sigma = (cRandom > 0 && lObserved > 0 && lRandom > 0)
                ? (cObserved / cRandom) / (lObserved / lRandom)
                : double.NaN;

            return OperationResult<Dictionary<string, object>>.Ok(new Dictionary<string, object>
            {
                ["Sigma"] = sigma,
                ["ObservedClustering"] = cObserved,
                ["ObservedAvgDistance"] = lObserved,
                ["RandomClustering"] = cRandom,
                ["RandomAvgDistance"] = lRandom,
                ["NullTrials"] = nullTrials
            });
        }

        /// <summary>Average local clustering coefficient over all nodes (0.0 if there are none).</summary>
        private static double AverageClustering(uint[] nodeIds, ILayerOneMode layer, ClusteringMethod method)
        {
            var values = LocalStructureFunctions.ClusteringCoefficient(nodeIds, [layer], method);
            return values.Count > 0 ? values.Values.Average() : 0.0;
        }

        /// <summary>
        /// Resolves layerNames (or, if null, every layer in the network) for Constraint/
        /// EffectiveSize: any number of 1-mode layers without selfties, or exactly one 2-mode
        /// layer on its own (pseudo-projected: traversed node-hyperedge-node directly, without
        /// materializing a 1-mode projection) — a 2-mode layer cannot be combined with any other
        /// layer, since the exact multilayer tie-strength combination (<see
        /// cref="LocalStructureFunctions.StructuralHoles"/>) is only defined across 1-mode layers.
        /// </summary>
        private static OperationResult ResolveStructuralHolesLayers(Network network, string[]? layerNames, string metricName, out List<ILayerOneMode> oneModes, out ILayer? twoMode)
        {
            oneModes = [];
            twoMode = null;
            string? twoModeLayerName = null;

            IEnumerable<KeyValuePair<string, ILayer>> candidates;
            if (layerNames == null)
                candidates = network.Layers;
            else
            {
                var list = new List<KeyValuePair<string, ILayer>>(layerNames.Length);
                foreach (string name in layerNames)
                {
                    var lr = network.GetLayer(name);
                    if (!lr.Success) return OperationResult.Fail(lr.Code, lr.Message);
                    list.Add(new(name, lr.Value!));
                }
                candidates = list;
            }

            foreach (var (name, layer) in candidates)
            {
                if (layer is ILayerTwoMode)
                {
                    if (twoMode != null || oneModes.Count > 0)
                        return OperationResult.Fail("InvalidLayerType",
                            $"Layer '{name}' is a 2-mode layer. {metricName} supports a single 2-mode layer on its own (with samplesize > 0), not combined with other layers.");
                    twoMode = layer;
                    twoModeLayerName = name;
                }
                else if (layer is ILayerOneMode lom)
                {
                    if (twoMode != null)
                        return OperationResult.Fail("InvalidLayerType",
                            $"Layer '{name}' is a 1-mode layer, but 2-mode layer '{twoModeLayerName}' was already specified. {metricName} supports a single 2-mode layer on its own, not combined with other layers.");
                    if (lom.Selfties)
                        return OperationResult.Fail("LayerAllowsSelfties",
                            $"Layer '{name}' allows selfties. Burt's {metricName.ToLowerInvariant()} has no defined treatment of a tie from a node to itself, so it is refused for any layer where selfties are allowed — even if none are actually present.");
                    oneModes.Add(lom);
                }
            }

            if (twoMode == null && oneModes.Count == 0)
                return OperationResult.Fail("NoLayers", "No layers found.");
            return OperationResult.Ok();
        }

        /// <summary>
        /// Computes Burt's constraint for each node in the specified layer(s) and stores the
        /// result as a float node attribute. Constraint C(i) = Σ_j (p_ij + Σ_q p_iq p_qj)² where
        /// p_ij is i's proportion of interaction with j. Ranges from ~0 (broker, many structural
        /// holes) to ~1 (fully embedded in dense clique). Accepts any number of 1-mode layers, or
        /// a single 2-mode layer on its own (pseudo-projected: traversed node-hyperedge-node
        /// directly, without materializing a 1-mode projection; tie strength is the
        /// count-weighted shared-hyperedge count). Because a 2-mode ego network is every
        /// co-member and the per-ego cost grows with the square of that count, a 2-mode layer
        /// requires samplesize > 0: only that many randomly sampled nodes get an (exact, not
        /// approximated) value, and all others are left unset. Burt's formula has no defined
        /// treatment of a tie from a node to itself, so a 1-mode layer where selfties are allowed
        /// is always refused, even if it has none. The per-node O(k²) computation (shared with
        /// EffectiveSize) runs in parallel across up to 'maxthreads' threads (see 'setting()').
        /// Reference: Burt (1992) Structural Holes; Burt (2004) doi:10.1086/421787.
        /// </summary>
        public static OperationResult Constraint(Network network, string[]? layerNames, string? attrName = null, int sampleSize = 0)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult.Fail("NodesMissing", "Network has no nodes.");

            var resolved = ResolveStructuralHolesLayers(network, layerNames, "Constraint", out var oneModes, out var twoMode);
            if (!resolved.Success)
                return OperationResult.Fail(resolved.Code, resolved.Message);

            string layerTag = layerNames?.Length == 1 ? layerNames[0] + "_" : (layerNames == null ? "" : "multilayer_");
            attrName = !string.IsNullOrEmpty(attrName) ? attrName : layerTag + "constraint";

            Dictionary<uint, double> c;
            if (twoMode != null)
            {
                if (sampleSize <= 0)
                    return OperationResult.Fail("SampleSizeRequired",
                        "Constraint on a 2-mode layer requires samplesize > 0: a 2-mode ego network's cost grows with the square of its (potentially large) projected degree, so an exact value for every node isn't offered.");
                uint[] sampledIds = CentralityFunctions.SampleNodes(network.Nodeset.NodeIdArray, sampleSize);
                (c, _) = LocalStructureFunctions.StructuralHolesSampledTwoMode(sampledIds, twoMode);
            }
            else
            {
                (c, _) = LocalStructureFunctions.StructuralHoles(network.Nodeset.NodeIdArray, oneModes);
            }
            var attrDict = c.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Float);
        }

        /// <summary>
        /// Computes Burt's effective size for each node in the specified layer(s) and stores the
        /// result as a float node attribute. Effective size = k - Σ_j Σ_{q≠j} p_ij p_qj, measuring
        /// the number of non-redundant contacts. Higher values indicate more structural holes
        /// around the ego. Accepts any number of 1-mode layers, or a single 2-mode layer on its
        /// own (pseudo-projected: traversed node-hyperedge-node directly, without materializing a
        /// 1-mode projection; tie strength is the count-weighted shared-hyperedge count). Because
        /// a 2-mode ego network is every co-member and the per-ego cost grows with the square of
        /// that count, a 2-mode layer requires samplesize > 0: only that many randomly sampled
        /// nodes get an (exact, not approximated) value, and all others are left unset. Burt's
        /// formula has no defined treatment of a tie from a node to itself, so a 1-mode layer
        /// where selfties are allowed is always refused, even if it has none. The per-node O(k²)
        /// computation (shared with Constraint) runs in parallel across up to 'maxthreads'
        /// threads (see 'setting()').
        /// Reference: Burt (1992) Structural Holes; Burt (2004) doi:10.1086/421787.
        /// </summary>
        public static OperationResult EffectiveSize(Network network, string[]? layerNames, string? attrName = null, int sampleSize = 0)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult.Fail("NodesMissing", "Network has no nodes.");

            var resolved = ResolveStructuralHolesLayers(network, layerNames, "EffectiveSize", out var oneModes, out var twoMode);
            if (!resolved.Success)
                return OperationResult.Fail(resolved.Code, resolved.Message);

            string layerTag = layerNames?.Length == 1 ? layerNames[0] + "_" : (layerNames == null ? "" : "multilayer_");
            attrName = !string.IsNullOrEmpty(attrName) ? attrName : layerTag + "effectivesize";

            Dictionary<uint, double> es;
            if (twoMode != null)
            {
                if (sampleSize <= 0)
                    return OperationResult.Fail("SampleSizeRequired",
                        "EffectiveSize on a 2-mode layer requires samplesize > 0: a 2-mode ego network's cost grows with the square of its (potentially large) projected degree, so an exact value for every node isn't offered.");
                uint[] sampledIds = CentralityFunctions.SampleNodes(network.Nodeset.NodeIdArray, sampleSize);
                (_, es) = LocalStructureFunctions.StructuralHolesSampledTwoMode(sampledIds, twoMode);
            }
            else
            {
                (_, es) = LocalStructureFunctions.StructuralHoles(network.Nodeset.NodeIdArray, oneModes);
            }
            var attrDict = es.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Float);
        }

        /// <summary>
        /// Degree assortativity (Newman 2002): Pearson correlation between the degrees
        /// of connected node pairs. For directed (1-mode) layers uses out-degree of source and
        /// in-degree of target; for undirected (1-mode) layers uses degree of both endpoints.
        /// For a 2-mode layer (pseudo-projected: traversed node-hyperedge-node directly, without
        /// materializing a 1-mode projection), degree is always the existing binary projected
        /// degree (number of distinct co-affiliates, matching degree()'s own 2-mode convention),
        /// and 'projection' controls only how a pair of co-affiliates is counted: 'binary'
        /// (default) counts a pair once regardless of how many hyperedges it shares; 'count'
        /// counts it once per shared hyperedge, matching getedge()'s count-weighted projection.
        /// Returns a value in [-1, 1]: positive = hubs connect to hubs,
        /// negative = hubs connect to low-degree nodes.
        /// </summary>
        public static OperationResult<double> DegreeAssortativity(Network network, string layerName, ProjectionMethod projection = ProjectionMethod.Binary)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult<double>.Fail("NodesMissing", "Network has no nodes.");
            var lr = network.GetLayer(layerName);
            if (!lr.Success)
                return OperationResult<double>.Fail(lr.Code, lr.Message);
            if (projection == ProjectionMethod.Newman)
                return OperationResult<double>.Fail("InvalidProjection", "DegreeAssortativity only supports the 'binary' or 'count' projection method.");

            double r = lr.Value is ILayerOneMode oneMode
                ? NetworkLevelFunctions.DegreeAssortativity(network.Nodeset.NodeIdArray, oneMode)
                : NetworkLevelFunctions.DegreeAssortativityTwoMode(network.Nodeset.NodeIdArray, lr.Value!, projection);
            return OperationResult<double>.Ok(r);
        }

        /// <summary>
        /// Attribute assortativity (Newman 2003): tendency of connected nodes to share
        /// the same value of a node attribute. For Float/Int attributes uses Pearson
        /// correlation; for Char/String/Bool uses the nominal mixing formula.
        /// For a 2-mode layer (pseudo-projected: traversed node-hyperedge-node directly, without
        /// materializing a 1-mode projection), 'projection' controls how a pair of co-affiliates
        /// is counted: 'binary' (default) counts a pair once regardless of how many hyperedges it
        /// shares; 'count' counts it once per shared hyperedge, matching getedge()'s
        /// count-weighted projection.
        /// Returns a value in [-1, 1]. Nodes with missing attribute values are skipped.
        /// </summary>
        public static OperationResult<double> Assortativity(Network network, string layerName, string attrName, ProjectionMethod projection = ProjectionMethod.Binary)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult<double>.Fail("NodesMissing", "Network has no nodes.");
            var lr = network.GetLayer(layerName);
            if (!lr.Success)
                return OperationResult<double>.Fail(lr.Code, lr.Message);
            if (projection == ProjectionMethod.Newman)
                return OperationResult<double>.Fail("InvalidProjection", "Assortativity only supports the 'binary' or 'count' projection method.");

            if (!network.Nodeset.NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
                return OperationResult<double>.Fail("AttributeUnknown", $"Attribute '{attrName}' not found.");
            if (!network.Nodeset.NodeAttributeDefinitionManager.TryGetAttributeType(attrIndex, out NodeAttributeType attrType))
                return OperationResult<double>.Fail("AttributeTypeNotFound", $"Type not found for attribute '{attrName}'.");

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            var rawResult = network.Nodeset.GetMultipleNodeAttributes(nodeIds, attrName);
            if (!rawResult.Success)
                return OperationResult<double>.Fail(rawResult.Code, rawResult.Message);
            var raw = rawResult.Value!;

            bool isOneMode = lr.Value is ILayerOneMode;
            double r;
            if (attrType == NodeAttributeType.Float || attrType == NodeAttributeType.Int)
            {
                var vals = new Dictionary<uint, double>(raw.Count);
                foreach (var (id, obj) in raw)
                    if (obj != null)
                        vals[id] = attrType == NodeAttributeType.Int ? (double)(int)obj : (double)(float)obj;
                r = isOneMode
                    ? NetworkLevelFunctions.ContinuousAssortativity((ILayerOneMode)lr.Value!, vals)
                    : NetworkLevelFunctions.ContinuousAssortativityTwoMode(lr.Value!, vals, vals, nodeIds, projection);
            }
            else
            {
                var cats = new Dictionary<uint, string>(raw.Count);
                foreach (var (id, obj) in raw)
                    if (obj != null) cats[id] = obj.ToString()!;
                r = isOneMode
                    ? NetworkLevelFunctions.CategoricalAssortativity((ILayerOneMode)lr.Value!, cats)
                    : NetworkLevelFunctions.CategoricalAssortativityTwoMode(lr.Value!, cats, nodeIds, projection);
            }

            return OperationResult<double>.Ok(r);
        }

        /// <summary>
        /// Computes the MAN dyad census and reciprocity measures for a single layer. Returns
        /// counts of Mutual, Asymmetric and Null dyads plus ArcReciprocity and DyadicReciprocity.
        /// Undirected (1-mode) layers are accepted: all non-null dyads are Mutual. A 2-mode layer
        /// (pseudo-projected: traversed node-hyperedge-node directly, without materializing a
        /// 1-mode projection) is likewise accepted and, since an affiliation has no direction, is
        /// trivially all-mutual (Asymmetric is always 0, ArcReciprocity and DyadicReciprocity are
        /// 1.0 whenever any Mutual dyad exists) - Mutual is the exact count of distinct pairs
        /// sharing at least one hyperedge (binary projected degree, summed over all nodes and
        /// divided by two).
        /// </summary>
        public static OperationResult<Dictionary<string, object>> DyadCensus(Network network, string layerName)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult<Dictionary<string, object>>.Fail("NodesMissing", "Network has no nodes.");
            var lr = network.GetLayer(layerName);
            if (!lr.Success)
                return OperationResult<Dictionary<string, object>>.Fail(lr.Code, lr.Message);
            var result = lr.Value is ILayerOneMode oneMode
                ? NetworkLevelFunctions.DyadCensus(network.Nodeset.NodeIdArray, oneMode)
                : NetworkLevelFunctions.DyadCensusTwoMode(network.Nodeset.NodeIdArray, lr.Value!);
            return OperationResult<Dictionary<string, object>>.Ok(result);
        }

        /// <summary>
        /// Computes the coreness (k-shell index) of each node using k-core decomposition.
        /// Iteratively removes nodes with degree less than k to find the maximal k-core.
        /// Only 1-mode layers are accepted; 2-mode layers must be projected first.
        /// </summary>
        public static OperationResult Coreness(Network network, string[]? layerNames, string? attrName = null)
        {
            var resolveResult = ResolveOneModeOnlyLayers(network, layerNames, "Coreness", out var oneModes);
            if (!resolveResult.Success)
                return OperationResult.Fail(resolveResult.Code, resolveResult.Message);

            string layerTag = layerNames?.Length == 1 ? layerNames[0] + "_" : (layerNames == null ? "" : "multilayer_");
            attrName = !string.IsNullOrEmpty(attrName) ? attrName : layerTag + "coreness";

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            var values = LocalStructureFunctions.Coreness(nodeIds, oneModes);
            var attrDict = values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Int);
        }

        /// <summary>
        /// Calculates betweenness centrality for all nodes using Brandes' algorithm with
        /// hyperedge-aware BFS. Normalizes by (n-1)(n-2), the count of ordered pairs excluding
        /// v; this holds for both directed and undirected layers, since the undirected
        /// accumulation already sums each unordered pair twice (once per direction), which
        /// cancels against halving the ordered-pair count. When sampleSize > 0, scales the
        /// result by n/sampleSize. The per-source BFS passes (shared with ClosenessCentrality
        /// and HarmonicCentrality) run in parallel across up to 'maxthreads' threads (see
        /// 'setting()').
        /// </summary>
        public static OperationResult BetweennessCentrality(Network network, string[]? layerNames, string? attrName = null, int sampleSize = 0, EdgeTraversal traversal = EdgeTraversal.Out)
        {
            if (!TryResolveLayers(network, layerNames, out var one, out var dynTwo, out var statTwo, out var err))
                return err!;

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            uint[] sources = CentralityFunctions.SampleNodes(nodeIds, sampleSize);

            var (bw, _, _, _) = CentralityFunctions.AccumulateBFSCentralitiesForSources(sources, one, dynTwo, statTwo, traversal);

            var final = CentralityFunctions.FinalizeBetweenness(bw, nodeIds, sources.Length);
            attrName = string.IsNullOrEmpty(attrName) ? "betweenness" : attrName;
            var attrDict = final.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(CultureInfo.InvariantCulture));
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Float);
        }

        /// <summary>
        /// Calculates edge betweenness centrality for every edge in a single 1-mode layer, storing
        /// the result as a new valued layer (same directionality and selfties setting as the source
        /// layer). Uses the same Brandes-based approach as BetweennessCentrality — each predecessor
        /// edge on a shortest-path DAG is credited with the same contribution normally folded into
        /// node betweenness — so it shares that command's cost profile and 'sampleSize'-based
        /// approximation for large networks (Brandes &amp; Pich 2007). Kept as a separate command from
        /// BetweennessCentrality rather than an option on it, since edge identity — unlike node
        /// identity — has no clean mapping across combined layers, so this only operates on a single
        /// named layer rather than a combined set. Normalizes the same way as BetweennessCentrality:
        /// divide by (n-1)(n-2) regardless of directionality (see that method's remarks), when
        /// normalize is true (the default), scaled by n/sampleSize when sampled. The per-source BFS
        /// passes run in parallel across up to 'maxthreads' threads (see 'setting()').
        /// </summary>
        public static OperationResult EdgeBetweennessCentrality(Network network, string layerName, string newLayerName, int sampleSize = 0, bool normalize = true)
        {
            var lr = network.GetOneModeLayerForRead(layerName);
            if (!lr.Success)
                return OperationResult.Fail(lr.Code, lr.Message);
            ILayerOneMode layer = lr.Value!;
            if (network.Layers.ContainsKey(newLayerName))
                return OperationResult.Fail("LayerAlreadyExists", $"Layer '{newLayerName}' already exists in network '{network.Name}'.");

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            uint[] sources = CentralityFunctions.SampleNodes(nodeIds, sampleSize);

            var raw = CentralityFunctions.AccumulateEdgeBetweennessForSources(sources, layer, EdgeTraversal.Out);
            var final = normalize
                ? CentralityFunctions.FinalizeEdgeBetweenness(raw, nodeIds.Length, sources.Length)
                : raw;

            LayerOneMode newLayer = new LayerOneMode(newLayerName, layer.Directionality, EdgeType.Valued, layer.Selfties);
            foreach (var ((from, to), value) in final)
                newLayer._addEdge(from, to, (float)value);
            newLayer._sortEdgesets();
            newLayer._deduplicateEdgesets();
            network.AddLayer(newLayerName, newLayer);

            return OperationResult.Ok($"Edge betweenness centrality for layer '{layerName}' computed and stored as new layer '{newLayerName}'.");
        }

        /// <summary>
        /// Calculates closeness centrality using Wasserman-Faust normalization, which handles
        /// disconnected components by incorporating the reachable proportion of nodes.
        /// When sampleSize > 0, uses a random subset of source nodes. The per-source BFS passes
        /// run in parallel across up to 'maxthreads' threads (see 'setting()'); unlike
        /// BetweennessCentrality, this only needs shortest-path distances, not path counts, so it
        /// uses the cheaper plain-BFS traversal (linear in hyperedge sizes on a 2-mode layer,
        /// rather than Brandes' quadratic worst case).
        /// </summary>
        public static OperationResult ClosenessCentrality(Network network, string[]? layerNames, string? attrName = null, int sampleSize = 0, EdgeTraversal traversal = EdgeTraversal.Out)
        {
            if (!TryResolveLayers(network, layerNames, out var one, out var dynTwo, out var statTwo, out var err))
                return err!;

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            uint[] sources = CentralityFunctions.SampleNodes(nodeIds, sampleSize);

            var (cdSum, cdReach, _) = CentralityFunctions.AccumulateDistanceCentralitiesForSources(sources, one, dynTwo, statTwo, traversal);

            var final = CentralityFunctions.FinalizeCloseness(cdSum, cdReach, nodeIds);
            attrName = string.IsNullOrEmpty(attrName) ? "closeness" : attrName;
            var attrDict = final.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(CultureInfo.InvariantCulture));
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Float);
        }

        /// <summary>
        /// Calculates harmonic centrality (sum of inverse distances to all reachable nodes).
        /// When normalize = true, divides by (n-1). Handles disconnected graphs naturally.
        /// When sampleSize > 0, uses a random subset of source nodes. The per-source BFS passes
        /// run in parallel across up to 'maxthreads' threads (see 'setting()'); unlike
        /// BetweennessCentrality, this only needs shortest-path distances, not path counts, so it
        /// uses the cheaper plain-BFS traversal (linear in hyperedge sizes on a 2-mode layer,
        /// rather than Brandes' quadratic worst case).
        /// </summary>
        public static OperationResult HarmonicCentrality(Network network, string[]? layerNames, string? attrName = null, int sampleSize = 0, bool normalize = true, EdgeTraversal traversal = EdgeTraversal.Out)
        {
            if (!TryResolveLayers(network, layerNames, out var one, out var dynTwo, out var statTwo, out var err))
                return err!;

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            uint[] sources = CentralityFunctions.SampleNodes(nodeIds, sampleSize);

            var (_, _, hm) = CentralityFunctions.AccumulateDistanceCentralitiesForSources(sources, one, dynTwo, statTwo, traversal);

            var final = CentralityFunctions.FinalizeHarmonic(hm, nodeIds, normalize);
            attrName = string.IsNullOrEmpty(attrName) ? "harmonic" : attrName;
            var attrDict = final.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(CultureInfo.InvariantCulture));
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Float);
        }

        /// <summary>
        /// Calculates eigenvector centrality via power iteration. Converges to the dominant
        /// eigenvector of the (projected) adjacency matrix. For 2-mode layers, uses efficient
        /// per-hyperedge summation. Typically applied with EdgeTraversal.Both for undirected networks.
        /// </summary>
        public static OperationResult EigenvectorCentrality(Network network, string[]? layerNames,
            string? attrName = null, EdgeTraversal traversal = EdgeTraversal.Out,
            int maxIterations = 100, double tolerance = 1e-8)
        {
            if (!TryResolveLayers(network, layerNames, out var one, out var dynTwo, out var statTwo, out var err))
                return err!;

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            var scores = CentralityFunctions.EigenvectorCentrality(nodeIds, one, dynTwo, statTwo, traversal, maxIterations, tolerance);

            attrName = string.IsNullOrEmpty(attrName) ? "eigenvector" : attrName;
            var attrDict = scores.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(CultureInfo.InvariantCulture));
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Float);
        }

        /// <summary>
        /// Calculates PageRank scores. Precomputes projected out-neighbor lists (deduplicated),
        /// handles dangling nodes, and iterates until convergence. Default damping factor 0.85.
        /// </summary>
        public static OperationResult PageRank(Network network, string[]? layerNames, string? attrName = null, double dampingFactor = 0.85, int maxIterations = 100, double tolerance = 1e-8)
        {
            if (!TryResolveLayers(network, layerNames, out var one, out var dynTwo, out var statTwo, out var err))
                return err!;

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            var scores = CentralityFunctions.PageRank(nodeIds, one, dynTwo, statTwo, dampingFactor, maxIterations, tolerance);

            attrName = string.IsNullOrEmpty(attrName) ? "pagerank" : attrName;
            var attrDict = scores.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(CultureInfo.InvariantCulture));
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Float);
        }

        /// <summary>
        /// Calculates local clustering coefficient for each node in the specified 1-mode layer(s)
        /// and stores the result as a float node attribute. Only 1-mode layers are accepted;
        /// 2-mode layers must be projected first via projecttwomode().
        /// Auto-selects formula based on layer type (directed/undirected, binary/valued) unless
        /// method is specified explicitly. The Onnela method is refused for any layer where
        /// selfties are allowed, even if it has none, since its normalization by the global
        /// maximum edge weight does not currently scan self-tie values.
        /// When multiple layers are specified, adjacency is the union across all layers.
        /// Each node's coefficient is independent of every other's, so the per-node computations
        /// run in parallel across up to 'maxthreads' threads (see 'setting()').
        /// </summary>
        public static OperationResult ClusteringCoefficient(Network network, string[]? layerNames, string? attrName = null, ClusteringMethod method = ClusteringMethod.Auto, int sampleSize = 0)
        {
            var resolveResult = ResolveOneModeOnlyLayers(network, layerNames, "Clustering coefficient", out var oneModes,
                (lom, name) => method == ClusteringMethod.Onnela && lom.Selfties
                    ? OperationResult.Fail("LayerAllowsSelfties",
                        $"Layer '{name}' allows selfties. Onnela's normalization (dividing by the global maximum edge weight) does not currently scan self-tie values when finding that maximum, so it is refused for any layer where selfties are allowed — even if none are actually present. Use a different method (e.g. 'barrat') for layers that permit selfties.")
                    : OperationResult.Ok());
            if (!resolveResult.Success)
                return OperationResult.Fail(resolveResult.Code, resolveResult.Message);

            uint[] allNodeIds = network.Nodeset.NodeIdArray;
            uint[] sourceIds = (sampleSize > 0 && sampleSize < allNodeIds.Length)
                ? CentralityFunctions.SampleNodes(allNodeIds, sampleSize)
                : allNodeIds;

            // Resolve actual method (in case Auto was passed) before naming the attribute
            ClusteringMethod resolvedMethod = method == ClusteringMethod.Auto
                ? LocalStructureFunctions.DetectMethod(oneModes)
                : method;

            string methodSuffix = resolvedMethod switch
            {
                ClusteringMethod.WattsStrogatz => "_ws",
                ClusteringMethod.Fagiolo => "_fagiolo",
                ClusteringMethod.Barrat => "_barrat",
                ClusteringMethod.Onnela => "_onnela",
                _ => ""
            };

            string layerTag = layerNames?.Length == 1 ? layerNames[0] + "_" : (layerNames == null ? "" : "multilayer_");
            attrName = !string.IsNullOrEmpty(attrName) ? attrName : layerTag + "clustering" + methodSuffix;

            var values = LocalStructureFunctions.ClusteringCoefficient(sourceIds, oneModes, resolvedMethod);
            var attrDict = values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(CultureInfo.InvariantCulture));
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Float);
        }

        /// <summary>
        /// Calculates the global clustering coefficient (transitivity) of the specified 1-mode layer(s):
        /// ratio of closed triangles to all connected triples, treating edges as undirected.
        /// Equivalent to 3T / Σ C(k,2) where T is the number of distinct triangles.
        /// </summary>
        public static OperationResult<double> Transitivity(Network network, string[]? layerNames)
        {
            var resolveResult = ResolveOneModeOnlyLayers(network, layerNames, "Transitivity", out var oneModes);
            if (!resolveResult.Success)
                return OperationResult<double>.Fail(resolveResult.Code, resolveResult.Message);

            double t = LocalStructureFunctions.Transitivity(network.Nodeset.NodeIdArray, oneModes);
            return OperationResult<double>.Ok(t);
        }

        /// <summary>
        /// Generates summary info about an attribute in a nodeset. The specific info that is returned depends on the type
        /// of node attribute.
        /// </summary>
        /// <param name="nodeset">The Nodeset structure.</param>
        /// <param name="attrName">The attribute name.</param>
        /// <returns>An <see cref="OperationResult"/> with a potentially nested string-object dictionary with summary statistics about the attribute.</returns>
        public static OperationResult<Dictionary<string, object>> GetAttributeSummary(Nodeset nodeset, string attrName)
        {
            if (!nodeset.NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
                return OperationResult<Dictionary<string, object>>.Fail("AttributeUnknown", $"Attribute '{attrName}' not found in nodeset '{nodeset.Name}'.");
            if (!nodeset.NodeAttributeDefinitionManager.TryGetAttributeType(attrIndex, out NodeAttributeType attrType))
                return OperationResult<Dictionary<string, object>>.Fail("AttributeTypeNotFound", $"Type not found for attribute '{attrName}' in nodeset '{nodeset.Name}'.");
            int totalNodes = nodeset.Count;
            int countWithValues = 0;

            Dictionary<string, object> results = new()
            {
                ["AttributeName"] = attrName,
                ["AttributeType"] = attrType.ToString()
            };

            Dictionary<string, object> stats = [];

            switch (attrType)
            {
                case NodeAttributeType.Int:
                    stats = Functions.CalculateIntStatistics(nodeset, attrIndex, out countWithValues);
                    break;
                case NodeAttributeType.Float:
                    stats = Functions.CalculateFloatStatistics(nodeset, attrIndex, out countWithValues);
                    break;
                case NodeAttributeType.Bool:
                    stats = Functions.CalculateBoolStatistics(nodeset, attrIndex, out countWithValues);
                    break;
                case NodeAttributeType.Char:
                    stats = Functions.CalculateCharStatistics(nodeset, attrIndex, out countWithValues);
                    break;
                case NodeAttributeType.String:
                    stats = Functions.CalculateStringStatistics(nodeset, attrIndex, out countWithValues);
                    break;
            }

            stats["Count"] = countWithValues;
            stats["Missing"] = totalNodes - countWithValues;
            stats["PercentageWithValue"] = totalNodes > 0 ? (double)countWithValues / totalNodes * 100.0 : 0.0;
            results["Statistics"] = stats;
            return OperationResult<Dictionary<string, object>>.Ok(results);
        }

        /// <summary>
        /// For a valued 1-mode layer: computes summary statistics (Mean, Median, StdDev, Min, Max,
        /// Q1, Q3) over the edge values, using the same statistics machinery as GetAttributeSummary.
        /// Requires a valued layer — a binary layer has no varying values to summarize. For symmetric
        /// (undirected) layers, each edge is counted once, not once per endpoint. Returns Mean,
        /// Median, StdDev, Min, Max, Q1, Q3, LayerName and EdgeCount.
        /// For a 2-mode layer (pseudo-projected: no projection is performed here) instead computes
        /// the same Mean/Median/StdDev/Min/Max/Q1/Q3 statistics over hyperedge *sizes*, plus
        /// LayerName, NbrHyperedges, NbrAffiliations, and two numbers meant to help decide whether
        /// projecttwomode() is memory-feasible before calling it: EstimatedProjectedEdgesUpperBound
        /// (Σ k_h·(k_h−1)/2 over all hyperedges — a safe upper bound on the number of distinct
        /// projected edges, computed in one O(hyperedge count) pass with no pair enumeration, and
        /// dominated by the single largest hyperedge since its own contribution grows with the
        /// square of its size) and EstimatedProjectionMemoryBytes (that bound times a deliberately
        /// conservative, rounded per-edge byte constant derived from Threadle's actual storage:
        /// projecttwomode() builds the projected layer directly, one node's co-affiliates at a
        /// time (no global dictionary of every pair held in memory at once), so its peak is just
        /// the finished layer's own size — an 8-byte Connection struct stored on both endpoints
        /// for a valued/count projection, plus List growth slack from that incremental build).
        /// Both of these are order-of-magnitude estimates, not precise predictions — treat them
        /// as a smell test, not a guarantee.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layerName">The name of the layer (1-mode valued, or 2-mode).</param>
        /// <returns>An <see cref="OperationResult"/> with the fields described above.</returns>
        public static OperationResult<Dictionary<string, object>> GetLayerStats(Network network, string layerName)
        {
            var lr = network.GetLayer(layerName);
            if (!lr.Success)
                return OperationResult<Dictionary<string, object>>.Fail(lr.Code, lr.Message);

            if (lr.Value is ILayerTwoMode twoModeLayer)
            {
                List<float> sizes = [];
                double sumKChooseTwo = 0.0;
                foreach (var (_, memberIds) in twoModeLayer.GetAllHyperedgeData())
                {
                    int k = memberIds.Length;
                    sizes.Add(k);
                    sumKChooseTwo += (double)k * (k - 1) / 2.0;
                }
                var twoModeStats = Functions.CalculateValueStatistics(sizes);
                twoModeStats["LayerName"] = layerName;
                twoModeStats["NbrHyperedges"] = sizes.Count;
                twoModeStats["NbrAffiliations"] = twoModeLayer.NbrAffiliations;
                twoModeStats["EstimatedProjectedEdgesUpperBound"] = sumKChooseTwo;
                twoModeStats["EstimatedProjectionMemoryBytes"] = sumKChooseTwo * 25.0;
                return OperationResult<Dictionary<string, object>>.Ok(twoModeStats);
            }

            ILayerOneMode layer = (ILayerOneMode)lr.Value!;
            if (!layer.IsValued)
                return OperationResult<Dictionary<string, object>>.Fail("LayerNotValued", $"Layer '{layerName}' is binary — getlayerstats() requires a valued layer.");

            List<float> values = Functions.GetLayerEdgeValues(layer, network.Nodeset.NodeIdArray);
            var stats = Functions.CalculateValueStatistics(values);
            stats["LayerName"] = layerName;
            stats["EdgeCount"] = values.Count;
            return OperationResult<Dictionary<string, object>>.Ok(stats);
        }

        public static OperationResult<ShortestPathResult> ShortestPath(Network network, string[]? layerNames, uint nodeIdFrom, uint nodeIdTo, bool returnPath = false)
        {
            OperationResult nodeCheckResult = network.Nodeset.CheckThatNodesExist(nodeIdFrom, nodeIdTo);
            if (!nodeCheckResult.Success)
                return OperationResult<ShortestPathResult>.Fail(nodeCheckResult.Code, nodeCheckResult.Message);
            if (nodeIdFrom == nodeIdTo)
                return OperationResult<ShortestPathResult>.Ok(new ShortestPathResult(0, returnPath ? [nodeIdFrom] : null));

            if (!TryResolveLayers(network, layerNames, out var one, out var dynTwo, out var statTwo, out var err))
                return OperationResult<ShortestPathResult>.Fail(err!.Code, err.Message);

            return OperationResult<ShortestPathResult>.Ok(
                PathFunctions.BidirectionalBFS(nodeIdFrom, nodeIdTo, one, dynTwo, statTwo, returnPath));
        }

        /// <summary>
        /// Calculates exact shortest paths for a batch of node pairs, in parallel (up to the 'maxthreads'
        /// setting, see 'setting()'). Layers are resolved once and shared (read-only) across all pairs; each
        /// pair's result is written to its own independent output slot, so no merging of shared state is
        /// needed. Results are returned in the same order as 'pairs'.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <param name="layerNames">The names of the layers to use (or null to use all layers).</param>
        /// <param name="pairs">The node id pairs (from, to) to compute shortest paths for.</param>
        /// <param name="returnPath">If true, each result also contains the sequence of node ids along the shortest path.</param>
        /// <returns>An <see cref="OperationResult"/> containing a list of (From, To, Result) tuples, one per pair, in the same order as 'pairs'.</returns>
        public static OperationResult<List<(uint From, uint To, ShortestPathResult Result)>> ShortestPaths(Network network, string[]? layerNames, IReadOnlyList<(uint From, uint To)> pairs, bool returnPath = false)
        {
            if (pairs.Count == 0)
                return OperationResult<List<(uint, uint, ShortestPathResult)>>.Ok([]);

            foreach ((uint from, uint to) in pairs)
            {
                OperationResult nodeCheckResult = network.Nodeset.CheckThatNodesExist(from, to);
                if (!nodeCheckResult.Success)
                    return OperationResult<List<(uint, uint, ShortestPathResult)>>.Fail(nodeCheckResult.Code, nodeCheckResult.Message);
            }

            if (!TryResolveLayers(network, layerNames, out var one, out var dynTwo, out var statTwo, out var err))
                return OperationResult<List<(uint, uint, ShortestPathResult)>>.Fail(err!.Code, err.Message);

            var results = new (uint From, uint To, ShortestPathResult Result)[pairs.Count];

            System.Threading.Tasks.Parallel.For(0, pairs.Count, UserSettings.GetParallelOptions(), i =>
            {
                (uint from, uint to) = pairs[i];
                ShortestPathResult result = from == to
                    ? new ShortestPathResult(0, returnPath ? [from] : null)
                    : PathFunctions.BidirectionalBFS(from, to, one, dynTwo, statTwo, returnPath);
                results[i] = (from, to, result);
            });

            return OperationResult<List<(uint, uint, ShortestPathResult)>>.Ok([.. results]);
        }

        /// <summary>
        /// Calculates the density of the specified layer in the network. Can be 1-mode or 2-mode.
        /// 1-mode is always exact (cheap: NbrEdges / potential edges). 2-mode defaults to a
        /// 200-node sample, unlike most other samplesize-bearing commands' "0 means exact by
        /// default" convention - its exact computation is genuinely O(sum of hyperedge sizes
        /// squared), the same order as materializing a full projection, not the O(sum of
        /// hyperedge sizes) a quick stat might suggest, so it is deliberately not the default
        /// here. Pass sampleSize=0 explicitly for the exact value anyway - see
        /// <see cref="Functions.Density(Network, ILayerTwoMode, int)"/>.
        /// </summary>
        /// <param name="network">The network containing the layer.</param>
        /// <param name="layerName">The name of the layer.</param>
        /// <returns>An OperationResult containing the density value if successful; otherwise, an error message.</returns>
        public static OperationResult<double> Density(Network network, string layerName, int sampleSize = 200)
        {
            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return OperationResult<double>.Fail(layerResult);
            if (layerResult.Value is ILayerOneMode layerOneMode)
                return OperationResult<double>.Ok(Functions.Density(network, layerOneMode));
            if (layerResult.Value is ILayerTwoMode layerTwoMode)
                return OperationResult<double>.Ok(Functions.Density(network, layerTwoMode, sampleSize));
            return OperationResult<double>.Fail("UnexpectedError", $"Error calculating density of layer '{layerName}'");
        }

        /// <summary>
        /// Calculates connected components for a layer in the specified network, storing the component index as a new
        /// node attribute.
        /// </summary>
        /// <param name="network">The network containing the layer.</param>
        /// <param name="layerName">The name of the layer.</param>
        /// <param name="attrName">The name of the node attribute where to store the component index.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went, with a string-object dictionary with additional information.</returns>
        public static OperationResult<Dictionary<string, object>> ConnectedComponents(Network network, string layerName, string? attrName = null)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult<Dictionary<string, object>>.Fail("NodesMissing", "Network has no nodes: can't do component analysis on it.");
            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return OperationResult<Dictionary<string, object>>.Fail(layerResult);
            ILayer layer = layerResult.Value!;
            Dictionary<uint, int> componentIndexMapping = Functions.ConnectedComponents(network, layer);
            var attrDict = componentIndexMapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
            attrName = (attrName != null && attrName.Length > 0) ? attrName : layerName + "_componentIndex";
            var setAttrResult = network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Int);
            if (!setAttrResult.Success)
                return OperationResult<Dictionary<string, object>>.Fail(setAttrResult);

            Dictionary<string, object> componentInfo = [];
            int nbrComponents = componentIndexMapping.Values.Max() + 1;
            int[] componentSizes = new int[nbrComponents];
            foreach (int compId in componentIndexMapping.Values)
                componentSizes[compId]++;
            componentInfo["NbrComponents"] = nbrComponents;
            componentInfo["ComponentSizes"] = componentSizes.OrderByDescending(c => c).ToList();
            return OperationResult<Dictionary<string, object>>.Ok(componentInfo);
        }



        /// <summary>
        /// Calculates the degree centrality for each node in the specified one- or two-mode layer and stores the results
        /// as a node attribute in the network's nodeset.
        /// </summary>
        /// <param name="network">The network containing the layer.</param>
        /// <param name="layerName">The name of the layer.</param>
        /// <param name="attrName">The name of the node attribute where to store the degree centrality.</param>
        /// <param name="edgeTraversal">Whether to use inbound- and/or outbound edges.</param>
        /// <returns>An <see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult DegreeCentralities(Network network, string layerName, string? attrName = null, EdgeTraversal edgeTraversal = EdgeTraversal.Out)
        {
            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return OperationResult.Fail(layerResult.Code, layerResult.Message);
            ILayer layer = layerResult.Value!;
            Dictionary<uint, uint> degreeMapping = [];
            if (layer is ILayerOneMode layerOneMode)
            {
                string dirString = layerOneMode.IsSymmetric ? "degree" : edgeTraversal switch
                {
                    EdgeTraversal.Out => "outdegree",
                    EdgeTraversal.In => "indegree",
                    _ => "grossdegree"
                };
                attrName = (attrName != null && attrName.Length > 0) ? attrName : layerName + "_" + dirString;
                degreeMapping = Functions.DegreeCentrality(network, layerOneMode, edgeTraversal);
            }
            else if (layer is ILayerTwoMode layerTwoMode)
            {
                attrName = (attrName != null && attrName.Length > 0) ? attrName : layerName + "_degree";
                degreeMapping = Functions.DegreeCentrality(network, layerTwoMode);
            }
            else
                return OperationResult.Fail("InvalidLayerType", $"Layer '{layerName}' is neither a one-mode nor a two-mode layer.");
            // Convert the degree mapping into the <uint,string> format and store this as a node attribute
            var attrDict = degreeMapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Int);
        }

        /// <summary>
        /// Given an ego node, returns a random alter. Resolves layer names then delegates to the
        /// pre-resolved overload. For hot-path walk loops, resolve layers once and use
        /// GetRandomAlter(uint, IReadOnlyList<ILayer>, ...) directly.
        /// 'layerWeights', if given, applies only when 'balanced' is true and there is more than one
        /// layer: it controls the probability of picking each layer first (proportionally — they need
        /// not sum to 1), parallel in order to 'layerNames', which must then be given explicitly (there
        /// is no way to match weights to "all layers" reliably). It is otherwise ignored.
        /// </summary>
        public static OperationResult<uint> GetRandomAlter(Network network, uint nodeId, string[]? layerNames, EdgeTraversal edgeTraversal = EdgeTraversal.Both, bool balanced = false, bool weighted = false, float[]? layerWeights = null)
        {
            if (layerWeights != null && layerNames == null)
                return OperationResult<uint>.Fail("InvalidArguments", "'layerweights' requires 'layernames' to be given explicitly, so each weight can be matched to a specific layer.");
            List<ILayer> layersToUse = [];
            if (layerNames == null)
                layersToUse.AddRange(network.Layers.Values);
            else
            {
                foreach (string layerName in layerNames)
                {
                    var layerResult = network.GetLayer(layerName);
                    if (!layerResult.Success)
                        return OperationResult<uint>.Fail(layerResult);
                    layersToUse.Add(layerResult.Value!);
                }
            }
            return GetRandomAlter(nodeId, layersToUse, edgeTraversal, balanced, weighted, layerWeights);
        }

        /// <summary>
        /// Hot-path overload: accepts pre-resolved layers to avoid per-step layer lookup and List allocation.
        /// For non-balanced 2-mode layers the fast path (PickRandomAlterO1 / AppendProjectedAltersReservoir)
        /// is used via ILayerTwoMode, covering both LayerTwoMode and LayerTwoModeStatic.
        /// 'layerWeights', if given, must be parallel in order to 'layersToUse' (same length) and all
        /// positive; it applies only when 'balanced' is true and there is more than one layer.
        /// </summary>
        public static OperationResult<uint> GetRandomAlter(uint nodeId, IReadOnlyList<ILayer> layersToUse, EdgeTraversal edgeTraversal = EdgeTraversal.Both, bool balanced = false, bool weighted = false, float[]? layerWeights = null)
        {
            if (layerWeights != null)
            {
                if (layerWeights.Length != layersToUse.Count)
                    return OperationResult<uint>.Fail("LengthMismatch", $"The number of layer weights ({layerWeights.Length}) differs from the number of layers ({layersToUse.Count}).");
                if (layerWeights.Any(w => w <= 0))
                    return OperationResult<uint>.Fail("InvalidArguments", "All layer weights must be positive.");
            }

            if (weighted)
            {
                List<(uint alterId, float weight)> candidates = [];
                if (layersToUse.Count == 1 || !balanced)
                {
                    foreach (var layer in layersToUse)
                        Functions.AppendWeightedCandidates(candidates, layer, nodeId, edgeTraversal);
                }
                else
                {
                    bool[] eligible = new bool[layersToUse.Count];
                    bool anyEligible = false;
                    for (int i = 0; i < layersToUse.Count; i++)
                        if (layersToUse[i].GetNodeAlters(nodeId, edgeTraversal).Length > 0)
                            anyEligible = eligible[i] = true;
                    if (!anyEligible)
                        return OperationResult<uint>.Fail("ConstraintNoAlters", $"Node {nodeId} has no alters in any of the specified layers with the given edge traversal.");
                    int chosenIndex = PickWeightedLayerIndex(layersToUse.Count, eligible, layerWeights);
                    Functions.AppendWeightedCandidates(candidates, layersToUse[chosenIndex], nodeId, edgeTraversal);
                }
                if (candidates.Count == 0)
                    return OperationResult<uint>.Fail("ConstraintNoAlters", $"Node {nodeId} has no alters in the specified layer(s) with the given edge traversal.");
                return OperationResult<uint>.Ok(Functions.WeightedPick(candidates));
            }

            // Non-weighted follows
            if (layersToUse.Count == 1 || !balanced)
            {
                if (layersToUse.Count == 1 && layersToUse[0] is ILayerTwoMode itm_single)
                {
                    // Single 2-mode layer (dynamic or static): O(1) fast path, zero allocation
                    uint? fastPick = itm_single.PickRandomAlterO1(nodeId);
                    return fastPick.HasValue
                        ? OperationResult<uint>.Ok(fastPick.Value)
                        : OperationResult<uint>.Fail("ConstraintNoAlters", $"Node {nodeId} has no alters in the specified layer(s) with the given edge traversal.");
                }
                if (layersToUse.Count == 1)
                {
                    // Single 1-mode layer
                    uint[] alts = layersToUse[0].GetNodeAlters(nodeId, edgeTraversal);
                    return alts.Length > 0
                        ? OperationResult<uint>.Ok(alts[Misc.Random.Next(alts.Length)])
                        : OperationResult<uint>.Fail("ConstraintNoAlters", $"Node {nodeId} has no alters in the specified layer(s) with the given edge traversal.");
                }
                // Multi-layer pooled: reservoir sampling, zero allocation for 2-mode layers
                uint? selected = null;
                int totalCount = 0;
                foreach (var layer in layersToUse)
                {
                    if (layer is ILayerTwoMode itm2)
                        itm2.AppendProjectedAltersReservoir(nodeId, ref selected, ref totalCount);
                    else
                        foreach (uint m in layer.GetNodeAlters(nodeId, edgeTraversal))
                        { totalCount++; if (Misc.Random.Next(totalCount) == 0) selected = m; }
                }
                if (totalCount == 0)
                    return OperationResult<uint>.Fail("ConstraintNoAlters", $"Node {nodeId} has no alters in the specified layer(s) with the given edge traversal.");
                return OperationResult<uint>.Ok(selected!.Value);
            }
            else
            {
                // Balanced and multiple layers: pick a layer (weighted by 'layerWeights' if given,
                // uniform otherwise), then uniform pick of alter within it.
                bool[] eligible = new bool[layersToUse.Count];
                uint[][] altersPerLayer = new uint[layersToUse.Count][];
                bool anyEligible = false;
                for (int i = 0; i < layersToUse.Count; i++)
                {
                    altersPerLayer[i] = layersToUse[i].GetNodeAlters(nodeId, edgeTraversal);
                    if (altersPerLayer[i].Length > 0)
                        anyEligible = eligible[i] = true;
                }
                if (!anyEligible)
                    return OperationResult<uint>.Fail("ConstraintNoAlters", $"Node {nodeId} has no alters in any of the specified layers with the given edge traversal.");
                uint[] chosenLayerAlters = altersPerLayer[PickWeightedLayerIndex(layersToUse.Count, eligible, layerWeights)];
                return OperationResult<uint>.Ok(chosenLayerAlters[Misc.Random.Next(chosenLayerAlters.Length)]);
            }
        }

        /// <summary>
        /// Picks one eligible layer (by index into a 'count'-length list), weighted by 'layerWeights' if
        /// given (parallel by index; ineligible entries are simply skipped), or uniformly otherwise.
        /// Assumes at least one entry in 'eligible' is true.
        /// </summary>
        private static int PickWeightedLayerIndex(int count, bool[] eligible, float[]? layerWeights)
        {
            float total = 0f;
            for (int i = 0; i < count; i++)
                if (eligible[i])
                    total += layerWeights?[i] ?? 1f;
            double r = Misc.Random.NextDouble() * total;
            double cum = 0;
            int lastEligible = -1;
            for (int i = 0; i < count; i++)
            {
                if (!eligible[i])
                    continue;
                lastEligible = i;
                cum += layerWeights?[i] ?? 1f;
                if (r < cum)
                    return i;
            }
            return lastEligible; // floating-point fallback: last eligible index
        }

        /// <summary>
        /// Selects a random node identifier from the specified nodeset.
        /// </summary>
        /// <param name="nodeset">The nodeset from which to select a random node. Must contain at least one node.</param>
        /// <returns>An <see cref="OperationResult{T}"/> containing the identifier of the randomly selected node if successful, otherwise, an error message.</returns>
        public static OperationResult<uint> GetRandomNode(Nodeset nodeset)
        {
            if (nodeset.Count == 0)
                return OperationResult<uint>.Fail("ConstraintNoNodes", $"Nodeset '{nodeset.Name}' contains no nodes.");
            uint randomNodeId = nodeset.NodeIdArray[Misc.Random.Next(nodeset.Count)];
            return OperationResult<uint>.Ok(randomNodeId);
        }

        /// <summary>
        /// Selects a random edge from the specified network and layer. Layers can be either 1-mode or 2-mode: for 2-mode
        /// layers, the random pick is a bit intricate, using first a polling method with a certain number of attemps,
        /// before switching over to the second, slightly biased version.
        /// </summary>
        /// <param name="network">The network from which to pick the random edge.</param>
        /// <param name="layerName">The name of the layer</param>
        /// <returns>A string-object dictionary containing ids for the two nodes and the value of the tie.</returns>
        public static OperationResult<Dictionary<string, object>> GetRandomEdge(Network network, string layerName, int maxAttempts = 100)
        {
            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return OperationResult<Dictionary<string, object>>.Fail(layerResult);
            var layer = layerResult.Value!;

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            bool selfiesAllowed = layer is ILayerOneMode om && om.Selfties;
            if (nodeIds.Length == 0 || (nodeIds.Length == 1 && !selfiesAllowed))
                return OperationResult<Dictionary<string, object>>.Fail("EdgeNotFound", $"Network has fewer than 2 nodes and thus no edges.");

            Dictionary<string, object>? randomEdge = Functions.GetRandomEdge(layer, nodeIds, maxAttempts);

            if (randomEdge != null)
                return OperationResult<Dictionary<string, object>>.Ok(randomEdge, "Random edge found through polling.");

            if (layer is ILayerOneMode layerOneMode)
                randomEdge = Functions.GetRandomEdgeSweepOneMode(layerOneMode);
            else if (layer is ILayerTwoMode layerTwoMode)
                randomEdge = Functions.GetRandomEdgeWeightedTwoMode(layerTwoMode);

            if (randomEdge == null)
                return OperationResult<Dictionary<string, object>>.Fail("EdgeNotFound", $"Could not find any edge in layer {layerName}.");
            return OperationResult<Dictionary<string, object>>.Ok(randomEdge, "Random edge found through non-polling.");
        }
        #endregion

        #region Methods (private)
        private static bool TryResolveLayers(Network network, string[]? layerNames, out List<ILayerOneMode> oneModes, out List<LayerTwoMode> twoModesDynamic, out List<LayerTwoModeStatic> twoModesStatic, out OperationResult? error)
        {
            List<ILayer> resolved = [];
            if (layerNames == null)
                resolved.AddRange(network.Layers.Values);
            else
            {
                foreach (string name in layerNames)
                {
                    var lr = network.GetLayer(name);
                    if (!lr.Success)
                    {
                        oneModes = []; twoModesDynamic = []; twoModesStatic = [];
                        error = OperationResult.Fail(lr.Code, lr.Message);
                        return false;
                    }
                    resolved.Add(lr.Value!);
                }
            }
            GraphAlgorithms.SplitLayers(resolved, out oneModes, out twoModesDynamic, out twoModesStatic);
            error = null;
            return true;
        }

        /// <summary>
        /// Resolves layerNames (or all layers, if null) to a List&lt;ILayerOneMode&gt;, failing if any
        /// resolved layer is 2-mode. Shared by every 1-mode-only measure (Coreness,
        /// ClusteringCoefficient, Transitivity, and — via TryResolveOneModeLayersForCommunityDetection
        /// below — community detection's Girvan-Newman path). 'metricName' is woven into the
        /// InvalidLayerType message so each caller still gets a precise error naming itself.
        /// 'extraPerLayerCheck', if given, runs once per resolved 1-mode layer (name, layer) before
        /// it's added to the result — e.g. ClusteringCoefficient's Onnela/Selfties rejection — and
        /// its failure (if any) is returned immediately, preserving each caller's original check order.
        /// </summary>
        private static OperationResult ResolveOneModeOnlyLayers(Network network, string[]? layerNames, string metricName, out List<ILayerOneMode> oneModes, Func<ILayerOneMode, string, OperationResult>? extraPerLayerCheck = null)
        {
            oneModes = [];
            if (network.Nodeset.Count == 0)
                return OperationResult.Fail("NodesMissing", "Network has no nodes.");

            OperationResult CheckLayer(string name, ILayer layer, bool namedExplicitly)
            {
                if (layer is ILayerTwoMode)
                    return OperationResult.Fail("InvalidLayerType",
                        $"Layer '{name}' is a 2-mode layer. {metricName} is not defined for 2-mode layers " +
                        $"— use projecttwomode() first{(namedExplicitly ? "" : ", or specify only 1-mode layers")}.");
                if (layer is ILayerOneMode lom)
                {
                    if (extraPerLayerCheck != null)
                    {
                        var checkResult = extraPerLayerCheck(lom, name);
                        if (!checkResult.Success) return checkResult;
                    }
                    oneModes.Add(lom);
                }
                return OperationResult.Ok();
            }

            if (layerNames == null)
            {
                foreach (var (name, layer) in network.Layers)
                {
                    var result = CheckLayer(name, layer, namedExplicitly: false);
                    if (!result.Success) return result;
                }
            }
            else
            {
                foreach (string name in layerNames)
                {
                    var lr = network.GetLayer(name);
                    if (!lr.Success) return OperationResult.Fail(lr.Code, lr.Message);
                    var result = CheckLayer(name, lr.Value!, namedExplicitly: true);
                    if (!result.Success) return result;
                }
            }

            if (oneModes.Count == 0)
                return OperationResult.Fail("NoLayers", "No 1-mode layers found.");
            return OperationResult.Ok();
        }

        /// <summary>
        /// Resolves layerNames to a 1-mode-only layer list for community detection's Girvan-Newman
        /// path, which has no 2-mode counterpart (edge betweenness has no hyperedge analogue).
        /// Thin wrapper around ResolveOneModeOnlyLayers, keeping this method's OperationResult&lt;
        /// Dictionary&lt;string,object&gt;&gt;-shaped bool/out-error signature unchanged for its caller.
        /// </summary>
        private static bool TryResolveOneModeLayersForCommunityDetection(Network network, string[]? layerNames, out List<ILayerOneMode> oneModes, out OperationResult<Dictionary<string, object>>? error)
        {
            var resolveResult = ResolveOneModeOnlyLayers(network, layerNames, "Community detection", out oneModes);
            if (!resolveResult.Success)
            {
                error = OperationResult<Dictionary<string, object>>.Fail(resolveResult.Code, resolveResult.Message);
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>
        /// Resolves layerNames for CommunityDetectionLouvain/Leiden/LabelPropagation/LPAm: any
        /// number of 1-mode layers (as <see cref="TryResolveOneModeLayersForCommunityDetection"/>
        /// accepts), or a single 2-mode layer on its own, pseudo-projected (traversed
        /// node-hyperedge-node directly, without materializing a 1-mode projection; tie strength
        /// is always count-weighted — see <see cref="CommunityFunctions"/>'s "2-mode community
        /// detection" remarks for why no projection= parameter is offered). A 2-mode layer cannot
        /// be combined with other layers. CommunityDetectionGirvanNewman keeps using the stricter
        /// <see cref="TryResolveOneModeLayersForCommunityDetection"/>, since it has no 2-mode
        /// counterpart (edge betweenness has no hyperedge analogue).
        /// </summary>
        private static bool TryResolveLayersForCommunityDetection(Network network, string[]? layerNames,
            out List<ILayerOneMode> oneModes, out ILayerTwoMode? twoMode, out OperationResult<Dictionary<string, object>>? error)
        {
            oneModes = [];
            twoMode = null;
            if (network.Nodeset.Count == 0)
            {
                error = OperationResult<Dictionary<string, object>>.Fail("NodesMissing", "Network has no nodes.");
                return false;
            }

            string? twoModeLayerName = null;
            IEnumerable<KeyValuePair<string, ILayer>> candidates;
            if (layerNames == null)
                candidates = network.Layers;
            else
            {
                var list = new List<KeyValuePair<string, ILayer>>(layerNames.Length);
                foreach (string name in layerNames)
                {
                    var lr = network.GetLayer(name);
                    if (!lr.Success)
                    {
                        error = OperationResult<Dictionary<string, object>>.Fail(lr.Code, lr.Message);
                        return false;
                    }
                    list.Add(new(name, lr.Value!));
                }
                candidates = list;
            }

            foreach (var (name, layer) in candidates)
            {
                if (layer is ILayerTwoMode ltm)
                {
                    if (twoMode != null || oneModes.Count > 0)
                    {
                        error = OperationResult<Dictionary<string, object>>.Fail("InvalidLayerType",
                            $"Layer '{name}' is a 2-mode layer. Community detection supports a single 2-mode layer on its own, not combined with other layers.");
                        return false;
                    }
                    twoMode = ltm;
                    twoModeLayerName = name;
                }
                else if (layer is ILayerOneMode lom)
                {
                    if (twoMode != null)
                    {
                        error = OperationResult<Dictionary<string, object>>.Fail("InvalidLayerType",
                            $"Layer '{name}' is a 1-mode layer, but 2-mode layer '{twoModeLayerName}' was already specified. Community detection supports a single 2-mode layer on its own, not combined with other layers.");
                        return false;
                    }
                    oneModes.Add(lom);
                }
            }

            if (twoMode == null && oneModes.Count == 0)
            {
                error = OperationResult<Dictionary<string, object>>.Fail("NoLayers", "No layers found.");
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>Default attribute name for community detection results, shared across methods.</summary>
        private static string ResolveCommunityAttrName(string[]? layerNames, string? attrName)
        {
            if (!string.IsNullOrEmpty(attrName)) return attrName;
            string layerTag = layerNames?.Length == 1 ? layerNames[0] + "_" : (layerNames == null ? "" : "multilayer_");
            return layerTag + "community";
        }

        /// <summary>
        /// Writes the community index as an int node attribute and builds the standard
        /// NbrCommunities/CommunitySizes/Modularity/NumRestarts summary shared by every community
        /// detection method.
        /// </summary>
        private static OperationResult<Dictionary<string, object>> StoreCommunityResult(Network network, string attrName, Dictionary<uint, int> communities, double modularity, int numRestarts = 1)
        {
            var attrDict = communities.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
            var setResult = network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Int);
            if (!setResult.Success)
                return OperationResult<Dictionary<string, object>>.Fail(setResult);

            int nbrCommunities = communities.Count > 0 ? communities.Values.Max() + 1 : 0;
            int[] communitySizes = new int[nbrCommunities];
            foreach (int c in communities.Values) communitySizes[c]++;

            var result = new Dictionary<string, object>
            {
                ["NbrCommunities"] = nbrCommunities,
                ["CommunitySizes"] = communitySizes.OrderByDescending(s => s).ToList(),
                ["Modularity"] = modularity,
                // The underlying detection methods treat numRestarts <= 1 as "run exactly once",
                // so report that actual run count rather than a caller-supplied 0 or negative
                // value verbatim.
                ["NumRestarts"] = Math.Max(numRestarts, 1)
            };
            return OperationResult<Dictionary<string, object>>.Ok(result);
        }
        #endregion
    }
}