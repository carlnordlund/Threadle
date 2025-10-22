using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Processing
{
    public static class NetworkProcessor
    {
        const float epsilon = 1e-6f;
        internal static bool IsZeroOrOne(float value)
        {
            return Math.Abs(value - 0f) < epsilon || Math.Abs(value - 1f) < epsilon;
        }

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

            foreach (var kvp in originalLayer.Edgesets)
            {
                if (!(kvp.Value is IEdgesetValued edgesetValued))
                    return OperationResult.Fail("InvalidEdgesetType", $"Edgeset for node '{kvp.Key}' in layer '{layerName}' is not a valued edgeset.");


                uint node1Id = kvp.Key;

                //List<Connection> outboundConnections = (edgeset is EdgesetValuedDirectional dirValEdgeset) ? dirValEdgeset.GetOutboundConnections : (edgeset is EdgesetValuedSymmetric symValEdgeset) ? symValEdgeset.GetOutboundConnections : new List<Connection>();
                
                List<Connection> outboundConnections = edgesetValued.GetOutboundConnections;

                foreach (var connection in outboundConnections)
                {
                    if (Misc.CompareValues<float>(connection.value, threshold, conditionType))
                    {
                        float valueToAssign = float.IsNaN(trueValue) ? connection.value : trueValue;
                        newLayer.AddEdge(node1Id, connection.partnerNodeId, valueToAssign);
                    }
                    else
                    {
                        float valueToAssign = float.IsNaN(falseValue) ? connection.value : falseValue;
                        if (!IsZeroOrOne(valueToAssign) || valueToAssign != 0f) // Assuming 0 represents no tie
                            newLayer.AddEdge(node1Id, connection.partnerNodeId, valueToAssign);
                    }
                }

            }

            network.Layers.Add(newLayerName, newLayer);
            return OperationResult.Ok("Dichotomization completed successfully.");
        }
    }
}
