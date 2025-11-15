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
    /// <summary>
    /// Class implementing various methods for importing to specific layers
    /// </summary>
    internal static class LayerImporters
    {
        #region Methods (internal)
        /// <summary>
        /// Imports an edgelist file into a 1-mode network layer.
        /// </summary>
        /// <remarks>The method expects the edgelist file to conform to the following format: <list
        /// type="bullet"> <item> <description>For binary edges, the file must contain exactly two columns: the IDs of
        /// the two nodes connected by each edge. For layers with directed edges, the first column contains the source
        /// node ids, and the second column contains the destination node id.</description> </item> <item>
        /// <description>For valued edges, the file must contain exactly three columns: the IDs of the two nodes and the
        /// edge value.</description> </item> </list> If the file format does not match the expected structure for the
        /// layer's edge type, the operation will fail with an appropriate error message.</remarks>
        /// <param name="filepath">The path to the edgelist file to import. The file must be formatted with either two or three columns,
        /// depending on the edge type.</param>
        /// <param name="network">The network into which the edges will be imported.</param>
        /// <param name="layerOneMode">The one-mode layer of the network where the edges will be added. The layer's edge type determines the
        /// expected file format.</param>
        /// <param name="separator">The character used to separate columns in the edgelist file.</param>
        /// <param name="addMissingNodes">A value indicating whether nodes that are referenced in the matrix but do not exist in the network should be
        /// automatically added. <see langword="true"/> to add missing nodes; otherwise, <see langword="false"/>.</param>
        /// <returns>An <see cref="OperationResult"/> indicating the success or failure of the import operation.  If the
        /// operation fails, the result contains an error code and message describing the issue.</returns>
        internal static OperationResult ImportOneModeEdgelist(string filepath, Network network, LayerOneMode layerOneMode, string separator, bool addMissingNodes)
        {
            try
            {
                string[,] cells = ReadCells(filepath, separator);
                int nbrColumns = cells.GetLength(1);
                EdgeType valueType = layerOneMode.EdgeValueType;
                if (valueType == EdgeType.Binary && nbrColumns != 2)
                    return OperationResult.Fail("FileColumnsError", $"Layer '{layerOneMode.Name}' is binary, so edgelist must have two columns.");
                if ((valueType == EdgeType.Valued) && nbrColumns != 3)
                    return OperationResult.Fail("FileColumnsError", $"Layer '{layerOneMode.Name}' is valued, so edgelist must have three columns.");
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
                        if (!float.TryParse(cells[r, 2], out value))
                            return OperationResult.Fail("FileFormatError", $"Could not parse value '{cells[r,2]}' in edgelist '{filepath}'.");
                        network.AddEdge(layerOneMode, node1id, node2id, Misc.FixConnectionValue(value, valueType), addMissingNodes);
                    }
                }
                return OperationResult.Ok();
            }
            catch (Exception e)
            {
                return OperationResult.Fail("UnexpectedImportError", e.Message);
            }
        }

        /// <summary>
        /// Imports a one-mode matrix file into a 1-mode network layer.
        /// </summary>
        /// <remarks>The file must contain a square matrix where the first row and column represent
        /// unsigned integer node IDs. The matrix values represent edge weights between the nodes. If the matrix is not
        /// square, or if the headers are not valid unsigned integers, the operation will fail. For directed networks, the
        /// first column contains the source node ids, and the first row contains the destinat5ion node ids. For symmetric
        /// networks, only the upper triangle of the matrix is checked for ties.</remarks>
        /// <param name="filepath">The path to the file containing the one-mode matrix. The file must be in a square matrix format with
        /// unsigned integer row and column headers.</param>
        /// <param name="network">The <see cref="Network"/> instance to which the edges will be added.</param>
        /// <param name="layerOneMode">The one-mode layer of the network where the edges will be added.</param>
        /// <param name="separator">The character used to separate values in the file</param>
        /// <param name="addMissingNodes">A value indicating whether nodes that are referenced in the matrix but do not exist in the network should be
        /// automatically added. <see langword="true"/> to add missing nodes; otherwise, <see langword="false"/>.</param>
        /// <returns>An <see cref="OperationResult"/> indicating the success or failure of the import operation.  If the
        /// operation fails, the result contains an error code and message describing the issue.</returns>
        internal static OperationResult ImportOneModeMatrix(string filepath, Network network, LayerOneMode layerOneMode, string separator, bool addMissingNodes)
        {
            try
            {
                string[,] cells = ReadCells(filepath, separator);
                int nbrRows = cells.GetLength(0), nbrCols = cells.GetLength(1);
                if (nbrRows != nbrCols)
                    return OperationResult.Fail("FileFormatError", $"Matrix '{filepath}' not square-shaped.");
                uint[] rowIds = new uint[nbrRows - 1];
                uint[] colIds = new uint[nbrCols - 1];
                for (int i = 1; i < nbrCols; i++)
                {
                    if (!uint.TryParse(cells[0, i], out colIds[i - 1]))
                        return OperationResult.Fail("FileFormatError", $"Column header '{cells[0, i]}' in file '{filepath}' not an unsigned integer.");
                    if (!uint.TryParse(cells[i, 0], out rowIds[i - 1]))
                        return OperationResult.Fail("FileFormatError", $"Row header '{cells[i, 0]}' in file '{filepath}' not an unsigned integer.");
                }
                float[,] data = Misc.ConvertStringCellsToFloatCells(cells, 1);
                bool hasSelfties = layerOneMode.Selfties;
                if (layerOneMode.Directionality == EdgeDirectionality.Directed)
                {
                    for (int r = 1; r < nbrRows; r++)
                        for (int c = 1; c < nbrCols; c++)
                            if (data[r - 1, c - 1] != 0)
                                network.AddEdge(layerOneMode, rowIds[r - 1], colIds[c - 1], data[r - 1, c - 1], addMissingNodes);
                }
                else
                {
                    for (int r = 1; r < nbrRows; r++)
                        for (int c = r; c < nbrCols; c++)
                            if (data[r - 1, c - 1] != 0)
                                network.AddEdge(layerOneMode, rowIds[r - 1], colIds[c - 1], data[r - 1, c - 1], addMissingNodes);
                }
                return OperationResult.Ok();
            }
            catch (Exception e)
            {
                return OperationResult.Fail("UnexpectedImportError", e.Message);
            }
        }

        /// <summary>
        /// Imports a two-mode edgelist file into a 2-mode network layer.
        /// </summary>
        /// <remarks>The edgelist file must contain exactly two columns: the first column represents node
        /// ids (as unsigned integers),  and the second column represents affiliation codes (as non-empty strings). Rows
        /// with invalid or empty values are ignored. Note that affiliation codes must be unique - this is not checked/validated here.</remarks>
        /// <param name="filepath">The path to the file containing the two-mode edgelist. The file must have two columns separated by the
        /// specified <paramref name="separator"/>.</param>
        /// <param name="network">The <see cref="Network"/> instance to which the hyperedges will be added.</param>
        /// <param name="layerTwoMode">The two-mode layer of the network where the hyperedges will be added.</param>
        /// <param name="separator">The character used to separate columns in the edgelist file.</param>
        /// <param name="addMissingNodes">A value indicating whether nodes that are referenced in the edgelist but do not exist in the network should
        /// be added automatically. <see langword="true"/> to add missing nodes; otherwise, <see langword="false"/>.</param>
        /// <returns>An <see cref="OperationResult"/> indicating the success or failure of the import operation.  If the
        /// operation fails, the result contains an error code and message describing the issue.</returns>
        internal static OperationResult ImportTwoModeEdgelist(string filepath, Network network, LayerTwoMode layerTwoMode, string separator, bool addMissingNodes)
        {
            try
            {
                string[,] cells = ReadCells(filepath, separator);
                if (cells.GetLength(1) != 2)
                    return OperationResult.Fail("FileFormatError", $"Edgelist must have two columns (separated by '{separator}').");
                uint nodeid;
                string affcode;
                Dictionary<string, List<uint>> affiliations = [];
                for (int r = 0; r < cells.GetLength(0); r++)
                {
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
                return OperationResult.Ok();
            }
            catch (Exception e)
            {
                return OperationResult.Fail("UnexpectedImportError", e.Message);
            }
        }

        /// <summary>
        /// Imports a two-mode matrix file into a 2-mode network layer.
        /// </summary>
        /// <remarks>The file must have a specific format: <list type="bullet"> <item>The first row
        /// contains column headers representing the names of the hyperedges. Note that these
        /// hyperedge names must be unique: this is not validated!</item> <item>The first column contains row
        /// headers representing node IDs, which must be unsigned integers.</item> <item>The remaining cells contain
        /// numeric values, where a positive value indicates a connection between the node and the hyperedge.</item>
        /// </list> If the file format is invalid (e.g., row headers are not unsigned integers), the operation will fail
        /// with a "FileFormatError".</remarks>
        /// <param name="filepath">The path to the file containing the two-mode matrix. The file must use the specified separator to delimit
        /// values.</param>
        /// <param name="network">The <see cref="Network"/> instance to which the hyperedges will be added.</param>
        /// <param name="layerTwoMode">The two-mode layer of the network where the hyperedges will be added.</param>
        /// <param name="separator">The character used to separate values in the file</param>
        /// <param name="addMissingNodes">A value indicating whether nodes that are referenced in the edgelist but do not exist in the network should
        /// be added automatically. <see langword="true"/> to add missing nodes; otherwise, <see langword="false"/>.</param>
        /// <returns>An <see cref="OperationResult"/> indicating the success or failure of the import operation.  If the
        /// operation fails, the result contains an error code and message describing the issue.</returns>
        internal static OperationResult ImportTwoModeMatrix(string filepath, Network network, LayerTwoMode layerTwoMode, string separator, bool addMissingNodes)
        {
            try
            {
                string[,] cells = ReadCells(filepath, separator);
                int nbrRows = cells.GetLength(0), nbrCols = cells.GetLength(1);
                uint[] rowIds = new uint[nbrRows - 1];
                string[] colNames = new string[nbrCols - 1];
                for (int i = 1; i < nbrRows; i++)
                    if (!uint.TryParse(cells[i, 0], out rowIds[i - 1]))
                        return OperationResult.Fail("FileFormatError", $"Row header '{cells[i, 0]}' in file '{filepath}' not an unsigned integer.");
                for (int i = 1; i < nbrCols; i++)
                    colNames[i - 1] = cells[0, i];
                float[,] data = Misc.ConvertStringCellsToFloatCells(cells, 1);
                for (int c = 0; c < colNames.Length; c++)
                {
                    List<uint> nodeIds = [];
                    for (int r = 0; r < rowIds.Length; r++)
                        if (data[r, c] > 0)
                            nodeIds.Add(rowIds[r]);
                    network.AddHyperedge(layerTwoMode, colNames[c], nodeIds.ToArray(), addMissingNodes);
                }
                return OperationResult.Ok();
            }
            catch (Exception e)
            {
                return OperationResult.Fail("UnexpectedImportError", e.Message);
            }
        }
        #endregion


        #region Methods (private)
        /// <summary>
        /// Reads the contents of a delimited text file and returns a two-dimensional array of strings representing the
        /// parsed cells.
        /// </summary>
        /// <remarks>Lines in the file that are empty are ignored. The resulting array will have a number
        /// of columns equal to the longest line in the file, with shorter lines padded with empty strings.</remarks>
        /// <param name="filepath">The path to the file to be read. The file must exist and be accessible.</param>
        /// <param name="separator">The string used to separate values within each line of the file.</param>
        /// <returns>A two-dimensional array of strings where each row corresponds to a line in the file and each column
        /// corresponds to a value separated by the specified <paramref name="separator"/>. Empty cells are represented
        /// as empty strings.</returns>
        /// <exception cref="Exception">Thrown if the file at <paramref name="filepath"/> cannot be loaded.</exception>
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
        #endregion
    }
}
