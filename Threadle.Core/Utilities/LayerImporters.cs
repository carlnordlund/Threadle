using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Class implementing various methods for importing to specific layers
    /// </summary>
    internal static class LayerImporters
    {
        #region Methods (internal)

        /// <summary>
        /// Imports a 1-mode edgelist from file, inserting it into the specified layer. Checks that the node exists
        /// in the Nodeset of the network: will either ignore those lines or add these nodes, depending on the setting
        /// of <paramref name="addMissingNodes"/>.
        /// Note that any existing edges in the layer are not removed.
        /// Note also that a deduplication cleanup is done after importing: as edges are added without checking for
        /// multiedges, this will remove any would-be occurrences of multiedges.
        /// This method separates into different code blocks depending on whether it is binary or valued, and whether
        /// missing nodes should be added or not, i.e. a total of 4 different code blocks.
        /// The edgelist file:
        /// The first column contains the first nodeid, the second column contains the second node id.
        /// The file might have a header: that will be ignored.
        /// For valued layers, the edgelist must have a third column containing the edge value.
        /// </summary>
        /// <param name="filepath">The filepath to the edgelist</param>
        /// <param name="network">The network (to obtain the Nodeset)</param>
        /// <param name="layerOneMode">The 1-mode layer to import to.</param>
        /// <param name="separator">Character that separates columns</param>
        /// <param name="addMissingNodes">Set to true if nodes not in the Nodeset should be added to it.</param>
        /// <exception cref="FileNotFoundException">Thrown if the file is not found</exception>
        /// <exception cref="Exception">Exceptions when something went wrong.</exception>
        internal static void ImportOneModeEdgelist(string filepath, Network network, LayerOneMode layerOneMode, char separator, bool addMissingNodes)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: {filepath}");
            using var reader = new StreamReader(filepath);
            string? line;
            int lineNumber = 0;

            uint node1id, node2id;
            if (layerOneMode.IsBinary)
            {
                // Importing binary data
                if (addMissingNodes)
                {
                    // Add nodes if missing from Nodeset
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        ReadOnlySpan<char> span = line.AsSpan();
                        int sepIndex = span.IndexOf(separator);
                        if (sepIndex < 0)
                            throw new Exception($"Invalid column count at line {lineNumber}");
                        if (!uint.TryParse(span.Slice(0, sepIndex), out node1id) || !uint.TryParse(span.Slice(sepIndex + 1), out node2id))
                            continue;
                        if (!network.Nodeset.CheckThatNodeExists(node1id))
                            network.Nodeset._addNodeWithoutAttribute(node1id);
                        if (!network.Nodeset.CheckThatNodeExists(node2id))
                            network.Nodeset._addNodeWithoutAttribute(node2id);
                        layerOneMode._addEdge(node1id, node2id);
                    }
                }
                else
                {
                    // Ignore edges that refers to non-existing nodes
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        ReadOnlySpan<char> span = line.AsSpan();
                        int sepIndex = span.IndexOf(separator);
                        if (sepIndex < 0)
                            throw new Exception($"Invalid column count at line {lineNumber}");
                        if (!uint.TryParse(span.Slice(0, sepIndex), out node1id) || !uint.TryParse(span.Slice(sepIndex + 1), out node2id))
                            continue;
                        if (!network.Nodeset.CheckThatNodeExists(node1id) || !network.Nodeset.CheckThatNodeExists(node2id))
                            continue;
                        layerOneMode._addEdge(node1id, node2id);
                    }
                }
            }
            else if (layerOneMode.IsValued)
            {
                // Importing valued data
                float value = 1;
                if (addMissingNodes)
                {
                    // Add nodes if missing from Nodeset
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        ReadOnlySpan<char> span = line.AsSpan();
                        int sepIndex1 = span.IndexOf(separator);
                        int sepIndex2 = span.Slice(sepIndex1 + 1).IndexOf(separator);
                        sepIndex2 += sepIndex1 + 1;
                        if (sepIndex1 < 0 || sepIndex2 < 0)
                            throw new Exception($"Invalid column count at line {lineNumber}");
                        if (!uint.TryParse(span[..sepIndex1], out node1id)
                            || !uint.TryParse(span[(sepIndex1+1)..sepIndex2], out node2id)
                            || !float.TryParse(span[(sepIndex2+1)..], out value))
                            continue;
                        if (!network.Nodeset.CheckThatNodeExists(node1id))
                            network.Nodeset._addNodeWithoutAttribute(node1id);
                        if (!network.Nodeset.CheckThatNodeExists(node2id))
                            network.Nodeset._addNodeWithoutAttribute(node2id);
                        layerOneMode._addEdge(node1id, node2id, value);
                    }
                }
                else
                {
                    // Ignore edges that refers to non-existing nodes
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        ReadOnlySpan<char> span = line.AsSpan();
                        int sepIndex1 = span.IndexOf(separator);
                        int sepIndex2 = span.Slice(sepIndex1 + 1).IndexOf(separator);
                        sepIndex2 += sepIndex1 + 1;
                        if (sepIndex1 < 0 || sepIndex2 < 0)
                            throw new Exception($"Invalid column count at line {lineNumber}");
                        if (!uint.TryParse(span[..sepIndex1], out node1id)
                            || !uint.TryParse(span[(sepIndex1 + 1)..sepIndex2], out node2id)
                            || !float.TryParse(span[(sepIndex2 + 1)..], out value))
                            continue;
                        if (!network.Nodeset.CheckThatNodeExists(node1id) || !network.Nodeset.CheckThatNodeExists(node2id))
                            continue;
                        layerOneMode._addEdge(node1id, node2id, value);
                    }
                }
            }
            // Deduplicate edges
            layerOneMode._deduplicateEdgesets();
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
        internal static void ImportTwoModeEdgelist(string filepath, Network network, LayerTwoMode layerTwoMode, char separator, bool addMissingNodes)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: {filepath}");
            using var reader = new StreamReader(filepath);
            string? line;
            int lineNumber = 0;
            uint nodeId;
            string hyperedgeName;
            // Distinguish between whether unknown nodes should be added or ignored: to speed up loading
            if (addMissingNodes)
            {
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    ReadOnlySpan<char> span = line.AsSpan();
                    int sepIndex = span.IndexOf(separator);
                    if (sepIndex < 0)
                        throw new Exception($"Invalid column count at line {lineNumber}");
                    if (!uint.TryParse(span.Slice(0, sepIndex), out nodeId))
                        continue;
                    hyperedgeName = new string(span.Slice(sepIndex + 1)).Trim();
                    if (hyperedgeName.Length < 1)
                        continue;
                    if (!network.Nodeset.CheckThatNodeExists(nodeId))
                        network.Nodeset._addNodeWithoutAttribute(nodeId);
                    layerTwoMode._addAffiliation(nodeId, hyperedgeName);
                }
            }
            else
            {
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    ReadOnlySpan<char> span = line.AsSpan();
                    int sepIndex = span.IndexOf(separator);
                    if (sepIndex < 0)
                        throw new Exception($"Invalid column count at line {lineNumber}");
                    if (!uint.TryParse(span.Slice(0, sepIndex), out nodeId))
                        continue;
                    if (!network.Nodeset.CheckThatNodeExists(nodeId))
                        continue;
                    hyperedgeName = new string(span.Slice(sepIndex + 1)).Trim();
                    if (hyperedgeName.Length < 1)
                        continue;
                    layerTwoMode._addAffiliation(nodeId, hyperedgeName);
                }
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
