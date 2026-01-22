using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;
using System.Runtime.InteropServices.Marshalling;

namespace Threadle.Core.Analysis
{
    /// <summary>
    /// A collection of public-facing (and fairly simple) network analysis functions accessible by the frontend.
    /// </summary>
    public static class Analyses
    {
        #region Methods (public)
        /// <summary>
        /// Generates summary info about an attribute in a nodeset. The specific info that is returned depends on the type
        /// of node attribute.
        /// </summary>
        /// <param name="nodeset">The Nodeset structure.</param>
        /// <param name="attrName">The attribute name.</param>
        /// <returns>An <see cref="OperationResult"/> with a potentially nested string-object dictionary with summary statistics about the attribute.</returns>
        public static OperationResult<Dictionary<string,object>> GetAttributeSummary(Nodeset nodeset, string attrName)
        {
            if (!nodeset.NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
                return OperationResult<Dictionary<string, object>>.Fail("AttributeNotFound", $"Attribute '{attrName}' not found in nodeset '{nodeset.Name}'.");
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
                    stats = Functions.CalculateIntStatistics(nodeset, attrIndex,  out countWithValues);
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
            }

            stats["Count"] = countWithValues;
            stats["Missing"] = totalNodes - countWithValues;
            stats["PercentageWithValue"] = totalNodes > 0 ? (double)countWithValues / totalNodes * 100.0 : 0.0;
            results["Statistics"] = stats;
            return OperationResult<Dictionary<string, object>>.Ok(results);
        }

        /// <summary>
        /// Calculates the shortest path between two nodes, either for a particular layer or for all layers.
        /// To work with all layers, set layerName to an empty string. Note that the shortest path takes edge
        /// directionality into account: if a layer has directional edges, this matters. For layers that are symmetric,
        /// the directionality is moot.
        /// If there is no path between the nodes, a distance of -1 is returned: note that this is also
        /// wrapped in a OperationResult.Success.
        /// </summary>
        /// <param name="network">The network.</param>
        /// <param name="layerName">The name of the layer (or an empty/null string if all layers should be used)</param>
        /// <param name="nodeIdFrom">The source node id.</param>
        /// <param name="nodeIdTo">The destination node id.</param>
        /// <returns>An OperationResult containing the shortest path (integer) if successful; otherwise, an error message.</returns>
        public static OperationResult<int> ShortestPath(Network network, string? layerName, uint nodeIdFrom, uint nodeIdTo)
        {
            OperationResult nodeCheckResult = network.Nodeset.CheckThatNodesExist(nodeIdFrom, nodeIdTo);
            if (!nodeCheckResult.Success)
                return OperationResult<int>.Fail(nodeCheckResult.Code, nodeCheckResult.Message);
            if (nodeIdFrom == nodeIdTo)
                return OperationResult<int>.Ok(0);
            else
            {
                Queue<uint> queue = [];
                HashSet<uint> visited = [];
                Dictionary<uint,int> distances = [];
                uint current;

                if (layerName != null && layerName.Length > 0)
                {
                    var layerResult = network.GetLayer(layerName);
                    if (!layerResult.Success)
                        return OperationResult<int>.Fail(layerResult);
                    var layer = layerResult.Value!;

                    queue.Enqueue(nodeIdFrom);
                    visited.Add(nodeIdFrom);
                    distances[nodeIdFrom] = 0;

                    while (queue.Count > 0)
                    {
                        current = queue.Dequeue();
                        foreach (uint neighborId in layer.GetNodeAlters(current,EdgeTraversal.Out))
                        {
                            if (!visited.Contains(neighborId))
                            {
                                visited.Add(neighborId);
                                distances[neighborId] = distances[current] + 1;
                                if (neighborId == nodeIdTo)
                                    return OperationResult<int>.Ok(distances[neighborId]);
                                queue.Enqueue(neighborId);
                            }
                        }
                    }
                    return OperationResult<int>.Ok(-1);
                }
                else
                {
                    queue.Enqueue(nodeIdFrom);
                    visited.Add(nodeIdFrom);
                    distances[nodeIdFrom] = 0;
                    while (queue.Count>0)
                    {
                        current = queue.Dequeue();
                        foreach (uint neighborId in network._getNodeAltersAllLayers(current, EdgeTraversal.Out))
                        {
                            if (!visited.Contains(neighborId))
                            {
                                visited.Add(neighborId);
                                distances[neighborId] = distances[current] + 1;
                                if (neighborId == nodeIdTo)
                                    return OperationResult<int>.Ok(distances[neighborId]);
                                queue.Enqueue(neighborId);
                            }
                        }
                    }
                    return OperationResult<int>.Ok(-1);
                }
            }
        }

