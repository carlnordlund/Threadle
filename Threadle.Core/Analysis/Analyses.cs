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
    public static class Analyses
    {
        public static OperationResult<double> Density(Network network, string layerName)
        {
            var layerResult = network.GetOneModeLayer(layerName);
            if (!layerResult.Success)
                return OperationResult<double>.Fail(layerResult);

            double density = Functions.Density(network, layerResult.Value!);

            return OperationResult<double>.Ok(density);
        }

        public static OperationResult DegreeCentrality(Network network, string layerName, string? attrName=null, EdgeTraversal edgeTraversal = EdgeTraversal.Out)
        {
            var layerResult = network.GetOneModeLayer(layerName);
            if (!layerResult.Success)
                return OperationResult<string>.Fail(layerResult);
            LayerOneMode layerOneMode = layerResult.Value!;
            string dirString = layerOneMode.IsSymmetric ? "degree" : edgeTraversal == EdgeTraversal.Out ? "outdegree" : edgeTraversal == EdgeTraversal.In ? "indegree" : "grossdegree";
            attrName = (attrName != null && attrName.Length > 0) ? attrName : layerName + "_" + dirString;
            var degreeMapping = Functions.DegreeCentrality(network, layerResult.Value!, edgeTraversal);
            var attrDict = degreeMapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
            return network.Nodeset.DefineAndSetNodeAttributeValues(attrName, attrDict, NodeAttributeType.Float);
        }



        //public static MatrixStructure? ShortestPaths(Network network, string layerName)
        //{
        //    if (!network.Layers.ContainsKey(layerName))
        //        throw new Exception($"Error: Network layer '{layerName}' not found in network {network.Name}.");
        //    if (!(network.Layers[layerName] is LayerOneMode layerOneMode))
        //        throw new Exception($"Error: Network layer '{layerName}' must be 1-mode.");
        //    bool isSymmetric = layerOneMode.IsSymmetric;

        //    MatrixStructure shortestpaths = new MatrixStructure("shortestpaths", network.Nodeset, isSymmetric);
        //    int size = network.Nodeset.Count;

        //    // Always use FW for both binary and valued
        //    if (layerOneMode.IsBinary || layerOneMode.IsValued)
        //    {
        //        foreach (Node fromNode in network.Nodeset.Nodes)
        //            foreach (Node toNode in network.Nodeset.Nodes)
        //                shortestpaths.Set(fromNode.Id, toNode.Id, (fromNode == toNode) ? 0 : (network.GetEdgeValue(layerName, fromNode, toNode) == 0) ? double.PositiveInfinity : 1);
        //        foreach (Node kNode in network.Nodeset.Nodes)
        //        {
        //            foreach (Node fromNode in network.Nodeset.Nodes)
        //                foreach (Node toNode in network.Nodeset.Nodes)
        //                    shortestpaths.Set(
        //                        fromNode.Id,
        //                        toNode.Id,
        //                        Math.Min(
        //                            shortestpaths.Get(fromNode.Id, toNode.Id),
        //                            shortestpaths.Get(fromNode.Id, kNode.Id) + shortestpaths.Get(kNode.Id, toNode.Id)
        //                            ));
        //        }
        //        return shortestpaths;
        //    }
        //    else
        //        throw new Exception($"Error: Shortest path not implemented for signed networks yet.");
        //}
    }
}
