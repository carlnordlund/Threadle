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
            Dictionary<uint, uint> grossDegreeCentrality = [];
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
            Dictionary<uint, uint> inDegreeCentrality = [];
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                inDegreeCentrality[nodeId] = layerOneMode.GetInDegree(nodeId);
            return inDegreeCentrality;
        }

        private static Dictionary<uint, uint> OutDegreeCentrality(Network network, LayerOneMode layerOneMode)
        {
            Dictionary<uint, uint> outDegreeCentrality = [];
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                outDegreeCentrality[nodeId] = layerOneMode.GetOutDegree(nodeId);
            return outDegreeCentrality;
        }

        internal static Dictionary<uint, uint> DegreeCentrality(Network network, LayerTwoMode layerTwoMode)
        {
            // For 2-mode layers, degree centrality is the union of nodes a node is connected to through its hyperedges
            // For this, I collect their alterId arrays and count their lenghts
            Dictionary<uint, uint> degreeCentrality = [];
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                degreeCentrality[nodeId] = (uint)layerTwoMode.GetAlterIds(nodeId).Length;
            return degreeCentrality;
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