        /// <summary>
        /// Calculates the density of the specified layer in the network. Can be 1-mode or 2-mode.
        /// Using different methods for 1- resp 2-mode layers.
        /// </summary>
        /// <param name="network">The network containing the layer.</param>
        /// <param name="layerName">The name of the layer.</param>
        /// <returns>An OperationResult containing the density value if successful; otherwise, an error message.</returns>
        public static OperationResult<double> Density(Network network, string layerName)
        {
            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return OperationResult<double>.Fail(layerResult);
            if (layerResult.Value is LayerOneMode layerOneMode)
                return OperationResult<double>.Ok(Functions.Density(network, layerOneMode));
            if (layerResult.Value is LayerTwoMode layerTwoMode)
                return OperationResult<double>.Ok(Functions.Density(network, layerTwoMode));
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
        public static OperationResult<Dictionary<string,object>> ConnectedComponents(Network network, string layerName, string? attrName = null)
        {
            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return OperationResult<Dictionary<string,object>>.Fail(layerResult);
            ILayer layer = layerResult.Value!;
            Dictionary<uint, int> componentIndexMapping = Functions.ConnectedComponents(network, layer);
            var attrDict = componentIndexMapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
            attrName = (attrName != null && attrName.Length > 0) ? attrName : layerName + "_componentIndex";
            var setAttrResult = network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Int);
            if (!setAttrResult.Success)
                return OperationResult<Dictionary<string,object>>.Fail(setAttrResult);
            
            Dictionary<string, object> componentInfo = [];
            int nbrComponents = componentIndexMapping.Values.Max() + 1;
            int[] componentSizes = new int[nbrComponents];
            foreach (int compId in componentIndexMapping.Values)
                componentSizes[compId]++;
            componentInfo["NbrComponents"] = nbrComponents;
            componentInfo["ComponentSizes"] = componentSizes.OrderByDescending(c => c).ToList();
            return OperationResult<Dictionary<string,object>>.Ok(componentInfo);

            //return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Int);
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
                return OperationResult<string>.Fail(layerResult);
            ILayer layer = layerResult.Value!;
            Dictionary<uint, uint> degreeMapping = [];
            if (layer is LayerOneMode layerOneMode)
            {
                string dirString = layerOneMode switch
                {
                    LayerOneMode { IsSymmetric: true } => "degree",
                    LayerOneMode { IsSymmetric: false } => edgeTraversal switch
                    {
                        EdgeTraversal.Out => "outdegree",
                        EdgeTraversal.In => "indegree",
                        _ => "grossdegree"
                    },
                    _ => "degree"
                };
                attrName = (attrName != null && attrName.Length > 0) ? attrName : layerName + "_" + dirString;
                degreeMapping = Functions.DegreeCentrality(network, layerOneMode, edgeTraversal);
            }
            else if (layer is LayerTwoMode layerTwoMode)
            {
                attrName = (attrName != null && attrName.Length > 0) ? attrName : layerName + "_degree";
                degreeMapping = Functions.DegreeCentrality(network, layerTwoMode);
            }
            else
                return OperationResult.Fail("InvalidLayerType", $"Layer '{layerName}' is neither a one-mode nor a two-mode layer.");
            // Convert the degree mapping into the <uint,string> format and store this as a node attribute
            var attrDict = degreeMapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Float);
        }

