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
        #endregion
    }
}
