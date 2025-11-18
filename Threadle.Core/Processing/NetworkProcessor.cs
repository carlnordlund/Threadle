using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
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
            if (!(network.Layers[layerName] is LayerOneMode originalLayer))
                return OperationResult.Fail("InvalidLayerType", $"Layer '{layerName}' is not a 1-mode layer.");
            if (originalLayer.IsBinary)
                return OperationResult.Fail("LayerAlreadyBinary", $"Layer '{layerName}' is already binary, dichotomization is not applicable.");
            if (conditionType == ConditionType.isnull || conditionType == ConditionType.notnull)
                return OperationResult.Fail("InvalidCondition", $"Condition '{conditionType}' is invalid for dichotomization.");
            if (network.Layers.ContainsKey(newLayerName))
                return OperationResult.Fail("LayerAlreadyExists", $"Layer '{newLayerName}' already exists in network '{network.Name}'.");
            EdgeType newEdgeType = (IsZeroOrOne(trueValue) && IsZeroOrOne(falseValue)) ? EdgeType.Binary : EdgeType.Valued;

            LayerOneMode newLayer = new LayerOneMode(newLayerName, originalLayer.Directionality, newEdgeType, originalLayer.Selfties);

            foreach ((uint nodeId, IEdgeset edgeset) in originalLayer.Edgesets)
            {
                if (!(edgeset is IEdgesetValued edgesetValued))
                    return OperationResult.Fail("InvalidEdgesetType", $"Edgeset for node '{nodeId}' in layer '{layerName}' is not a valued edgeset.");
                foreach (var connection in edgesetValued.GetOutboundConnections)
                {
                    if (Misc.CompareValues<float>(connection.value, threshold, conditionType))
                    {
                        float valueToAssign = float.IsNaN(trueValue) ? connection.value : trueValue;
                        newLayer.AddEdge(nodeId, connection.partnerNodeId, valueToAssign);
                    }
                    else
                    {
                        float valueToAssign = float.IsNaN(falseValue) ? connection.value : falseValue;
                        if (!IsZeroOrOne(valueToAssign) || valueToAssign != 0f)
                            newLayer.AddEdge(nodeId, connection.partnerNodeId, valueToAssign);
                    }
                }
            }
            network.AddLayer(newLayerName, newLayer);
            return OperationResult.Ok($"Dichotomized layer '{layerName}' and stored it as new layer '{newLayerName}', all in network '{network.Name}'.");
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