        /// <summary>
        /// Given an ego node, this method returns a random alter of this node, either for a specific layer or all layers.
        /// If no layer is specified, i.e. so that all layers are used, this pick can either be done balanced (first picking
        /// a random layer, and subsequently picking an alter from one of these layers) or non-balanced (pooling together all
        /// alters in all layers and then picking one from this set). For the latter, it is thus possible that an alter could
        /// appear more than once in the choice set.
        /// For directed layers, it is also possible to specify whether outbound, inbound or both-directional alters should be included.
        /// </summary>
        /// <param name="network">The Network object.</param>
        /// <param name="nodeid">The ego node id.</param>
        /// <param name="layerName">The name of the layer to pick from. If left blank or null, all layers are used.</param>
        /// <param name="edgeTraversal">An <see cref="EdgeTraversal"/> value indicating whether inbound- or outbound-going edges (or both) should be considered.</param>
        /// <param name="balanced">Indicates whether the pick should be balanced across layers.</param>
        /// <returns>An <see cref="OperationResult{T}"/> containing the random alter node id if successful; otherwise, an error message.</returns>
        public static OperationResult<uint> GetRandomAlter(Network network, uint nodeid, string layerName, EdgeTraversal edgeTraversal = EdgeTraversal.Both, bool balanced = false)
        {
            List<uint> alterIds = [];
            if (layerName != null && layerName.Length > 0)
            {
                // Only use the specified layer
                var layerResult = network.GetLayer(layerName);
                if (!layerResult.Success)
                    return OperationResult<uint>.Fail(layerResult);
                var layer = layerResult.Value!;
                var altersResult = network.GetNodeAlters(layer.Name, nodeid, edgeTraversal);
                if (!altersResult.Success)
                    return OperationResult<uint>.Fail(altersResult);
                alterIds.AddRange(altersResult.Value!);
            }
            else
            {
                if (balanced)
                {
                    List<uint[]> layerAlterslist = [];

                    foreach (var layer in network.Layers.Values)
                    {
                        uint[] alters = layer.GetNodeAlters(nodeid, edgeTraversal);
                        if (alters.Length > 0)
                            layerAlterslist.Add(alters);
                    }
                    if (layerAlterslist.Count == 0)
                        return OperationResult<uint>.Fail("ConstraintNoAlters", $"Node {nodeid} has no alters in any layer with the given edge traversal.");
                    var chosenLayerAlters = layerAlterslist[Misc.Random.Next(layerAlterslist.Count)];
                    alterIds.AddRange(chosenLayerAlters);
                }
                else
                {
                    foreach (var layer in network.Layers.Values)
                    {
                        uint[] alters = layer.GetNodeAlters(nodeid, edgeTraversal);
                        alterIds.AddRange(alters);
                    }
                }
            }
            if (alterIds.Count == 0)
                return OperationResult<uint>.Fail("ConstraintNoAlters", $"Node {nodeid} has no alters in the specified layer(s) with the given edge traversal.");
            uint randomAlterId = alterIds[Misc.Random.Next(alterIds.Count)];
            return OperationResult<uint>.Ok(randomAlterId);
        }

        /// <summary>
        /// Selects a random node identifier from the specified nodeset.
        /// </summary>
        /// <param name="nodeset">The nodeset from which to select a random node. Must contain at least one node.</param>
        /// <returns>An <see cref="OperationResult{T}"/> containing the identifier of the randomly selected node uf syccessful, otherwise, an error message.</returns>
        public static OperationResult<uint> GetRandomNode(Nodeset nodeset)
        {
            if (nodeset.Count == 0)
                return OperationResult<uint>.Fail("ConstraintNoNodes", $"Nodeset '{nodeset.Name}' contains no nodes.");
            uint randomNodeId = nodeset.NodeIdArray[Misc.Random.Next(nodeset.Count)];
            return OperationResult<uint>.Ok(randomNodeId);
        }

        #endregion
    }
}
