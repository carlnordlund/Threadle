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
    /// Static class for various network generation methods.
    /// </summary>
    public static class NetworkGenerators
    {

        public static Network ErdosRenyi(int size, double p, EdgeDirectionality directionality, bool selfties)
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
            return network;
        }

        private static uint GetRowLength(uint row, uint n, EdgeDirectionality directionality, bool selfties)
        {
            if (directionality == EdgeDirectionality.Directed)
                return selfties ? n : n - 1;
            else
                return selfties ? n - row : n - row - 1;
        }

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


    }
}
