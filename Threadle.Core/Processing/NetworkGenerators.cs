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
    /// <summary>
    /// Various network generator methods.
    /// </summary>
    public static class NetworkGenerators
    {
        #region Methods (public)
        /// <summary>
        /// Generates a random network using the Erdős–Rényi model. This approach utilizes the very clever solution
        /// proposed by Batagelj and Brandes (2005) in https://doi.org/10.1103/PhysRevE.71.036113
        /// </summary>
        /// <remarks>The Erdős–Rényi model generates a network by iterating over all possible edges
        /// between nodes and including each edge with a probability of <paramref name="p"/>. The resulting network
        /// structure depends on the specified edge directionality and whether self-loops are allowed.</remarks>
        /// <param name="size">The number of nodes in the network. Must be greater than zero.</param>
        /// <param name="p">The probability of an edge existing between any two nodes. Must be in the range [0, 1].</param>
        /// <param name="directionality">Specifies whether the edges in the network are directed or undirected.</param>
        /// <param name="selfties">Indicates whether self-loops (edges from a node to itself) are allowed in the network.</param>
        /// <returns>A <see cref="Network"/> object representing the generated Erdős–Rényi random network.</returns>
        public static StructureResult ErdosRenyi(int size, double p, EdgeDirectionality directionality, bool selfties)
        {
            Nodeset nodeset = new Nodeset("er_nodes", size);
            Network network = new Network("er", nodeset);
            string layername = $"er_{p}";
            network.AddLayerOneMode(layername, directionality, EdgeType.Binary, selfties);
            uint[] nodes = nodeset.NodeIdArray;
            uint n = (uint)nodeset.Count;
            ulong totalEdges = Misc.GetNbrPotentialEdges(n, directionality, selfties);
            ulong index = 0;
            uint row = 0;
            ulong rowStartindex = 0;
            uint rowLength = GetRowLength(row, n, directionality, selfties);
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
                    rowLength = GetRowLength(row, n, directionality, selfties);
                }
                uint offsetInRow = (uint)(index - rowStartindex);
                uint col = GetCol(row, offsetInRow, n, directionality, selfties);
                network.AddEdge(layername, nodes[row], nodes[col]);
                index++;
            }
            return new StructureResult(network, new Dictionary<string, IStructure>
            {
                { "nodeset", nodeset }
            });
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
        /// <param name="n">The total number of nodes in the graph. This parameter is not used in the calculation.</param>
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
