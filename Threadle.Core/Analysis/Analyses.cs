using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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


            //bool directed = layerOneMode.IsDirectional;
            //bool selfties = layerOneMode.Selfties;
            //ulong n = (ulong)nodeset.Count;

            //ulong nbrPotentialEdges = Misc.GetNbrPotentialEdges(n, directed, selfties);
            ////ulong nbrOfExistingOutgoingEdges = Misc.GetTotalNbrOutgoingEdges(layerOneMode);
            //ulong nbrOfExistingOutgoingEdges = layerOneMode.NbrEdges;
            ////return (double)nbrOfExistingOutgoingEdges / nbrPotentialEdges;
            //return 

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
