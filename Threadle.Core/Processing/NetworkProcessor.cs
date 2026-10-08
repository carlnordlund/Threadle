using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Processing
{
    /// <summary>
    /// Various methods for processing Network structures
    /// </summary>
    public static class NetworkProcessor
    {
        #region Fields
        /// <summary>
        /// Constant representing a very small number
        /// </summary>
        private const float epsilon = 1e-6f;
        #endregion


        #region Methods (public)

        /// <summary>
        /// Converts a binary 1-mode layer into a valued layer with the same directionality, where every
        /// existing tie is given the value 1. Topology is otherwise unchanged — this is a pure type
        /// promotion, not a re-weighting. Note that conversion creates a new layer, without modifying
        /// the original layer.
        /// </summary>
        /// <param name="network">The Network object.</param>
        /// <param name="layerName">The name of the 1-mode binary layer to convert.</param>
        /// <param name="newLayerName">The name of the new 1-mode valued layer to store the converted data in.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult BinaryToValuedLayer(Network network, string layerName, string newLayerName)
        {
            if (!network.Layers.TryGetValue(layerName, out var layer))
                return OperationResult.Fail("LayerNotFound", $"Layer '{layerName}' does not exist in network '{network.Name}'.");
            if (!(layer is ILayerOneMode originalLayer))
                return OperationResult.Fail("InvalidLayerType", $"Layer '{layerName}' is not a 1-mode layer.");
            if (!originalLayer.IsBinary)
                return OperationResult.Fail("ConstraintLayerAlreadyValued", $"Layer '{layerName}' is already valued, conversion to a valued layer is not applicable.");
            if (network.Layers.ContainsKey(newLayerName))
                return OperationResult.Fail("LayerAlreadyExists", $"Layer '{newLayerName}' already exists in network '{network.Name}'.");

            LayerOneMode newLayer = new LayerOneMode(newLayerName, originalLayer.Directionality, EdgeType.Valued, originalLayer.Selfties);
            foreach (var (nodeId, alters, _) in originalLayer.GetAllEgoData())
                foreach (uint partnerId in alters.Span)
                    newLayer._addEdge(nodeId, partnerId, 1f);

            // GetAllEgoData() never yields self-loops for symmetric layers — a symmetric edgeset
            // only yields each edge once, from the lower node id (partnerId > egoId), a condition a
            // self-loop can never satisfy. Add them explicitly so they aren't silently dropped.
            if (originalLayer.IsSymmetric && originalLayer.Selfties)
                foreach (uint nodeId in network.Nodeset.NodeIdArray)
                    if (originalLayer.GetEdgeValue(nodeId, nodeId) > 0)
                        newLayer._addEdge(nodeId, nodeId, 1f);

            newLayer._sortEdgesets();
            network.AddLayer(newLayerName, newLayer);
            return OperationResult.Ok($"Converted layer '{layerName}' to a valued layer (all ties set to 1) and stored it as new layer '{newLayerName}', all in network '{network.Name}'.");
        }

        /// <summary>
        /// Converts a symmetric (undirected) 1-mode layer into a directed layer, turning each undirected
        /// tie into two independent directed arcs (one in each direction) carrying the same value. The
        /// resulting layer keeps the original's edge value type (binary stays binary, valued stays
        /// valued) — this is purely a directionality conversion. Note that conversion creates a new
        /// layer, without modifying the original layer.
        /// </summary>
        /// <param name="network">The Network object.</param>
        /// <param name="layerName">The name of the 1-mode undirected layer to convert.</param>
        /// <param name="newLayerName">The name of the new 1-mode directed layer to store the converted data in.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult SymmetricToDirectedLayer(Network network, string layerName, string newLayerName)
        {
            if (!network.Layers.TryGetValue(layerName, out var layer))
                return OperationResult.Fail("LayerNotFound", $"Layer '{layerName}' does not exist in network '{network.Name}'.");
            if (!(layer is ILayerOneMode originalLayer))
                return OperationResult.Fail("InvalidLayerType", $"Layer '{layerName}' is not a 1-mode layer.");
            if (!originalLayer.IsSymmetric)
                return OperationResult.Fail("ConstraintLayerAlreadyDirected", $"Layer '{layerName}' is already directed, conversion to a directed layer is not applicable.");
            if (network.Layers.ContainsKey(newLayerName))
                return OperationResult.Fail("LayerAlreadyExists", $"Layer '{newLayerName}' already exists in network '{network.Name}'.");

            LayerOneMode newLayer = new LayerOneMode(newLayerName, EdgeDirectionality.Directed, originalLayer.EdgeValueType, originalLayer.Selfties);
            foreach (var (nodeId, alters, values) in originalLayer.GetAllEgoData())
            {
                // GetAllEgoData() never yields self-loops for symmetric layers (see BinaryToValuedLayer),
                // so every pair here is guaranteed nodeId != partnerId — both directions always apply.
                bool hasValues = values.Length > 0;
                for (int i = 0; i < alters.Length; i++)
                {
                    uint partnerId = alters.Span[i];
                    float value = hasValues ? values.Span[i] : 1f;
                    newLayer._addEdge(nodeId, partnerId, value);
                    newLayer._addEdge(partnerId, nodeId, value);
                }
            }

            // Self-loops are excluded from GetAllEgoData() for symmetric layers (see above) — add
            // them explicitly. A self-loop needs only one _addEdge call: node i's arc to itself.
            if (originalLayer.Selfties)
                foreach (uint nodeId in network.Nodeset.NodeIdArray)
                {
                    float selfValue = originalLayer.GetEdgeValue(nodeId, nodeId);
                    if (selfValue > 0)
                        newLayer._addEdge(nodeId, nodeId, selfValue);
                }

            newLayer._sortEdgesets();
            network.AddLayer(newLayerName, newLayer);
            return OperationResult.Ok($"Converted layer '{layerName}' to a directed layer (two arcs per original tie) and stored it as new layer '{newLayerName}', all in network '{network.Name}'.");
        }

        /// <summary>
        /// Merges two 1-mode layers into a new layer by the provided method, storing the result under
        /// the provided name. Note that merging creates a new layer, without modifying either source layer.
        /// Both source layers must share the same directionality (both directed or both undirected) —
        /// symmetrize the directed one first with <see cref="SymmetrizeLayer"/> if they don't match.
        /// The method fully determines the resulting layer's edge type: And/Or/Xor always produce a
        /// binary layer (does a tie exist, ignoring its strength); Sum/Average always produce a valued
        /// layer; Max/Min/Product produce a binary layer only if both source layers are binary, valued
        /// otherwise. For binary source layers, And is equivalent to Product and Or is equivalent to Max;
        /// Xor (true iff exactly one source layer has the tie) is not expressible via the other methods.
        /// </summary>
        /// <param name="network">The Network object.</param>
        /// <param name="layerName1">The name of the first 1-mode layer to merge.</param>
        /// <param name="layerName2">The name of the second 1-mode layer to merge.</param>
        /// <param name="method">The <see cref="MergeMethod"/> to use when combining edge values.</param>
        /// <param name="newLayerName">The name of the new layer to store the merged data in.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult MergeLayers(Network network, string layerName1, string layerName2, MergeMethod method, string newLayerName)
        {
            if (!network.Layers.TryGetValue(layerName1, out var layerA))
                return OperationResult.Fail("LayerNotFound", $"Layer '{layerName1}' does not exist in network '{network.Name}'.");
            if (!network.Layers.TryGetValue(layerName2, out var layerB))
                return OperationResult.Fail("LayerNotFound", $"Layer '{layerName2}' does not exist in network '{network.Name}'.");
            if (!(layerA is ILayerOneMode oneModeA))
                return OperationResult.Fail("InvalidLayerType", $"Layer '{layerName1}' is not a 1-mode layer.");
            if (!(layerB is ILayerOneMode oneModeB))
                return OperationResult.Fail("InvalidLayerType", $"Layer '{layerName2}' is not a 1-mode layer.");
            if (oneModeA.IsSymmetric != oneModeB.IsSymmetric)
                return OperationResult.Fail("MismatchedDirectionality",
                    $"Layer '{layerName1}' and '{layerName2}' have different directionality (one is directed, the other undirected). Symmetrize the directed layer first with symmetrizelayer().");
            if (network.Layers.ContainsKey(newLayerName))
                return OperationResult.Fail("LayerAlreadyExists", $"Layer '{newLayerName}' already exists in network '{network.Name}'.");

            bool bothBinary = oneModeA.IsBinary && oneModeB.IsBinary;
            bool outputBinary = method switch
            {
                MergeMethod.And or MergeMethod.Or or MergeMethod.Xor => true,
                MergeMethod.Sum or MergeMethod.Average => false,
                _ => bothBinary // Max, Min, Product
            };
            EdgeType outputEdgeType = outputBinary ? EdgeType.Binary : EdgeType.Valued;

            Func<float, float, float> MergeFunction = method switch
            {
                MergeMethod.And => (a, b) => (a > 0 && b > 0) ? 1f : 0f,
                MergeMethod.Or => (a, b) => (a > 0 || b > 0) ? 1f : 0f,
                MergeMethod.Xor => (a, b) => ((a > 0) != (b > 0)) ? 1f : 0f,
                MergeMethod.Sum => (a, b) => a + b,
                MergeMethod.Average => (a, b) => (a + b) / 2f,
                MergeMethod.Max => (a, b) => Math.Max(a, b),
                MergeMethod.Min => (a, b) => Math.Min(a, b),
                _ => (a, b) => a * b // Product
            };

            bool selfties = oneModeA.Selfties || oneModeB.Selfties;
            LayerOneMode newLayer = new LayerOneMode(newLayerName, oneModeA.Directionality, outputEdgeType, selfties);

            // Union of all node pairs with an edge in either layer.
            var pairs = new HashSet<(uint, uint)>();
            foreach (var (nodeId, alters, _) in oneModeA.GetAllEgoData())
                foreach (uint partnerId in alters.Span)
                    pairs.Add((nodeId, partnerId));
            foreach (var (nodeId, alters, _) in oneModeB.GetAllEgoData())
                foreach (uint partnerId in alters.Span)
                    pairs.Add((nodeId, partnerId));

            foreach (var (node1, node2) in pairs)
            {
                float valA = oneModeA.GetEdgeValue(node1, node2);
                float valB = oneModeB.GetEdgeValue(node1, node2);
                float merged = MergeFunction(valA, valB);
                if (merged > 0)
                    newLayer._addEdge(node1, node2, merged);
            }

            newLayer._sortEdgesets();
            newLayer._deduplicateEdgesets();
            network.AddLayer(newLayerName, newLayer);
            return OperationResult.Ok($"Merged layers '{layerName1}' and '{layerName2}' via {method} and stored the result as new layer '{newLayerName}', all in network '{network.Name}'.");
        }


        /// <summary>
        /// Symmetrizes the directed edges in a 1-mode layer by the provided method, storing these new symmetrized
        /// edges in a new symmetric 1-mode layer with the same value type with the provided name.
        /// Note that symmetrization creates a new layer, without modifying anything in the layer that was symmetrized.
        /// </summary>
        /// <param name="network">The Network object.</param>
        /// <param name="layerName">The name of the 1-mode valued layer to symmetrize.</param>
        /// <param name="method">The <see cref="SymmetrizeMethod"/> to use when symmetrizing.</param>
        /// <param name="newLayerName">The name of the new 1-mode binary layer to store the symmetrized data in.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult SymmetrizeLayer(Network network, string layerName, SymmetrizeMethod method, string newLayerName)
        {
            if (!network.Layers.TryGetValue(layerName, out var layer))
                return OperationResult.Fail("LayerNotFound", $"Layer '{layerName}' does not exist in network '{network.Name}'.");
            if (!(layer is ILayerOneMode originalLayer))
                return OperationResult.Fail("InvalidLayerType", $"Layer '{layerName}' is not a 1-mode layer.");
            if (originalLayer.IsSymmetric)
                return OperationResult.Ok($"Layer '{layerName}' is already symmetric.");

            bool needsValuedOutput = method == SymmetrizeMethod.sum || method == SymmetrizeMethod.average;
            EdgeType outputEdgeType = needsValuedOutput ? EdgeType.Valued : originalLayer.EdgeValueType;
            LayerOneMode newLayer = new LayerOneMode(newLayerName, EdgeDirectionality.Undirected, outputEdgeType, originalLayer.Selfties);

            Func<float, float, float> SymmetrizeFunction = method switch
            {
                SymmetrizeMethod.max => (a, b) => Math.Max(a, b),
                SymmetrizeMethod.minnonzero => (a, b) =>
                {
                    if (a == 0) return b;
                    if (b == 0) return a;
                    return Math.Min(a, b);
                }
                ,
                SymmetrizeMethod.sum => (a, b) => a + b,
                SymmetrizeMethod.average => (a, b) => (a + b) / 2,
                SymmetrizeMethod.product => (a, b) => a * b,
                _ => (a, b) => Math.Min(a, b)
            };

            if (originalLayer.IsBinary)
            {
                // Edges are binary
                foreach (var (nodeId, alters, _) in originalLayer.GetAllEgoData())
                    foreach (uint partnerNodeId in alters.Span)
                    {
                        if (newLayer.CheckEdgeExists(nodeId, partnerNodeId))
                            continue;
                        else
                        {
                            float reverseVal = originalLayer.GetEdgeValue(partnerNodeId, nodeId) > 0 ? 1f : 0f;
                            float val = SymmetrizeFunction(1f, reverseVal);
                            if (val > 0)
                                newLayer._addEdge(nodeId, partnerNodeId, val);
                        }
                    }
            }
            else
            {
                // Edges are valued
                foreach (var (nodeId, alters, values) in originalLayer.GetAllEgoData())
                    for (int i = 0; i < alters.Length; i++)
                    {
                        uint partnerNodeId = alters.Span[i];
                        if (newLayer.CheckEdgeExists(nodeId, partnerNodeId))
                            continue;
                        float val = SymmetrizeFunction(values.Span[i], originalLayer.GetEdgeValue(partnerNodeId, nodeId));
                        if (val > 0)
                            newLayer._addEdge(nodeId, partnerNodeId, val);
                    }
            }
            // Sort all partner NodeIds in the Edgesets - mostly cosmetics, but looks better when writing to tsv and when
            // getting node alters.
            newLayer._sortEdgesets();
            network.AddLayer(newLayerName, newLayer);
            return OperationResult.Ok($"Symmetrized layer '{layerName}' and stored it as new layer '{newLayerName}', all in network '{network.Name}'.");
        }


        /// <summary>
        /// Dichotomizes the valued edges in a 1-mode layer by the provided condition type and threshold, storing these
        /// new dichotomized edges in a new binary 1-mode layer with the same directionality with the provided name.
        /// The comparison condition, threshold, values-if-true and values-if-false are all customizable.
        /// Note that dichotomization creates a new layer, without modifying anything in the layer that is being dichotomized.
        /// </summary>
        /// <param name="network">The Network object.</param>
        /// <param name="layerName">The name of the 1-mode valued layer to dichotomize.</param>
        /// <param name="conditionType">The <see cref="ConditionType"/> condition.</param>
        /// <param name="threshold">The threshold value to compare edge values with.</param>
        /// <param name="trueValue">The value that edges should be set to if condition is true. If set to float.NaN, the existing value will be kept.</param>
        /// <param name="falseValue">The value that edges should be set to if condition is false. If set to float.NaN, the existing value will be kept.</param>
        /// <param name="newLayerName">The name of the new 1-mode binary layer to store the dichotomized data in.</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult DichotomizeLayer(Network network, string layerName, ConditionType conditionType, float threshold, float trueValue, float falseValue, string newLayerName)
        {
            if (!network.Layers.ContainsKey(layerName))
                return OperationResult.Fail("LayerNotFound", $"Layer '{layerName}' does not exist in network '{network.Name}'.");
            if (!(network.Layers[layerName] is ILayerOneMode originalLayer))
                return OperationResult.Fail("InvalidLayerType", $"Layer '{layerName}' is not a 1-mode layer.");
            if (originalLayer.IsBinary)
                return OperationResult.Fail("ConstraintLayerAlreadyBinary", $"Layer '{layerName}' is already binary, dichotomization is not applicable.");
            if (conditionType == ConditionType.isnull || conditionType == ConditionType.notnull)
                return OperationResult.Fail("InvalidCondition", $"Condition '{conditionType}' is invalid for dichotomization.");
            if (network.Layers.ContainsKey(newLayerName))
                return OperationResult.Fail("LayerAlreadyExists", $"Layer '{newLayerName}' already exists in network '{network.Name}'.");
            EdgeType newEdgeType = (IsZeroOrOne(trueValue) && IsZeroOrOne(falseValue)) ? EdgeType.Binary : EdgeType.Valued;

            LayerOneMode newLayer = new LayerOneMode(newLayerName, originalLayer.Directionality, newEdgeType, originalLayer.Selfties);
            foreach (var (nodeId, alters, values) in originalLayer.GetAllEgoData())
                for (int i = 0; i < alters.Length; i++)
                {
                    float edgeValue = values.Span[i];
                    if (Misc.CompareValues<float>(edgeValue, threshold, conditionType))
                    {
                        float valueToAssign = float.IsNaN(trueValue) ? edgeValue : trueValue;
                        newLayer._addEdge(nodeId, alters.Span[i], valueToAssign);
                    }
                    else
                    {
                        float valueToAssign = float.IsNaN(falseValue) ? edgeValue : falseValue;
                        if (!IsZeroOrOne(valueToAssign) || valueToAssign != 0f)
                            newLayer._addEdge(nodeId, alters.Span[i], valueToAssign);
                    }
                }
            newLayer._sortEdgesets();
            newLayer._deduplicateEdgesets();
            network.AddLayer(newLayerName, newLayer);
            return OperationResult.Ok($"Dichotomized layer '{layerName}' and stored it as new layer '{newLayerName}', all in network '{network.Name}'.");
        }

        /// <summary>
        /// Randomizes a 1-mode layer via repeated degree-preserving edge swaps (Markov chain
        /// switching; Maslov &amp; Sneppen 2002), storing the result in a new layer with the same
        /// directionality, edge type and selfties setting as the original layer, which is left
        /// unmodified. Every node's degree — in- and out-degree separately, for directed layers —
        /// is preserved exactly throughout, since a swap is only accepted if it would not create a
        /// self-loop the layer disallows or duplicate an already-existing edge.
        /// <para>
        /// Edge values travel with the edge object being repositioned, not with either endpoint as
        /// such. For the '2edge' swap on a directed layer this means each edge's original tail
        /// stays fixed and its value stays attached to that tail as its head is reassigned
        /// (igraph's convention: pick two arcs (a,b) and (c,d), reconnect as (a,d) and (c,b)). For
        /// '2edge' on a symmetric layer — which has no real tail/head to anchor a value to — which
        /// endpoint of each edge is treated as the fixed side for that swap is chosen
        /// independently at random every time, rather than by any fixed rule.
        /// </para>
        /// <para>
        /// 'threeEdgeSwap' selects networkx's directed_edge_swap convention instead (ignored for
        /// symmetric layers, which always use '2edge'): swaps along a 3-arc path a→b→c→d to
        /// a→c→b→d. This exists because the simpler 2-edge swap is not guaranteed to be able to
        /// reach every digraph with the same in/out-degree sequence, while this path-based form
        /// is. Its middle arc is effectively reversed by the move (b→c becomes c→b), so that arc's
        /// value ends up following a reversed connection rather than a simple tail-anchored one —
        /// an inherent property of this swap's mechanics. Since it only ever operates on a path of
        /// 4 distinct nodes, it never disturbs any self-loops already present in the layer.
        /// </para>
        /// 'numSwaps' is the target number of successful swaps (0: ten times the layer's edge
        /// count, a common mixing heuristic); 'maxTries' caps the total number of attempts,
        /// successful or not, so a layer too dense or structured to find enough valid swaps does
        /// not loop unboundedly (0: ten times 'numSwaps').
        /// </summary>
        /// <param name="network">The Network object.</param>
        /// <param name="layerName">The name of the 1-mode layer to rewire.</param>
        /// <param name="newLayerName">The name of the new 1-mode layer to store the rewired result in.</param>
        /// <param name="numSwaps">Target number of successful swaps (0: ten times the edge count).</param>
        /// <param name="threeEdgeSwap">Use the 3-edge path swap instead of the 2-edge swap (directed layers only).</param>
        /// <param name="maxTries">Maximum total swap attempts, successful or not (0: ten times numSwaps).</param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
        public static OperationResult RewireLayer(Network network, string layerName, string newLayerName, int numSwaps = 0, bool threeEdgeSwap = false, int maxTries = 0)
        {
            var layerResult = network.GetOneModeLayerForRead(layerName);
            if (!layerResult.Success)
                return OperationResult.Fail(layerResult.Code, layerResult.Message);
            ILayerOneMode layer = layerResult.Value!;
            if (network.Layers.ContainsKey(newLayerName))
                return OperationResult.Fail("LayerAlreadyExists", $"Layer '{newLayerName}' already exists in network '{network.Name}'.");

            bool directed = layer.IsDirectional;
            bool selfties = layer.Selfties;

            // Gather all edges (each undirected edge once; self-loops added explicitly, since
            // GetAllEgoData() never yields them for symmetric layers).
            var edges = new List<(uint From, uint To, float Weight)>();
            foreach (var (egoId, alters, values) in layer.GetAllEgoData())
            {
                ReadOnlySpan<uint> alterSpan = alters.Span;
                ReadOnlySpan<float> valueSpan = values.Span;
                for (int i = 0; i < alterSpan.Length; i++)
                    edges.Add((egoId, alterSpan[i], layer.IsValued ? valueSpan[i] : 1f));
            }
            if (!directed && selfties)
                foreach (uint nodeId in network.Nodeset.NodeIdArray)
                    if (layer.CheckEdgeExists(nodeId, nodeId))
                        edges.Add((nodeId, nodeId, layer.IsValued ? layer.GetEdgeValue(nodeId, nodeId) : 1f));

            int edgeCount = edges.Count;
            int targetSwaps = numSwaps > 0 ? numSwaps : 10 * edgeCount;
            int attemptCap = maxTries > 0 ? maxTries : 10 * targetSwaps;

            int achievedSwaps = 0;
            if (edgeCount >= 2 && targetSwaps > 0)
                achievedSwaps = (directed && threeEdgeSwap)
                    ? RunThreeEdgeSwaps(edges, targetSwaps, attemptCap)
                    : RunTwoEdgeSwaps(edges, directed, selfties, targetSwaps, attemptCap);

            LayerOneMode newLayer = new LayerOneMode(newLayerName, layer.Directionality, layer.IsValued ? EdgeType.Valued : EdgeType.Binary, selfties);
            foreach (var (from, to, weight) in edges)
                newLayer._addEdge(from, to, weight);
            newLayer._sortEdgesets();
            newLayer._deduplicateEdgesets();
            network.AddLayer(newLayerName, newLayer);

            string swapNote = achievedSwaps < targetSwaps
                ? $" ({achievedSwaps} of {targetSwaps} target swaps achieved before reaching the attempt limit)"
                : $" ({achievedSwaps} swaps)";
            return OperationResult.Ok($"Rewired layer '{layerName}' and stored it as new layer '{newLayerName}', all in network '{network.Name}'{swapNote}.");
        }

        public static OperationResult ProjectTwoModeToOneMode(Network network, string layerName, ProjectionMethod method, string newLayerName)
        {
            if (!network.Layers.ContainsKey(layerName))
                return OperationResult.Fail("LayerNotFound", $"Layer '{layerName}' does not exist in network '{network.Name}'.");
            if (!(network.Layers[layerName] is ILayerTwoMode originalLayer))
                return OperationResult.Fail("InvalidLayerType", $"Layer '{layerName}' is not a 2-mode layer.");
            if (network.Layers.ContainsKey(newLayerName))
                return OperationResult.Fail("LayerAlreadyExists", $"Layer '{newLayerName}' already exists in network '{network.Name}'.");

            Dictionary<(uint, uint), float> projectedEdges = [];
            if (method == ProjectionMethod.Count || method == ProjectionMethod.Newman)
                foreach ((string hypername, uint[] nodeIds) in originalLayer.GetAllHyperedgeData())
                    for (int i = 0; i < nodeIds.Length; i++)
                        for (int j = i + 1; j < nodeIds.Length; j++)
                        {
                            var key = (Math.Min(nodeIds[i], nodeIds[j]), Math.Max(nodeIds[i], nodeIds[j]));
                            projectedEdges[key] = projectedEdges.GetValueOrDefault(key) + 1f / (method == ProjectionMethod.Count ? 1f : (nodeIds.Length - 1));
                        }
            else if (method == ProjectionMethod.Binary)
                foreach ((string hypername, uint[] nodeIds) in originalLayer.GetAllHyperedgeData())
                    for (int i = 0; i < nodeIds.Length; i++)
                        for (int j = i + 1; j < nodeIds.Length; j++)
                        {
                            var key = (Math.Min(nodeIds[i], nodeIds[j]), Math.Max(nodeIds[i], nodeIds[j]));
                            projectedEdges.TryAdd(key, 1f);
                        }

            LayerOneMode newLayer = new LayerOneMode(newLayerName, EdgeDirectionality.Undirected, (method == ProjectionMethod.Binary) ? EdgeType.Binary : EdgeType.Valued, false);
            foreach (var ((node1, node2), value) in projectedEdges)
                newLayer._addEdge(node1, node2, value);
            network.AddLayer(newLayerName, newLayer);
            return OperationResult.Ok($"Projected layer '{layerName}' and stored it as new layer '{newLayerName}', all in network '{network.Name}'.");
        }


        /// <summary>
        /// Creates a new Network object based on the provided network that instead uses the provided Nodeset, thus
        /// removing all edges that are not related to any of the nodes in the provided nodeset.
        /// </summary>
        /// <param name="network">The original <see cref="Network"/> object.</param>
        /// <param name="nodeset">The <see cref="Nodeset"/> of the new <see cref="Network"/> object that is created.</param>
        /// <returns>A <see cref="Network"/> object that is a subset of the provided 'network'.</returns>
        public static OperationResult<Network> Subnet(Network network, Nodeset nodeset)
        {
            Network subnet = new Network(network.Name + "_subnet", nodeset);

            foreach (var (layerName, layer) in network.Layers)
            {
                subnet.AddLayer(layerName, layer.CreateFilteredCopy(nodeset));
            }
            return OperationResult<Network>.Ok(subnet);
        }
        #endregion


        #region Methods (private)
        /// <summary>
        /// Determines whether the specified value is approximately equal to 0 or 1.
        /// </summary>
        /// <remarks>The comparison uses a tolerance defined by the constant <c>epsilon</c> to account for
        /// floating-point precision errors.</remarks>
        /// <param name="value">The floating-point value to evaluate.</param>
        /// <returns><see langword="true"/> if the value is approximately equal to 0 or 1; otherwise, <see langword="false"/>.</returns>
        private static bool IsZeroOrOne(float value)
        {
            return Math.Abs(value - 0f) < epsilon || Math.Abs(value - 1f) < epsilon;
        }

        /// <summary>Canonicalizes an undirected pair so (x,y) and (y,x) hash to the same key.</summary>
        private static (uint, uint) Canon(uint x, uint y) => x <= y ? (x, y) : (y, x);

        /// <summary>
        /// Runs the '2edge' swap chain in place on 'edges': repeatedly picks two distinct edges
        /// (a,b) and (c,d) and reconnects them as (a,d) and (c,b), accepting the swap only if
        /// neither new pair already exists and (unless the layer allows selfties) neither is a
        /// self-loop. For symmetric layers, each edge's anchor side is re-randomized on every
        /// swap it participates in, since an undirected edge has no real tail/head to anchor a
        /// value to. Returns the number of successful swaps.
        /// </summary>
        private static int RunTwoEdgeSwaps(List<(uint From, uint To, float Weight)> edges, bool directed, bool selfties, int targetSwaps, int maxTries)
        {
            int n = edges.Count;
            var edgeSet = new HashSet<(uint, uint)>(n);
            foreach (var e in edges)
                edgeSet.Add(directed ? (e.From, e.To) : Canon(e.From, e.To));

            int successes = 0;
            for (int tries = 0; successes < targetSwaps && tries < maxTries; tries++)
            {
                int i = Misc.Random.Next(n);
                int j = Misc.Random.Next(n);
                if (i == j)
                    continue;

                var (a, b, w1) = edges[i];
                var (c, d, w2) = edges[j];

                if (!directed)
                {
                    // Random per-swap orientation: which endpoint anchors the value is re-chosen every time.
                    if (Misc.Random.Next(2) == 0) (a, b) = (b, a);
                    if (Misc.Random.Next(2) == 0) (c, d) = (d, c);
                }

                if (!selfties && (a == d || c == b))
                    continue;

                (uint, uint) newPair1 = directed ? (a, d) : Canon(a, d);
                (uint, uint) newPair2 = directed ? (c, b) : Canon(c, b);
                if (edgeSet.Contains(newPair1) || edgeSet.Contains(newPair2))
                    continue;

                edgeSet.Remove(directed ? (a, b) : Canon(a, b));
                edgeSet.Remove(directed ? (c, d) : Canon(c, d));
                edgeSet.Add(newPair1);
                edgeSet.Add(newPair2);
                edges[i] = (a, d, w1);
                edges[j] = (c, b, w2);
                successes++;
            }
            return successes;
        }

        /// <summary>
        /// Runs the '3edge' (networkx directed_edge_swap) chain in place on 'edges': repeatedly
        /// finds a random directed path a→b→c→d of 3 arcs over 4 distinct nodes among the
        /// currently rewired arcs, and reconnects it as a→c→b→d, accepting the swap only if none
        /// of the three new arcs already exist. Directed layers only; since it only ever operates
        /// on a path of 4 distinct nodes, it can never create or disturb a self-loop. Maintains a
        /// per-tail out-arc index (swap-with-last removal) so finding a path is not a rejection
        /// search over all arcs. Returns the number of successful swaps.
        /// </summary>
        private static int RunThreeEdgeSwaps(List<(uint From, uint To, float Weight)> edges, int targetSwaps, int maxTries)
        {
            int n = edges.Count;
            var edgeSet = new HashSet<(uint, uint)>(n);
            var outIndicesByTail = new Dictionary<uint, List<int>>();
            var positionInTailList = new int[n];
            for (int idx = 0; idx < n; idx++)
            {
                edgeSet.Add((edges[idx].From, edges[idx].To));
                if (!outIndicesByTail.TryGetValue(edges[idx].From, out var list))
                    outIndicesByTail[edges[idx].From] = list = [];
                positionInTailList[idx] = list.Count;
                list.Add(idx);
            }

            void RemoveFromTailIndex(uint tail, int idx)
            {
                var list = outIndicesByTail[tail];
                int pos = positionInTailList[idx];
                int lastIdx = list[^1];
                list[pos] = lastIdx;
                positionInTailList[lastIdx] = pos;
                list.RemoveAt(list.Count - 1);
            }
            void AddToTailIndex(uint tail, int idx)
            {
                if (!outIndicesByTail.TryGetValue(tail, out var list))
                    outIndicesByTail[tail] = list = [];
                positionInTailList[idx] = list.Count;
                list.Add(idx);
            }

            int successes = 0;
            for (int tries = 0; successes < targetSwaps && tries < maxTries; tries++)
            {
                int i = Misc.Random.Next(n);
                var (a, b, w1) = edges[i];

                if (!outIndicesByTail.TryGetValue(b, out var bOut) || bOut.Count == 0)
                    continue;
                int j = bOut[Misc.Random.Next(bOut.Count)];
                if (j == i)
                    continue;
                uint c = edges[j].To;
                float w2 = edges[j].Weight;

                if (!outIndicesByTail.TryGetValue(c, out var cOut) || cOut.Count == 0)
                    continue;
                int k = cOut[Misc.Random.Next(cOut.Count)];
                if (k == i || k == j)
                    continue;
                uint d = edges[k].To;
                float w3 = edges[k].Weight;

                // All 4 path nodes must be distinct: this also guarantees none of the 3 new arcs
                // below can ever be a self-loop, regardless of whether the layer allows selfties.
                if (a == b || a == c || a == d || b == c || b == d || c == d)
                    continue;

                if (edgeSet.Contains((a, c)) || edgeSet.Contains((c, b)) || edgeSet.Contains((b, d)))
                    continue;

                edgeSet.Remove((a, b));
                edgeSet.Remove((b, c));
                edgeSet.Remove((c, d));
                edgeSet.Add((a, c));
                edgeSet.Add((c, b));
                edgeSet.Add((b, d));

                // Tails: edges[i] keeps tail a; edges[j]'s tail moves b->c; edges[k]'s tail moves c->b.
                RemoveFromTailIndex(b, j);
                AddToTailIndex(c, j);
                RemoveFromTailIndex(c, k);
                AddToTailIndex(b, k);

                edges[i] = (a, c, w1);
                edges[j] = (c, b, w2);
                edges[k] = (b, d, w3);
                successes++;
            }
            return successes;
        }
        #endregion
    }
}