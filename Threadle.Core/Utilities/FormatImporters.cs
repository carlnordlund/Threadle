using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Threadle.Core.Utilities
{
    public static class FormatImporters
    {
        internal static void ImportOneModeEdgelist(string filepath, Network network, LayerOneMode layerOneMode, string separator, bool addMissingNodes)
        {
            // So here we are just importing a standard 1-mode edgelist
            string[,] cells = ReadCells(filepath, separator);
            int nbrColumns = cells.GetLength(1);
            EdgeType valueType = layerOneMode.EdgeValueType;
            if (valueType == EdgeType.Binary && nbrColumns != 2)
                throw new Exception($"Error: Layer '{layerOneMode.Name}' is binary, so edgelist must have two columns.");
            if ((valueType == EdgeType.Valued) && nbrColumns != 3)
                throw new Exception($"Error: Layer '{layerOneMode.Name}' is valued, so edgelist must have three columns.");

            uint node1id, node2id;
            float value = 1;
            for (int r = 0; r < cells.GetLength(0); r++)
            {
                if (!uint.TryParse(cells[r, 0], out node1id) || !uint.TryParse(cells[r, 1], out node2id))
                    continue;
                if (valueType == EdgeType.Binary)
                    network.AddEdge(layerOneMode, node1id, node2id, 1, addMissingNodes);
                else
                {
                    if (float.TryParse(cells[r, 2], out value))
                        network.AddEdge(layerOneMode, node1id, node2id, Misc.FixConnectionValue(value, valueType), addMissingNodes);
                }
            }
        }

        internal static void ImportOneModeMatrix(string filepath, Network network, LayerOneMode layerOneMode, string separator, bool addMissingNodes)
        {
            string[,] cells = ReadCells(filepath, separator);
            
            int nbrRows = cells.GetLength(0), nbrCols = cells.GetLength(1);
            if (nbrRows != nbrCols)
                throw new Exception($"Error: Matrix not square shaped.");

            // Maybe from here I can call a generic method that takes matrix cells and populates a layer?
            // But for now, just read Matrix here as normal

            // Create arrays to convert row/col index to uint (to save time from parsing these headers every time)
            uint[] rowIds = new uint[nbrRows - 1];
            uint[] colIds = new uint[nbrCols - 1];
            for (int i = 1; i < nbrCols; i++)
            {
                if (!uint.TryParse(cells[0, i], out colIds[i-1]))
                    throw new Exception($"Error: Column header '{cells[0,i]}' in file '{filepath}' not an unsigned integer.");
                if (!uint.TryParse(cells[i, 0], out rowIds[i-1]))
                    throw new Exception($"Error: Row header '{cells[i, 0]}' in file '{filepath}' not an unsigned integer.");
            }

            float[,] data = Misc.ConvertStringCellsToFloatCells(cells, 1);
            bool hasSelfties = layerOneMode.Selfties;
            if (layerOneMode.Directionality == EdgeDirectionality.Directed)
            {
                for (int r = 1; r < nbrRows; r++)
                {
                    for (int c = 1; c < nbrCols; c++)
                    {
                        if (data[r - 1, c - 1] != 0)
                            network.AddEdge(layerOneMode, rowIds[r - 1], colIds[c - 1], data[r - 1, c - 1], addMissingNodes);
                    }
                }
            }
            else
            {
                for (int r = 1; r < nbrRows; r++)
                    for (int c = r; c < nbrCols; c++)
                        if (data[r - 1, c - 1] != 0)
                            network.AddEdge(layerOneMode, rowIds[r - 1], colIds[c - 1], data[r - 1, c - 1], addMissingNodes);
            }
        }

        internal static void ImportTwoModeEdgelist(string filepath, Network network, LayerTwoMode layerTwoMode, string separator, bool addMissingNodes)
        {
            // Ok - so this is the one to implement now!
            // Try parsing all lines, including the first
            string[,] cells = ReadCells(filepath, separator);
            if (cells.GetLength(1) != 2)
                throw new Exception($"Error: Edgelist must have two columns (separated by '{separator}').");

            uint nodeid;
            string affcode;
            Dictionary<string, List<uint>> affiliations = [];
            for (int r = 0; r < cells.GetLength(0); r++)
            {
                // Check that I can parse the nodeid. If not, could be the header.
                if (!uint.TryParse(cells[r, 0], out nodeid))
                    continue;
                affcode = cells[r, 1];
                if (affcode.Length == 0 || affcode == string.Empty)
                    continue;
                if (affiliations.ContainsKey(affcode))
                    affiliations[affcode].Add(nodeid);
                else
                    affiliations.Add(affcode, new List<uint>() { nodeid });
            }
            foreach ((string hypername, List<uint> nodeids) in affiliations)
                network.AddHyperedge(layerTwoMode, hypername, nodeids.ToArray(), addMissingNodes);
        }

        internal static void ImportTwoModeMatrix(string filepath, Network network, LayerTwoMode layerTwoMode, string separator, bool addMissingNodes)
        {
            //string[,] cells = ReadCells(filepath, separator);
            throw new NotImplementedException();
        }

        private static string[,] ReadCells(string filepath, string separator)
        {
            string[] lines = File.ReadAllLines(filepath) ??
                throw new Exception($"Error: Could not load file '{filepath}'");
            List<string[]> listOfCells = new List<string[]>();
            int nbrCols = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > 0)
                {
                    string[] lineCells = lines[i].Split(separator);
                    nbrCols = lineCells.Length > nbrCols ? lineCells.Length : nbrCols;
                    listOfCells.Add(lineCells);
                }
            }
            string[,] cells = new string[listOfCells.Count, nbrCols];
            for (int r = 0; r < listOfCells.Count; r++)
                for (int c = 0; c < listOfCells[r].Length; c++)
                    cells[r, c] = listOfCells[r][c].Trim();
            return cells;
        }
    }
}
