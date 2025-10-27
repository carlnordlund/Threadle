using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Utilities
{
    public static class NetworkGenerators
    {

        public static Network GenerateErdosRenyi3(Nodeset nodeset, double p, bool directed, bool selfties)
        {
            //ulong createdTies = 0;
            Network network = new Network("", nodeset);

            //// Get all nodes as Node[] array: work with it locally here
            //Node[] nodes = nodeset.NodeArray;

            //network.AddLayerOneMode("er", (directed) ? EdgeDirectionality.Directed : EdgeDirectionality.Undirected, EdgeValueType.Binary, selfties);

            //uint n = (uint)nodeset.Count;

            //ulong totalEdges = Misc.GetNbrPotentialEdges(n, directed, selfties);
            
            //ulong index = 0;
            //uint row = 0;
            //ulong rowStartindex = 0;
            //uint rowLength = GetRowLength(row, n, directed, selfties);

            //while (index < totalEdges)
            //{
            //    ulong skip = SampleGeometric(p);
            //    index += skip;
            //    if (index >= totalEdges)
            //        break;
            //    while (index >= rowStartindex + rowLength)
            //    {
            //        rowStartindex += rowLength;
            //        row++;
            //        rowLength = GetRowLength(row, n, directed, selfties);
            //    }
            //    uint offsetInRow = (uint)(index - rowStartindex);
            //    uint col = GetCol(row, offsetInRow, n, directed, selfties);
            //    //network.AddEdge("er", nodeset.GetNodeByIndex(row), nodeset.GetNodeByIndex(col), 1);
            //    //network.AddEdge("er", GetNodeByIndex(nodes, row), GetNodeByIndex(nodes, col), 1);
            //    network.AddEdge("er", nodes[row].Id, nodes[col].Id, 1);
            //    //createdTies++;
            //    index++;
            //}
            return network;
        }

        //private static Node GetNodeByIndex(Node[] nodes, uint index)
        //{
        //    if (index < nodes.Length)
        //        return nodes[(int)index];
        //    throw new IndexOutOfRangeException($"Node at index {index} not found.");
        //}

        public static void GenerateRandomBooleanAttribute(Network network, string attrName, float boolAttrProb)
        {
            //network.Nodeset.DefineNodeAttribute(attrName, AttributeType.Bool);
            //foreach (Node node in network.Nodeset.NodeArray)
            //    network.Nodeset.SetNodeAttribute(node.Id, attrName, (Misc.Random.NextDouble() < boolAttrProb));
        }

        public static void GenerateRandomIntAttribute(Network network, string attrName, int catAttrs)
        {
            //network.Nodeset.DefineNodeAttribute(attrName, AttributeType.Int);
            //foreach (Node node in network.Nodeset.NodeArray)
            //    network.Nodeset.SetNodeAttribute(node.Id, attrName, (int)(Misc.Random.Next(catAttrs)));
        }

        private static uint GetCol(uint row, uint offsetInRow, long n, bool directed, bool selfties)
        {
            if (directed)
            {
                if (selfties)
                    return offsetInRow;
                else
                    return offsetInRow >= row ? offsetInRow+1: offsetInRow;
            }
            else
                return (uint)(row + offsetInRow + (selfties ? 0 : 1));
        }

        private static uint GetRowLength(uint row, uint n, bool directed, bool selfties)
        {
            if (directed)
                return selfties ? n : n - 1;
            else
                return selfties ? n - row : n - row - 1;
        }

        //private static ulong SampleGeometric(double p)
        //{
        //    return (ulong)Math.Floor(Math.Log(Misc.Random.NextDouble()) / Math.Log(1.0 - p));
        //}
    }
}
