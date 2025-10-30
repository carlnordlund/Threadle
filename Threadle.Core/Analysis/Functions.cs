using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Analysis
{
    internal static class Functions
    {
        internal static Dictionary<uint, uint> DegreeCentrality(Network network, LayerOneMode layerOneMode, EdgeTraversal edgeTraversal)
        {
            Dictionary<uint, uint> degreeCentrality = edgeTraversal switch
            {
                EdgeTraversal.Out => OutDegreeCentrality(network, layerOneMode),
                EdgeTraversal.In => InDegreeCentrality(network, layerOneMode),
                _ => GrossDegreeCentrality(network, layerOneMode)
            };
            return degreeCentrality;
        }

        internal static double Density(Network network, LayerOneMode layer)
        {
            ulong nbrPotentialEdges = GetNbrPotentialEdges((ulong)network.Nodeset.Count, layer.IsDirectional, layer.Selfties);
            ulong nbrExistingEdges = layer.NbrEdges;
            return (double)nbrExistingEdges / nbrPotentialEdges;
        }


        private static Dictionary<uint, uint> GrossDegreeCentrality(Network network, LayerOneMode layerOneMode)
        {
            Dictionary<uint, uint> grossDegreeCentrality = new();
            if (layerOneMode.IsSymmetric)
                foreach (var nodeId in network.Nodeset.NodeIdArray)
                    grossDegreeCentrality[nodeId] = layerOneMode.GetInDegree(nodeId);
            else
                foreach (var nodeId in network.Nodeset.NodeIdArray)
                    grossDegreeCentrality[nodeId] = layerOneMode.GetInDegree(nodeId) + layerOneMode.GetOutDegree(nodeId);
            return grossDegreeCentrality;
        }

        private static Dictionary<uint, uint> InDegreeCentrality(Network network, LayerOneMode layerOneMode)
        {
            Dictionary<uint, uint> inDegreeCentrality = new();
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                inDegreeCentrality[nodeId] = layerOneMode.GetInDegree(nodeId);
            return inDegreeCentrality;
        }

        private static Dictionary<uint, uint> OutDegreeCentrality(Network network, LayerOneMode layerOneMode)
        {
            Dictionary<uint, uint> outDegreeCentrality = new();
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                outDegreeCentrality[nodeId] = layerOneMode.GetOutDegree(nodeId);
            return outDegreeCentrality;
        }

        private static ulong GetNbrPotentialEdges(ulong n, bool isDirectional, bool selfties)
        {
            if (isDirectional)
                return selfties ? n * n : n * (n - 1);
            else
                return selfties ? n * (n + 1) / 2 : n * (n - 1) / 2;
        }
    }
}
