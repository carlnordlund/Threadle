using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Class implementing various methods for importing to specific layers
    /// </summary>
    internal static class LayerImportExport
    {

        #region Methods (internal)
        /// <summary>
        /// Exports a specified 1-mode layer as an edgelist to the specified file. The first two columns contains the node pairs
        /// for each edge. If the layer contains valued ties, a third column contains the edge value. An optional header is shown
        /// on the first row, this being either 'from' and 'to' for directional edges, and 'node1' and 'node2' for symmetric edges.
        /// For valued edges, the header for the third column is 'value. Columns are separated using the provided sep character.
        /// </summary>
        /// <param name="layerOneMode">The 1-mode layer</param>
        /// <param name="filepath">File to write the edgelist to</param>
        /// <param name="sep">The separator to use</param>
        /// <param name="header">Boolean whether the first line is to contain headers.</param>
        internal static void ExportOneModeEdgeList(LayerOneMode layerOneMode, string filepath, char sep, bool header)
        {
            using var writer = new StreamWriter(filepath);

            if (header)
            {
                string headerLine = layerOneMode.IsDirectional ? $"from{sep}to" : $"node1{sep}node2";
                headerLine += layerOneMode.IsValued ? $"{sep}value" : "";
                writer.WriteLine(headerLine);
            }
            if (layerOneMode.IsBinary)
                foreach (var kvp in layerOneMode.Edgesets)
                {
                    uint egoId = kvp.Key;
                    foreach (var alterId in kvp.Value.GetOutboundNodeIds)
                        writer.WriteLine($"{egoId}{sep}{alterId}");
                }
            else
                foreach (var kvp in layerOneMode.Edgesets)
                {
                    uint egoId = kvp.Key;
                    foreach (var (alterId, value) in kvp.Value.GetOutboundEdgesWithValues(egoId))
                        writer.WriteLine($"{egoId}{sep}{alterId}{sep}{value}");
                }
        }

        /// <summary>
        /// Exports a specified 2-mode layer as an edgelist to the specified file. The first column contains the node id
        /// and the second column contains the affiliation (i.e. hyperedge name). An optional header is shown at the top
        /// with 'node' and 'affiliation' as headers. Columns are separated using the provided character.
        /// </summary>
        /// <param name="layerTwoMode">The 2-mode layer</param>
        /// <param name="filepath">File to write the edgelist to</param>
        /// <param name="sep">The separator to use</param>
        /// <param name="header">Boolean whether the first line is to contain headers.</param>
        internal static void ExportTwoModeEdgeList(LayerTwoMode layerTwoMode, string filepath, char sep, bool header)
        {
            using var writer = new StreamWriter(filepath);
            if (header)
                writer.WriteLine($"node{sep}affiliation");
            foreach (var kvp in layerTwoMode.HyperEdgeCollections)
            {
                uint egoId = kvp.Key;
                foreach (var hyperedge in kvp.Value.HyperEdges)
                    writer.WriteLine($"{egoId}{sep}{hyperedge.Name}");
            }
        }

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
        internal static void ImportOneModeEdgelist(string filepath, Network network, LayerOneMode layerOneMode, int node1col, int node2col, int valueCol, bool hasHeader, char separator, bool addMissingNodes)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: {filepath}");
            using var reader = new StreamReader(filepath);
            string? line;
            int lineNumber = 0;

            int maxColIndex = Math.Max(node1col, node2col);
            uint node1Id, node2Id;
            if (layerOneMode.IsBinary)
            {
                // Importing binary datac
                if (addMissingNodes)
                {
                    // Add nodes if missing from Nodeset
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;

                        if (lineNumber == 1 && hasHeader)
                            continue;

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        string[] columns = line.Split(separator);

                        // Skip rows that don't have enough columns
                        if (columns.Length <= maxColIndex)
                            continue;

                        // Skip rows where we can't parse node ids
                        if (!uint.TryParse(Misc.TrimQuotes(columns[node1col]), out node1Id) || !uint.TryParse(Misc.TrimQuotes(columns[node2col]), out node2Id))
                            continue;

                        // Add nodes that are missing
                        if (!network.Nodeset.CheckThatNodeExists(node1Id))
                            network.Nodeset._addNodeWithoutAttribute(node1Id);
                        if (!network.Nodeset.CheckThatNodeExists(node2Id))
                            network.Nodeset._addNodeWithoutAttribute(node2Id);
                        layerOneMode._addEdge(node1Id, node2Id);
                    }
                }
                else
                {
                    // Ignore edges that refers to non-existing nodes
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;

                        if (lineNumber == 1 && hasHeader)
                            continue;

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        string[] columns = line.Split(separator);

                        // Skip rows that don't have enough columns
                        if (columns.Length <= maxColIndex)
                            continue;

                        // Skip rows where we can't parse node ids
                        if (!uint.TryParse(Misc.TrimQuotes(columns[node1col]), out node1Id) || !uint.TryParse(Misc.TrimQuotes(columns[node2col]), out node2Id))
                            continue;

                        // Skip edges with nodes that are missing
                        if (!network.Nodeset.CheckThatNodeExists(node1Id) || !network.Nodeset.CheckThatNodeExists(node2Id))
                            continue;
                        layerOneMode._addEdge(node1Id, node2Id);
                    }
                }
            }
            else if (layerOneMode.IsValued)
            {
                // Now also include the valueCol in the minCol check
                maxColIndex = Math.Max(maxColIndex, valueCol);

                // Importing valued data
                float value = 1;
                if (addMissingNodes)
                {
                    // Add nodes if missing from Nodeset
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;

                        if (lineNumber == 1 && hasHeader)
                            continue;

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        string[] columns = line.Split(separator);

                        // Skip rows that don't have enough columns
                        if (columns.Length <= maxColIndex)
                            continue;

                        // Skip rows where we can't parse node ids or float value
                        if (!uint.TryParse(Misc.TrimQuotes(columns[node1col]), out node1Id) || !uint.TryParse(Misc.TrimQuotes(columns[node2col]), out node2Id) || !float.TryParse(Misc.TrimQuotes(columns[valueCol]), out value))
                            continue;

                        // Add nodes that are missing                        
                        if (!network.Nodeset.CheckThatNodeExists(node1Id))
                            network.Nodeset._addNodeWithoutAttribute(node1Id);
                        if (!network.Nodeset.CheckThatNodeExists(node2Id))
                            network.Nodeset._addNodeWithoutAttribute(node2Id);
                        layerOneMode._addEdge(node1Id, node2Id, value);
                    }
                }
                else
                {
                    // Ignore edges that refers to non-existing nodes
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        if (lineNumber == 1 && hasHeader)
                            continue;

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        string[] columns = line.Split(separator);

                        // Skip rows that don't have enough columns
                        if (columns.Length <= maxColIndex)
                            continue;

                        // Skip rows where we can't parse node ids or float value
                        if (!uint.TryParse(Misc.TrimQuotes(columns[node1col]), out node1Id) || !uint.TryParse(Misc.TrimQuotes(columns[node2col]), out node2Id) || !float.TryParse(Misc.TrimQuotes(columns[valueCol]), out value))
                            continue;

                        // Skip edges that have missing nodes
                        if (!network.Nodeset.CheckThatNodeExists(node1Id) || !network.Nodeset.CheckThatNodeExists(node2Id))
                            continue;
                        layerOneMode._addEdge(node1Id, node2Id, value);
                    }
                }
            }
            // Deduplicate edges
            layerOneMode._deduplicateEdgesets();
        }

        /// <summary>
        /// Imports a one-mode matrix file into a 1-mode network layer, with node ids given on first row and first column.
        /// Will add new nodes if addMissingNodes is true. Note that the order of rows and column nodes can be different:
        /// it keeps track of these separately. Uses the normal Network.AddEdge() method: that will take care of the addMissingNodes.
        /// </summary>
        /// <remarks>The file must contain a square matrix where the first row and column represent
        /// unsigned integer node IDs. The matrix values represent edge weights between the nodes. If the matrix is not
        /// square, or if the headers are not valid unsigned integers, the operation will fail. For directed networks, the
        /// first column contains the source node ids, and the first row contains the destination node ids. For symmetric
        /// networks, only the upper triangle of the matrix is checked for ties.</remarks>
        /// <param name="filepath">The path to the file containing the one-mode matrix. The file must be in a square matrix format with
        /// unsigned integer row and column headers.</param>
        /// <param name="network">The <see cref="Network"/> instance to which the edges will be added.</param>
        /// <param name="layerOneMode">The one-mode layer of the network where the edges will be added.</param>
        /// <param name="separator">The character used to separate values in the file</param>
        /// <param name="addMissingNodes">A value indicating whether nodes that are referenced in the matrix but do not exist in the network should be
        /// automatically added. <see langword="true"/> to add missing nodes; otherwise, <see langword="false"/>.</param>
        /// <exception cref="FileNotFoundException">Thrown if the file is not found</exception>
        /// <exception cref="Exception">Exceptions when something went wrong.</exception>
        internal static void ImportOneModeMatrix(string filepath, Network network, LayerOneMode layerOneMode, char separator, bool addMissingNodes)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: {filepath}");
            string[,] cells = ReadCells(filepath, separator);
            int nbrRows = cells.GetLength(0), nbrCols = cells.GetLength(1);
            if (nbrRows != nbrCols)
                throw new Exception($"Number of rows ({nbrRows}) different than number of columns ({nbrCols}) in file '{filepath}'");
            uint[] rowIds = new uint[nbrRows - 1];
            uint[] colIds = new uint[nbrCols - 1];
            for (int i = 1; i < nbrCols; i++)
            {
                if (!uint.TryParse(cells[0, i], out colIds[i - 1]))
                    throw new Exception($"Column header '{cells[0, i]}' in file '{filepath}' not an unsigned integer.");
                if (!uint.TryParse(cells[i, 0], out rowIds[i - 1]))
                    throw new Exception($"Row header '{cells[i, 0]}' in file '{filepath}' not an unsigned integer.");
            }
            float[,] data = Misc.ConvertStringCellsToFloatCells(cells, 1);
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


            //try
            //{
            //    string[,] cells = ReadCells(filepath, separator);
            //    int nbrRows = cells.GetLength(0), nbrCols = cells.GetLength(1);
            //    if (nbrRows != nbrCols)
            //        return OperationResult.Fail("FileFormatError", $"Matrix '{filepath}' not square-shaped.");
            //    uint[] rowIds = new uint[nbrRows - 1];
            //    uint[] colIds = new uint[nbrCols - 1];
            //    for (int i = 1; i < nbrCols; i++)
            //    {
            //        if (!uint.TryParse(cells[0, i], out colIds[i - 1]))
            //            return OperationResult.Fail("FileFormatError", $"Column header '{cells[0, i]}' in file '{filepath}' not an unsigned integer.");
            //        if (!uint.TryParse(cells[i, 0], out rowIds[i - 1]))
            //            return OperationResult.Fail("FileFormatError", $"Row header '{cells[i, 0]}' in file '{filepath}' not an unsigned integer.");
            //    }
            //    float[,] data = Misc.ConvertStringCellsToFloatCells(cells, 1);
            //    bool hasSelfties = layerOneMode.Selfties;
            //    if (layerOneMode.Directionality == EdgeDirectionality.Directed)
            //    {
            //        for (int r = 1; r < nbrRows; r++)
            //            for (int c = 1; c < nbrCols; c++)
            //                if (data[r - 1, c - 1] != 0)
            //                    network.AddEdge(layerOneMode, rowIds[r - 1], colIds[c - 1], data[r - 1, c - 1], addMissingNodes);
            //    }
            //    else
            //    {
            //        for (int r = 1; r < nbrRows; r++)
            //            for (int c = r; c < nbrCols; c++)
            //                if (data[r - 1, c - 1] != 0)
            //                    network.AddEdge(layerOneMode, rowIds[r - 1], colIds[c - 1], data[r - 1, c - 1], addMissingNodes);
            //    }
            //    return OperationResult.Ok();
            //}
            //catch (Exception e)
            //{
            //    return OperationResult.Fail("UnexpectedImportError", e.Message);
            //}
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
        /// <exception cref="FileNotFoundException">Thrown if the file is not found</exception>
        /// <exception cref="Exception">Exceptions when something went wrong.</exception>
        internal static void ImportTwoModeEdgelist(string filepath, Network network, LayerTwoMode layerTwoMode, int nodeCol, int affCol, char separator, bool addMissingNodes)
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
        /// Imports a two-mode matrix file into a two-mode network layer, with node ids given on first column and affiliation
        /// (hyperedge) names given on first row. Will add new nodes if addMissingNodes is true. Uses the normal
        /// Network.AddHyperedge() method: that will take care of validation and the addMissingNodes option.
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
        /// <exception cref="FileNotFoundException">Thrown if the file is not found</exception>
        /// <exception cref="Exception">Exceptions when something went wrong.</exception>
        internal static void ImportTwoModeMatrix(string filepath, Network network, LayerTwoMode layerTwoMode, char separator, bool addMissingNodes)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: {filepath}");
            string[,] cells = ReadCells(filepath, separator);
            int nbrRows = cells.GetLength(0), nbrCols = cells.GetLength(1);
            uint[] rowIds = new uint[nbrRows - 1];
            string[] colNames = new string[nbrCols - 1];
            for (int i = 1; i < nbrRows; i++)
                if (!uint.TryParse(cells[i, 0], out rowIds[i - 1]))
                    throw new Exception($"Row header '{cells[i, 0]}' in file '{filepath}' not an unsigned integer.");
            for (int i = 1; i < nbrCols; i++)
                colNames[i - 1] = cells[0, i].Trim();
            float[,] data = Misc.ConvertStringCellsToFloatCells(cells, 1);
            for (int c = 0; c < colNames.Length; c++)
            {
                List<uint> nodeIds = [];
                for (int r = 0; r < rowIds.Length; r++)
                    if (data[r, c] > 0)
                        network.AddAffiliation(layerTwoMode, colNames[c], rowIds[r], addMissingNodes, true);
            }

            //try
            //{
            //    string[,] cells = ReadCells(filepath, separator);
            //    int nbrRows = cells.GetLength(0), nbrCols = cells.GetLength(1);
            //    uint[] rowIds = new uint[nbrRows - 1];
            //    string[] colNames = new string[nbrCols - 1];
            //    for (int i = 1; i < nbrRows; i++)
            //        if (!uint.TryParse(cells[i, 0], out rowIds[i - 1]))
            //            return OperationResult.Fail("FileFormatError", $"Row header '{cells[i, 0]}' in file '{filepath}' not an unsigned integer.");
            //    for (int i = 1; i < nbrCols; i++)
            //        colNames[i - 1] = cells[0, i];
            //    float[,] data = Misc.ConvertStringCellsToFloatCells(cells, 1);
            //    for (int c = 0; c < colNames.Length; c++)
            //    {
            //        List<uint> nodeIds = [];
            //        for (int r = 0; r < rowIds.Length; r++)
            //            if (data[r, c] > 0)
            //                nodeIds.Add(rowIds[r]);
            //        network.AddHyperedge(layerTwoMode, colNames[c], nodeIds.ToArray(), addMissingNodes);
            //    }
            //    return OperationResult.Ok();
            //}
            //catch (Exception e)
            //{
            //    return OperationResult.Fail("UnexpectedImportError", e.Message);
            //}
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
        private static string[,] ReadCells(string filepath, char separator)
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
