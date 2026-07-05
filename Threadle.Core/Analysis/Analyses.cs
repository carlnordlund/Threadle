using System.Globalization;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
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
        /// Degree assortativity (Newman 2002): Pearson correlation between the degrees
        /// of connected node pairs. For directed layers uses out-degree of source and
        /// in-degree of target; for undirected uses degree of both endpoints.
        /// Returns a value in [-1, 1]: positive = hubs connect to hubs,
        /// negative = hubs connect to low-degree nodes.
        /// </summary>
        public static OperationResult<double> DegreeAssortativity(Network network, string layerName)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult<double>.Fail("NodesMissing", "Network has no nodes.");
            var lr = network.GetOneModeLayerForRead(layerName);
            if (!lr.Success)
                return OperationResult<double>.Fail(lr.Code, lr.Message);
            double r = NetworkLevelFunctions.DegreeAssortativity(network.Nodeset.NodeIdArray, lr.Value!);
            return OperationResult<double>.Ok(r);
        }

        /// <summary>
        /// Attribute assortativity (Newman 2003): tendency of connected nodes to share
        /// the same value of a node attribute. For Float/Int attributes uses Pearson
        /// correlation; for Char/String/Bool uses the nominal mixing formula.
        /// Returns a value in [-1, 1]. Nodes with missing attribute values are skipped.
        /// </summary>
        public static OperationResult<double> Assortativity(Network network, string layerName, string attrName)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult<double>.Fail("NodesMissing", "Network has no nodes.");
            var lr = network.GetOneModeLayerForRead(layerName);
            if (!lr.Success)
                return OperationResult<double>.Fail(lr.Code, lr.Message);

            if (!network.Nodeset.NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
                return OperationResult<double>.Fail("AttributeUnknown", $"Attribute '{attrName}' not found.");
            if (!network.Nodeset.NodeAttributeDefinitionManager.TryGetAttributeType(attrIndex, out NodeAttributeType attrType))
                return OperationResult<double>.Fail("AttributeTypeNotFound", $"Type not found for attribute '{attrName}'.");

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            var rawResult = network.Nodeset.GetMultipleNodeAttributes(nodeIds, attrName);
            if (!rawResult.Success)
                return OperationResult<double>.Fail(rawResult.Code, rawResult.Message);
            var raw = rawResult.Value!;

            double r;
            if (attrType == NodeAttributeType.Float || attrType == NodeAttributeType.Int)
            {
                var vals = new Dictionary<uint, double>(raw.Count);
                foreach (var (id, obj) in raw)
                    if (obj != null)
                        vals[id] = attrType == NodeAttributeType.Int ? (double)(int)obj : (double)(float)obj;
                r = NetworkLevelFunctions.ContinuousAssortativity(lr.Value!, vals);
            }
            else
            {
                var cats = new Dictionary<uint, string>(raw.Count);
                foreach (var (id, obj) in raw)
                    if (obj != null) cats[id] = obj.ToString()!;
                r = NetworkLevelFunctions.CategoricalAssortativity(lr.Value!, cats);
            }

            return OperationResult<double>.Ok(r);
        }

        /// <summary>
        /// Computes the MAN dyad census and reciprocity measures for a single 1-mode layer.
        /// Returns counts of Mutual, Asymmetric and Null dyads plus ArcReciprocity and
        /// DyadicReciprocity. Undirected layers are accepted: all non-null dyads are Mutual.
        /// </summary>
        public static OperationResult<Dictionary<string, object>> DyadCensus(Network network, string layerName)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult<Dictionary<string, object>>.Fail("NodesMissing", "Network has no nodes.");
            var lr = network.GetOneModeLayerForRead(layerName);
            if (!lr.Success)
                return OperationResult<Dictionary<string, object>>.Fail(lr.Code, lr.Message);
            var result = NetworkLevelFunctions.DyadCensus(network.Nodeset.Count, lr.Value!);
            return OperationResult<Dictionary<string, object>>.Ok(result);
        }

        /// <summary>
        /// Computes the coreness (k-shell index) of each node using k-core decomposition.
        /// Iteratively removes nodes with degree less than k to find the maximal k-core.
        /// Only 1-mode layers are accepted; 2-mode layers must be projected first.
        /// </summary>
        public static OperationResult Coreness(Network network, string[]? layerNames, string? attrName = null)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult.Fail("NodesMissing", "Network has no nodes.");

            List<ILayerOneMode> oneModes = [];
            if (layerNames == null)
            {
                foreach (var (name, layer) in network.Layers)
                {
                    if (layer is ILayerTwoMode)
                        return OperationResult.Fail("InvalidLayerType",
                            $"Layer '{name}' is a 2-mode layer. Coreness is not defined for 2-mode layers — use projecttwomodetoonemode() first, or specify only 1-mode layers.");
                    if (layer is ILayerOneMode lom) oneModes.Add(lom);
                }
            }
            else
            {
                foreach (string name in layerNames)
                {
                    var lr = network.GetLayer(name);
                    if (!lr.Success) return OperationResult.Fail(lr.Code, lr.Message);
                    if (lr.Value is ILayerTwoMode)
                        return OperationResult.Fail("InvalidLayerType",
                            $"Layer '{name}' is a 2-mode layer. Coreness is not defined for 2-mode layers — use projecttwomodetoonemode() first.");
                    if (lr.Value is ILayerOneMode lom) oneModes.Add(lom);
                }
            }
            if (oneModes.Count == 0)
                return OperationResult.Fail("NoLayers", "No 1-mode layers found.");

            string layerTag = layerNames?.Length == 1 ? layerNames[0] + "_" : (layerNames == null ? "" : "multilayer_");
            attrName = !string.IsNullOrEmpty(attrName) ? attrName : layerTag + "coreness";

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            var values = LocalStructureFunctions.Coreness(nodeIds, oneModes);
            var attrDict = values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Int);
        }

        /// <summary>
        /// Calculates betweenness centrality for all nodes using Brandes' algorithm with
        /// hyperedge-aware BFS. Normalizes by (n-1)(n-2) for directed, halved for undirected.
        /// When sampleSize > 0, scales the result by n/sampleSize.
        /// </summary>
        public static OperationResult BetweennessCentrality(Network network, string[]? layerNames, string? attrName = null, int sampleSize = 0, bool directed = true, EdgeTraversal traversal = EdgeTraversal.Out)
        {
            if (!TryResolveLayers(network, layerNames, out var one, out var dynTwo, out var statTwo, out var err))
                return err!;

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            uint[] sources = CentralityFunctions.SampleNodes(nodeIds, sampleSize);

            var bw = new Dictionary<uint, double>();
            var cdSum = new Dictionary<uint, double>();
            var cdReach = new Dictionary<uint, int>();
            var hm = new Dictionary<uint, double>();

            foreach (uint s in sources)
                CentralityFunctions.AccumulateBFSCentralities(s, one, dynTwo, statTwo, traversal, bw, cdSum, cdReach, hm);

            var final = CentralityFunctions.FinalizeBetweenness(bw, nodeIds, sources.Length, directed);
            attrName = string.IsNullOrEmpty(attrName) ? "betweenness" : attrName;
            var attrDict = final.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(CultureInfo.InvariantCulture));
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Float);
        }

        /// <summary>
        /// Calculates closeness centrality using Wasserman-Faust normalization, which handles
        /// disconnected components by incorporating the reachable proportion of nodes.
        /// When sampleSize > 0, uses a random subset of source nodes.
        /// </summary>
        public static OperationResult ClosenessCentrality(Network network, string[]? layerNames, string? attrName = null, int sampleSize = 0, EdgeTraversal traversal = EdgeTraversal.Out)
        {
            if (!TryResolveLayers(network, layerNames, out var one, out var dynTwo, out var statTwo, out var err))
                return err!;

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            uint[] sources = CentralityFunctions.SampleNodes(nodeIds, sampleSize);

            var bw = new Dictionary<uint, double>();
            var cdSum = new Dictionary<uint, double>();
            var cdReach = new Dictionary<uint, int>();
            var hm = new Dictionary<uint, double>();

            foreach (uint s in sources)
                CentralityFunctions.AccumulateBFSCentralities(s, one, dynTwo, statTwo, traversal, bw, cdSum, cdReach, hm);

            var final = CentralityFunctions.FinalizeCloseness(cdSum, cdReach, nodeIds);
            attrName = string.IsNullOrEmpty(attrName) ? "closeness" : attrName;
            var attrDict = final.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(CultureInfo.InvariantCulture));
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Float);
        }

        /// <summary>
        /// Calculates harmonic centrality (sum of inverse distances to all reachable nodes).
        /// When normalize = true, divides by (n-1). Handles disconnected graphs naturally.
        /// When sampleSize > 0, uses a random subset of source nodes.
        /// </summary>
        public static OperationResult HarmonicCentrality(Network network, string[]? layerNames, string? attrName = null, int sampleSize = 0, bool normalize = true, EdgeTraversal traversal = EdgeTraversal.Out)
        {
            if (!TryResolveLayers(network, layerNames, out var one, out var dynTwo, out var statTwo, out var err))
                return err!;

            uint[] nodeIds = network.Nodeset.NodeIdArray;
            uint[] sources = CentralityFunctions.SampleNodes(nodeIds, sampleSize);

            var bw = new Dictionary<uint, double>();
            var cdSum = new Dictionary<uint, double>();
            var cdReach = new Dictionary<uint, int>();
            var hm = new Dictionary<uint, double>();

            foreach (uint s in sources)
                CentralityFunctions.AccumulateBFSCentralities(s, one, dynTwo, statTwo, traversal, bw, cdSum, cdReach, hm);

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
        /// 2-mode layers must be projected first via ProjectTwoModeToOneMode().
        /// Auto-selects formula based on layer type (directed/undirected, binary/valued) unless
        /// method is specified explicitly.
        /// When multiple layers are specified, adjacency is the union across all layers.
        /// </summary>
        public static OperationResult ClusteringCoefficient(Network network, string[]? layerNames, string? attrName = null, ClusteringMethod method = ClusteringMethod.Auto, int sampleSize = 0)
        {
            if (network.Nodeset.Count == 0)
                return OperationResult.Fail("NodesMissing", "Network has no nodes.");

            List<ILayerOneMode> oneModes = [];
            if (layerNames == null)
            {
                foreach (var (name, layer) in network.Layers)
                {
                    if (layer is ILayerTwoMode)
                        return OperationResult.Fail("InvalidLayerType",
                            $"Layer '{name}' is a 2-mode layer. Clustering coefficient is not defined for 2-mode layers — use projecttwomodetoonemode() first, or specify only 1-mode layers.");
                    if (layer is ILayerOneMode lom) oneModes.Add(lom);
                }
            }
            else
            {
                foreach (string name in layerNames)
                {
                    var lr = network.GetLayer(name);
                    if (!lr.Success) return OperationResult.Fail(lr.Code, lr.Message);
                    if (lr.Value is ILayerTwoMode)
                        return OperationResult.Fail("InvalidLayerType",
                            $"Layer '{name}' is a 2-mode layer. Clustering coefficient is not defined for 2-mode layers — use projecttwomodetoonemode() first.");
                    if (lr.Value is ILayerOneMode lom) oneModes.Add(lom);
                }
            }
            if (oneModes.Count == 0)
                return OperationResult.Fail("NoLayers", "No 1-mode layers found.");

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
            if (network.Nodeset.Count == 0)
                return OperationResult<double>.Fail("NodesMissing", "Network has no nodes.");

            List<ILayerOneMode> oneModes = [];
            if (layerNames == null)
            {
                foreach (var (name, layer) in network.Layers)
                {
                    if (layer is ILayerTwoMode)
                        return OperationResult<double>.Fail("InvalidLayerType",
                            $"Layer '{name}' is a 2-mode layer. Transitivity is not defined for 2-mode layers.");
                    if (layer is ILayerOneMode lom) oneModes.Add(lom);
                }
            }
            else
            {
                foreach (string name in layerNames)
                {
                    var lr = network.GetLayer(name);
                    if (!lr.Success) return OperationResult<double>.Fail(lr.Code, lr.Message);
                    if (lr.Value is ILayerTwoMode)
                        return OperationResult<double>.Fail("InvalidLayerType",
                            $"Layer '{name}' is a 2-mode layer.");
                    if (lr.Value is ILayerOneMode lom) oneModes.Add(lom);
                }
            }
            if (oneModes.Count == 0)
                return OperationResult<double>.Fail("NoLayers", "No 1-mode layers found.");

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


        ///// <summary>
        ///// Calculates the shortest path between two nodes, either for a particular layer or for all layers.
        ///// To work with all layers, set layerName to an empty string. Note that the shortest path takes edge
        ///// directionality into account: if a layer has directional edges, this matters. For layers that are symmetric,
        ///// the directionality is moot.
        ///// If there is no path between the nodes, a distance of -1 is returned: note that this is also
        ///// wrapped in a OperationResult.Success.
        ///// Uses bidirectional BFS with hyperedge-aware expansion for 2-mode layers: each hyperedge's members
        ///// are iterated at most once per direction, preventing frontier explosion from large affiliations
        ///// (e.g. workplaces with 10k members).
        ///// </summary>
        ///// <param name="network">The network.</param>
        ///// <param name="layerNames">The names of the layers to use (or null to use all layers).</param>
        ///// <param name="nodeIdFrom">The source node id.</param>
        ///// <param name="nodeIdTo">The destination node id.</param>
        ///// <param name="returnPath">If true, the result also contains the sequence of node ids along the shortest path.</param>
        ///// <returns>An OperationResult containing a ShortestPathResult with distance and optional path.</returns>
        //public static OperationResult<ShortestPathResult> ShortestPath(Network network, string[]? layerNames, uint nodeIdFrom, uint nodeIdTo, bool returnPath = false)
        //{
        //    OperationResult nodeCheckResult = network.Nodeset.CheckThatNodesExist(nodeIdFrom, nodeIdTo);
        //    if (!nodeCheckResult.Success)
        //        return OperationResult<ShortestPathResult>.Fail(nodeCheckResult.Code, nodeCheckResult.Message);
        //    if (nodeIdFrom == nodeIdTo)
        //        return OperationResult<ShortestPathResult>.Ok(new ShortestPathResult(0, returnPath ? [nodeIdFrom] : null));

        //    List<ILayer> resolvedLayers = [];
        //    if (layerNames == null)
        //        resolvedLayers.AddRange(network.Layers.Values);
        //    else
        //    {
        //        foreach (string layerName in layerNames)
        //        {
        //            var layerResult = network.GetLayer(layerName);
        //            if (!layerResult.Success)
        //                return OperationResult<ShortestPathResult>.Fail(layerResult);
        //            resolvedLayers.Add(layerResult.Value!);
        //        }
        //    }

        //    GraphAlgorithms.SplitLayers(resolvedLayers, out var oneModes, out var twoModesDynamic, out var twoModesStatic);

        //    var fwdDynVisited = Array.ConvertAll(twoModesDynamic.ToArray(), _ => new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance));
        //    var bwdDynVisited = Array.ConvertAll(twoModesDynamic.ToArray(), _ => new HashSet<Hyperedge>(ReferenceEqualityComparer.Instance));
        //    var fwdStatVisited = Array.ConvertAll(twoModesStatic.ToArray(), _ => new HashSet<int>());
        //    var bwdStatVisited = Array.ConvertAll(twoModesStatic.ToArray(), _ => new HashSet<int>());

        //    var distFwd = new Dictionary<uint, int> { [nodeIdFrom] = 0 };
        //    var distBwd = new Dictionary<uint, int> { [nodeIdTo] = 0 };
        //    Dictionary<uint, uint>? predFwd = returnPath ? [] : null;
        //    Dictionary<uint, uint>? predBwd = returnPath ? [] : null;
        //    List<uint> frontierFwd = [nodeIdFrom];
        //    List<uint> frontierBwd = [nodeIdTo];
        //    int best = int.MaxValue;
        //    uint meetingNode = 0;
        //    int dFwd = 0, dBwd = 0;

        //    while (frontierFwd.Count > 0 || frontierBwd.Count > 0)
        //    {
        //        if (best != int.MaxValue && best <= dFwd + dBwd + 1)
        //            break;

        //        if (frontierFwd.Count > 0 && (frontierBwd.Count == 0 || frontierFwd.Count <= frontierBwd.Count))
        //        {
        //            dFwd++;
        //            List<uint> next = [];
        //            foreach (uint u in frontierFwd)
        //            {
        //                foreach (var layer in oneModes)
        //                    foreach (uint v in layer.GetNodeAlters(u, EdgeTraversal.Out))
        //                        if (distFwd.TryAdd(v, dFwd))
        //                        {
        //                            next.Add(v);
        //                            if (returnPath) predFwd![v] = u;
        //                            if (distBwd.TryGetValue(v, out int bd))
        //                            {
        //                                int c = dFwd + bd;
        //                                if (c < best) { best = c; meetingNode = v; }
        //                            }
        //                        }

        //                for (int li = 0; li < twoModesDynamic.Count; li++)
        //                {
        //                    var hec = twoModesDynamic[li].GetNonEmptyHyperedgeCollection(u);
        //                    if (hec == null) continue;
        //                    foreach (var he in hec.HyperEdges)
        //                        if (fwdDynVisited[li].Add(he))
        //                            foreach (uint m in he.NodeIds)
        //                                if (distFwd.TryAdd(m, dFwd))
        //                                {
        //                                    next.Add(m);
        //                                    if (returnPath) predFwd![m] = u;
        //                                    if (distBwd.TryGetValue(m, out int bd))
        //                                    {
        //                                        int c = dFwd + bd;
        //                                        if (c < best) { best = c; meetingNode = m; }
        //                                    }
        //                                }
        //                }

        //                for (int li = 0; li < twoModesStatic.Count; li++)
        //                {
        //                    if (!twoModesStatic[li].TryGetNodeHyperedgeRange(u, out int nStart, out int nEnd)) continue;
        //                    for (int k = nStart; k < nEnd; k++)
        //                    {
        //                        int hIdx = twoModesStatic[li].GetNodeHyperedgeIndex(k);
        //                        if (!fwdStatVisited[li].Add(hIdx)) continue;
        //                        twoModesStatic[li].GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
        //                        for (int j = hStart; j < hEnd; j++)
        //                        {
        //                            uint m = twoModesStatic[li].GetHyperedgeNodeAt(j);
        //                            if (distFwd.TryAdd(m, dFwd))
        //                            {
        //                                next.Add(m);
        //                                if (returnPath) predFwd![m] = u;
        //                                if (distBwd.TryGetValue(m, out int bd))
        //                                {
        //                                    int c = dFwd + bd;
        //                                    if (c < best) { best = c; meetingNode = m; }
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //            frontierFwd = next;
        //        }
        //        else
        //        {
        //            dBwd++;
        //            List<uint> next = [];
        //            foreach (uint u in frontierBwd)
        //            {
        //                foreach (var layer in oneModes)
        //                    foreach (uint v in layer.GetNodeAlters(u, EdgeTraversal.In))
        //                        if (distBwd.TryAdd(v, dBwd))
        //                        {
        //                            next.Add(v);
        //                            if (returnPath) predBwd![v] = u;
        //                            if (distFwd.TryGetValue(v, out int fd))
        //                            {
        //                                int c = fd + dBwd;
        //                                if (c < best) { best = c; meetingNode = v; }
        //                            }
        //                        }

        //                for (int li = 0; li < twoModesDynamic.Count; li++)
        //                {
        //                    var hec = twoModesDynamic[li].GetNonEmptyHyperedgeCollection(u);
        //                    if (hec == null) continue;
        //                    foreach (var he in hec.HyperEdges)
        //                        if (bwdDynVisited[li].Add(he))
        //                            foreach (uint m in he.NodeIds)
        //                                if (distBwd.TryAdd(m, dBwd))
        //                                {
        //                                    next.Add(m);
        //                                    if (returnPath) predBwd![m] = u;
        //                                    if (distFwd.TryGetValue(m, out int fd))
        //                                    {
        //                                        int c = fd + dBwd;
        //                                        if (c < best) { best = c; meetingNode = m; }
        //                                    }
        //                                }
        //                }

        //                for (int li = 0; li < twoModesStatic.Count; li++)
        //                {
        //                    if (!twoModesStatic[li].TryGetNodeHyperedgeRange(u, out int nStart, out int nEnd)) continue;
        //                    for (int k = nStart; k < nEnd; k++)
        //                    {
        //                        int hIdx = twoModesStatic[li].GetNodeHyperedgeIndex(k);
        //                        if (!bwdStatVisited[li].Add(hIdx)) continue;
        //                        twoModesStatic[li].GetHyperedgeRange(hIdx, out int hStart, out int hEnd);
        //                        for (int j = hStart; j < hEnd; j++)
        //                        {
        //                            uint m = twoModesStatic[li].GetHyperedgeNodeAt(j);
        //                            if (distBwd.TryAdd(m, dBwd))
        //                            {
        //                                next.Add(m);
        //                                if (returnPath) predBwd![m] = u;
        //                                if (distFwd.TryGetValue(m, out int fd))
        //                                {
        //                                    int c = fd + dBwd;
        //                                    if (c < best) { best = c; meetingNode = m; }
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //            frontierBwd = next;
        //        }
        //    }

        //    int distance = best == int.MaxValue ? -1 : best;

        //    uint[]? path = null;
        //    if (returnPath && distance >= 0)
        //    {
        //        // Trace forward half: meetingNode → nodeIdFrom via predFwd, then reverse
        //        var pathList = new List<uint>();
        //        uint cur = meetingNode;
        //        while (cur != nodeIdFrom)
        //        {
        //            pathList.Add(cur);
        //            cur = predFwd![cur];
        //        }
        //        pathList.Add(nodeIdFrom);
        //        pathList.Reverse(); // now [nodeIdFrom, ..., meetingNode]

        //        // Trace backward half: meetingNode → nodeIdTo via predBwd
        //        cur = meetingNode;
        //        while (cur != nodeIdTo)
        //        {
        //            cur = predBwd![cur];
        //            pathList.Add(cur);
        //        }
        //        path = [.. pathList];
        //    }

        //    return OperationResult<ShortestPathResult>.Ok(new ShortestPathResult(distance, path));
        //}

        /// <summary>
        /// Calculates the density of the specified layer in the network. Can be 1-mode or 2-mode.
        /// Using different methods for 1- resp 2-mode layers.
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
        /// </summary>
        public static OperationResult<uint> GetRandomAlter(Network network, uint nodeId, string[]? layerNames, EdgeTraversal edgeTraversal = EdgeTraversal.Both, bool balanced = false, bool weighted = false)
        {
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
            return GetRandomAlter(nodeId, layersToUse, edgeTraversal, balanced, weighted);
        }

        /// <summary>
        /// Hot-path overload: accepts pre-resolved layers to avoid per-step layer lookup and List allocation.
        /// For non-balanced 2-mode layers the fast path (PickRandomAlterO1 / AppendProjectedAltersReservoir)
        /// is used via ILayerTwoMode, covering both LayerTwoMode and LayerTwoModeStatic.
        /// </summary>
        public static OperationResult<uint> GetRandomAlter(uint nodeId, IReadOnlyList<ILayer> layersToUse, EdgeTraversal edgeTraversal = EdgeTraversal.Both, bool balanced = false, bool weighted = false)
        {
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
                    List<ILayer> eligibleLayers = layersToUse
                        .Where(l => l.GetNodeAlters(nodeId, edgeTraversal).Length > 0)
                        .ToList();
                    if (eligibleLayers.Count == 0)
                        return OperationResult<uint>.Fail("ConstraintNoAlters", $"Node {nodeId} has no alters in any of the specified layers with the given edge traversal.");
                    Functions.AppendWeightedCandidates(candidates, eligibleLayers[Misc.Random.Next(eligibleLayers.Count)], nodeId, edgeTraversal);
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
                // Balanced and multiple layers: uniform pick of layer, then uniform pick within
                List<uint[]> layerAltersList = layersToUse
                    .Select(l => l.GetNodeAlters(nodeId, edgeTraversal))
                    .Where(a => a.Length > 0)
                    .ToList();
                if (layerAltersList.Count == 0)
                    return OperationResult<uint>.Fail("ConstraintNoAlters", $"Node {nodeId} has no alters in any of the specified layers with the given edge traversal.");
                uint[] chosenLayerAlters = layerAltersList[Misc.Random.Next(layerAltersList.Count)];
                return OperationResult<uint>.Ok(chosenLayerAlters[Misc.Random.Next(chosenLayerAlters.Length)]);
            }
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
        #endregion
    }
}
