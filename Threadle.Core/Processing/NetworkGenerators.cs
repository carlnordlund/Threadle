using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Processing
{
    /// <summary>
    /// Various network generator methods.
    /// </summary>
    public static class NetworkGenerators
    {
        #region Methods (public)

        /// <summary>
        /// Generates an Erdös-Renyi network in the provided network and layer name. The layer must alread exist and be binary.
        /// Any would-be existing ties in that layer will first be deleted. The generator will then follow the properties of the
        /// layer, i.e. whether directional or symmetric, whether selfties or not.
        /// This approach utilizes the very clever solution proposed by Batagelj and Brandes (2005)
        /// in https://doi.org/10.1103/PhysRevE.71.036113
        /// </summary>
        /// <param name="network">The network to generate this data in,</param>
        /// <param name="layerName">The name of the layer to create it in.</param>
        /// <param name="p">The tie probability.</param>
        /// <returns></returns>
        public static OperationResult ErdosRenyiLayer(Network network, string layerName, double p)
        {
            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return layerResult;
            if (!(layerResult.Value is LayerOneMode layer))
                return OperationResult.Fail("LayerNotOneMode", $"Layer '{layerName}' in network '{network.Name}' is not 1-mode.");
            if (!layer.IsBinary)
                return OperationResult.Fail("LayerNotBinary", $"Layer '{layerName}' in network '{network.Name}' is not for binary edges.");

            // Clear the layer
            layer.ClearLayer();

            // Get reference to nodeset
            Nodeset nodeset = network.Nodeset;
            uint n = (uint)nodeset.Count;

            // Determine the expected degree (i.e. number of directed connections per node)
            int edgesetCapacity = (int)(p * n);

            // Get an array of all node ids
            uint[] nodeIds = nodeset.NodeIdArray;

            // Initialize the capacity of the Edgesets dictionary as well as the number of connections for each IEdgeset
            layer._initEdgesets(nodeIds, edgesetCapacity);

            ulong totalEdges = Misc.GetNbrPotentialEdges(n, layer.Directionality, layer.Selfties);
            ulong index = 0;
            uint row = 0;
            ulong rowStartindex = 0;
            uint rowLength = GetRowLength(row, n, layer.Directionality, layer.Selfties);
            while (index < totalEdges)
            {
                ulong skip = Misc.SampleGeometric(p);
                index += skip;
                if (index >= totalEdges)
                    break;
                while (index >= rowStartindex + rowLength)
                {
                    rowStartindex += rowLength;
                    row++;
                    rowLength = GetRowLength(row, n, layer.Directionality, layer.Selfties);
                }
                uint offsetInRow = (uint)(index - rowStartindex);
                uint col = GetCol(row, offsetInRow, n, layer.Directionality, layer.Selfties);

                // Bypass validation when creating edges
                layer.Edgesets[nodeIds[row]]._addOutboundEdge(nodeIds[col], 1);
                layer.Edgesets[nodeIds[col]]._addInboundEdge(nodeIds[row], 1);
                index++;
            }
            return OperationResult.Ok($"Erdös-Renyi network with p={p} generated in layer '{layerName}' in network '{network.Name}'.");
        }
        #endregion


        #region Methods (private)
        /// <summary>
        /// Calculates the number of elements in a row based on the specified parameters.
        /// </summary>
        /// <param name="row">The zero-based index of the current row.</param>
        /// <param name="n">The total number of elements in the structure.</param>
        /// <param name="directionality">The directionality of the edges, indicating whether they are directed or undirected.</param>
        /// <param name="selfties">A value indicating whether self-ties (connections to the same element) are allowed.</param>
        /// <returns>The number of elements in the specified row, adjusted based on the directionality and self-ties settings.</returns>
        private static uint GetRowLength(uint row, uint n, EdgeDirectionality directionality, bool selfties)
        {
            if (directionality == EdgeDirectionality.Directed)
                return selfties ? n : n - 1;
            else
                return selfties ? n - row : n - row - 1;
        }

        /// <summary>
        /// Calculates the column index in a graph based on the specified row, offset, graph size, edge directionality,
        /// and whether self-loops are allowed.
        /// </summary>
        /// <param name="row">The zero-based index of the row in the graph.</param>
        /// <param name="offsetInRow">The zero-based offset within the specified row.</param>
        /// <param name="n">The total number of nodeIds in the graph. This parameter is not used in the calculation.</param>
        /// <param name="directionality">Specifies whether the graph is directed or undirected. If directed, the calculation accounts for the
        /// direction of edges.</param>
        /// <param name="selfties">A value indicating whether self-loops (edges from a node to itself) are allowed. If <see langword="true"/>,
        /// self-loops are included in the calculation.</param>
        /// <returns>The calculated column index as a zero-based unsigned integer.</returns>
        private static uint GetCol(uint row, uint offsetInRow, long n, EdgeDirectionality directionality, bool selfties)
        {
            if (directionality== EdgeDirectionality.Directed)
            {
                if (selfties)
                    return offsetInRow;
                else
                    return offsetInRow >= row ? offsetInRow + 1 : offsetInRow;
            }
            else
                return (uint)(row + offsetInRow + (selfties ? 0 : 1));
        }

        #endregion
    }
}
