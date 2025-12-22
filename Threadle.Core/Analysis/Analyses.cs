using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Analysis
{
    /// <summary>
    /// A collection of public-facing (and fairly simple) network analysis functions accessible by the frontend.
    /// </summary>
    public static class Analyses
    {
        #region Methods (public)
        /// <summary>
        /// Calculates the density of the specified one-mode layer in the network.
        /// </summary>
        /// <param name="network">The network containing the layer.</param>
        /// <param name="layerName">The name of the one-mode layer.</param>
        /// <returns>An OperationResult containing the density value if successful; otherwise, an error message.</returns>
        public static OperationResult<double> Density(Network network, string layerName)
        {
            var layerResult = network.GetOneModeLayer(layerName);
            if (!layerResult.Success)
                return OperationResult<double>.Fail(layerResult);

            double density = Functions.Density(network, layerResult.Value!);

            return OperationResult<double>.Ok(density);
        }

        /// <summary>
        /// Calculates the degree centrality for each node in the specified one- or two-mode layer and stores the results
        /// as a node attribute in the network's nodeset.
        /// </summary>
        /// <param name="network">The network containing the layer.</param>
        /// <param name="layerName"></param>
        /// <param name="attrName"></param>
        /// <param name="edgeTraversal"></param>
        /// <returns><see cref="OperationResult"/> object informing how well it went.</returns>
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
                        var altersResult = network.GetNodeAlters(layer.Name, nodeid, edgeTraversal);
                        if (!altersResult.Success)
                            return OperationResult<uint>.Fail(altersResult);

                        var alters = altersResult.Value!;
                        if (alters.Length > 0)
                            layerAlterslist.Add(alters);
                    }
                    if (layerAlterslist.Count == 0)
                        return OperationResult<uint>.Fail("NoAlters", $"Node {nodeid} has no alters in any layer with the given edge traversal.");
                    var chosenLayerAlters = layerAlterslist[Misc.Random.Next(layerAlterslist.Count)];
                    alterIds.AddRange(chosenLayerAlters);
                }
                else
                {
                    foreach (var layer in network.Layers.Values)
                    {
                        var altersResult = network.GetNodeAlters(layer.Name, nodeid, edgeTraversal);
                        if (!altersResult.Success)
                            return OperationResult<uint>.Fail(altersResult);
                        alterIds.AddRange(altersResult.Value!);
                    }
                }
            }
            if (alterIds.Count == 0)
                return OperationResult<uint>.Fail("NoAlters", $"Node {nodeid} has no alters in the specified layer(s) with the given edge traversal.");
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
                return OperationResult<uint>.Fail("NoNodes", $"Nodeset '{nodeset.Name}' contains no nodes.");
            uint randomNodeId = nodeset.NodeIdArray[Misc.Random.Next(nodeset.Count)];
            return OperationResult<uint>.Ok(randomNodeId);
        }
        #endregion
    }
}
