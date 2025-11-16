using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Analysis
{
    /// <summary>
    /// A collection of internal functions useful for the methods in Analyses.cs
    /// </summary>
    internal static class Functions
    {
        #region Methods (internal)
        /// <summary>
        /// Calculates node degree centrality measures of a specific 1-mode layer in a specific network.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layerOneMode">The 1-mode layer object.</param>
        /// <param name="edgeTraversal">A <see cref="EdgeTraversal"/> value specifying whether indegree, outdegree or grossdegree should be calculated.</param>
        /// <returns>Returns a dictionary with degree centrality measures by node id.</returns>
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

        /// <summary>
        /// Calculates the degree centrality for a specific network and specific 2-mode layer.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layerOneMode">The 2-mode layer object.</param>
        /// <returns>Returns a dictionary with outdegree centrality measures by node id.</returns>
        internal static Dictionary<uint, uint> DegreeCentrality(Network network, LayerTwoMode layerTwoMode)
        {
            Dictionary<uint, uint> degreeCentrality = [];
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                degreeCentrality[nodeId] = (uint)layerTwoMode.GetAlterIds(nodeId).Length;
            return degreeCentrality;
        }

        /// <summary>
        /// Calculates the density of a specific 1-mode layer in a specific network.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layer">The 1-mode layer object.</param>
        /// <returns>Density (as a double value).</returns>
        internal static double Density(Network network, LayerOneMode layer)
        {
            ulong nbrPotentialEdges = Misc.GetNbrPotentialEdges((ulong)network.Nodeset.Count, layer.Directionality, layer.Selfties);
            ulong nbrExistingEdges = layer.NbrEdges;
            return (double)nbrExistingEdges / nbrPotentialEdges;
        }
        #endregion


        #region Methods (private)
        /// <summary>
        /// Calculates the gross degree centrality for a specific network and specific 1-mode layer.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layerOneMode">The 1-mode layer object.</param>
        /// <returns>Returns a dictionary with gross degree centrality measures by node id.</returns>
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

        /// <summary>
        /// Calculates the indegree centrality for a specific network and specific 1-mode layer.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layerOneMode">The 1-mode layer object.</param>
        /// <returns>Returns a dictionary with indegree centrality measures by node id.</returns>
        private static Dictionary<uint, uint> InDegreeCentrality(Network network, LayerOneMode layerOneMode)
        {
            Dictionary<uint, uint> inDegreeCentrality = [];
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                inDegreeCentrality[nodeId] = layerOneMode.GetInDegree(nodeId);
            return inDegreeCentrality;
        }

        /// <summary>
        /// Calculates the outdegree centrality for a specific network and specific 1-mode layer.
        /// </summary>
        /// <param name="network">The Network structure.</param>
        /// <param name="layerOneMode">The 1-mode layer object.</param>
        /// <returns>Returns a dictionary with outdegree centrality measures by node id.</returns>
        private static Dictionary<uint, uint> OutDegreeCentrality(Network network, LayerOneMode layerOneMode)
        {
            Dictionary<uint, uint> outDegreeCentrality = [];
            foreach (var nodeId in network.Nodeset.NodeIdArray)
                outDegreeCentrality[nodeId] = layerOneMode.GetOutDegree(nodeId);
            return outDegreeCentrality;
        }
        #endregion
    }
}
