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
        internal static void ExportOneModeEdgeList(ILayerOneMode layerOneMode, string filepath, char sep, bool header)
        {
            using var writer = new StreamWriter(filepath);

            if (header)
            {
                string headerLine = layerOneMode.IsDirectional ? $"from{sep}to" : $"node1{sep}node2";
                headerLine += layerOneMode.IsValued ? $"{sep}value" : "";
                writer.WriteLine(headerLine);
            }
            if (layerOneMode.IsBinary)
            {
                foreach (var (egoId, alters, values) in layerOneMode.GetAllEgoData())
                    foreach (var alterId in alters.Span)
                        if (layerOneMode.IsDirectional || alterId > egoId)
                            writer.WriteLine($"{egoId}{sep}{alterId}");
            }
            else
            {
                foreach (var (egoId, alters, values) in layerOneMode.GetAllEgoData())
                    for (int i = 0; i < alters.Length; i++)
                        if (layerOneMode.IsDirectional || alters.Span[i] > egoId)
                            writer.WriteLine($"{egoId}{sep}{alters.Span[i]}{sep}{values.Span[i]}");
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
        internal static void ExportTwoModeEdgeList(ILayerTwoMode layerTwoMode, string filepath, char sep, bool header)
        {
            using var writer = new StreamWriter(filepath);
            if (header)
                writer.WriteLine($"node{sep}affiliation");
            foreach (var (hypername, nodeIds) in layerTwoMode.GetAllHyperedgeData())
                foreach (var nodeId in nodeIds)
                    writer.WriteLine($"{nodeId}{sep}{hypername}");
        }

        // To do
        internal static void ExportOneModeMatrix(ILayerOneMode layerOneMode, string filepath, char sep, bool header)
        {
            var nodeIdSet = new HashSet<uint>();
            var edgeLookup = new Dictionary<(uint from, uint to), float>();
            foreach (var (egoId, alters, values) in layerOneMode.GetAllEgoData())
            {
                nodeIdSet.Add(egoId);
                ReadOnlySpan<uint> alterSpan = alters.Span;
                ReadOnlySpan<float> valSpan = values.Span;
                for (int i=0; i<alterSpan.Length;i++)
                {
                    uint alterId = alterSpan[i];
                    nodeIdSet.Add(alterId);
                    float val = layerOneMode.IsValued ? valSpan[i] : 1f;
                    edgeLookup[(egoId, alterId)] = val;
                    if (!layerOneMode.IsDirectional)
                        edgeLookup[(alterId, egoId)] = val;
                }

                uint[] nodeIds = [.. nodeIdSet.OrderBy(id => id)];
                using var writer = new StreamWriter(filepath);
                if (header)
                {
                    writer.Write(sep);
                    writer.WriteLine(string.Join(sep, nodeIds));
                }
                foreach (uint rowId in nodeIds)
                {
                    writer.Write(rowId);
                    foreach (uint colId in nodeIds)
                    {
                        writer.Write(sep);
                        if (edgeLookup.TryGetValue((rowId, colId), out float val))
                            writer.Write(val.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        else
                            writer.Write(0);
                    }
                    writer.WriteLine();
                }
            }
        }

        // To do
        internal static void ExportTwoModeMatrix(ILayerTwoMode layerTwoMode, string filepath, char sep, bool header)
        {
            var hyperedgeNodeSets = new Dictionary<string, HashSet<uint>>();
            var allNodeIds = new HashSet<uint>();

            foreach (var (hypername, nodeIds) in layerTwoMode.GetAllHyperedgeData())
            {
                hyperedgeNodeSets[hypername] = new HashSet<uint>(nodeIds);
                foreach (uint id in nodeIds)
                    allNodeIds.Add(id);
            }

            uint[] sortedNodeIds = [.. allNodeIds.OrderBy(id => id)];
            string[] sortedHyperedgeNames = [.. hyperedgeNodeSets.Keys.OrderBy(n => n)];

            using var writer = new StreamWriter(filepath);
            if (header)
            {
                writer.Write(sep);
                writer.WriteLine(string.Join(sep, sortedHyperedgeNames));
            }
            foreach (uint nodeId in sortedNodeIds)
            {
                writer.Write(nodeId);
                foreach (string hypername in sortedHyperedgeNames)
                {
                    writer.Write(sep);
                    writer.Write(hyperedgeNodeSets[hypername].Contains(nodeId) ? 1 : 0);
                }
                writer.WriteLine();
            }
        }

        /// <summary>
        /// Imports a 1-mode edgelist from file, inserting it into the specified layer. Node identifiers are resolved
        /// through the provided <see cref="NodeIdResolver"/>: this either treats them as numeric node ids or as string
        /// labels, and either ignores unknown nodes or adds them to the Nodeset.
        /// Note that any existing edges in the layer are not removed.
        /// Note also that a deduplication cleanup is done after importing: as edges are added without checking for
        /// multiedges, this will remove any would-be occurrences of multiedges.
        /// The edgelist file:
        /// The columns given by node1col and node2col contain the node identifiers.
        /// The file might have a header: that will be ignored. Lines that can't be parsed are also ignored.
        /// For valued layers, the edgelist must have a column (valueCol) containing the edge value.
        /// If filterCol is non-negative, only lines where that column equals filterValue are imported. This makes
        /// it possible to import edgelists with a column specifying the layer/relation, one layer at a time.
        /// </summary>
        /// <param name="filepath">The filepath to the edgelist</param>
        /// <param name="network">The network (to obtain the Nodeset)</param>
        /// <param name="layerOneMode">The 1-mode layer to import to.</param>
        /// <param name="node1col">The column index of the first node.</param>
        /// <param name="node2col">The column index of the second node.</param>
        /// <param name="valueCol">The column index of the edge value (only for valued layers).</param>
        /// <param name="hasHeader">Whether the first line is a header.</param>
        /// <param name="separator">Character that separates columns</param>
        /// <param name="resolver">The <see cref="NodeIdResolver"/> that translates node identifiers to node ids.</param>
        /// <param name="filterCol">The column index to filter lines on (negative for no filtering).</param>
        /// <param name="filterValue">The value that the filter column must have for the line to be imported.</param>
        /// <exception cref="FileNotFoundException">Thrown if the file is not found</exception>
        /// <exception cref="Exception">Exceptions when something went wrong.</exception>
        internal static void ImportOneModeEdgelist(string filepath, Network network, LayerOneMode layerOneMode, int node1col, int node2col, int valueCol, bool hasHeader, char separator, NodeIdResolver resolver, int filterCol = -1, string? filterValue = null)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: {filepath}");
            using var reader = new StreamReader(filepath);
            string? line;
            int lineNumber = 0;

            bool isValued = layerOneMode.IsValued;
            bool useFilter = filterCol >= 0;
            int maxColIndex = Math.Max(node1col, node2col);
            if (isValued)
                maxColIndex = Math.Max(maxColIndex, valueCol);
            if (useFilter)
                maxColIndex = Math.Max(maxColIndex, filterCol);

            float value = 1;
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

                // Skip rows that don't match the filter
                if (useFilter && !MatchesFilter(columns[filterCol], filterValue))
                    continue;

                // Skip rows where node identifiers or (for valued layers) the edge value can't be parsed
                if (!resolver.IsValidToken(columns[node1col]) || !resolver.IsValidToken(columns[node2col]))
                    continue;
                if (isValued && !float.TryParse(Misc.TrimQuotes(columns[valueCol]), out value))
                    continue;

                // Resolve node ids: skips edges with nodes that are missing (unless missing nodes are added)
                if (!resolver.TryResolve(columns[node1col], out uint node1Id) || !resolver.TryResolve(columns[node2col], out uint node2Id))
                    continue;

                if (isValued)
                    layerOneMode._addEdge(node1Id, node2Id, value);
                else
                    layerOneMode._addEdge(node1Id, node2Id);
            }
            // Deduplicate edges
            layerOneMode._deduplicateEdgesets();
        }

        /// <summary>
        /// Imports a one-mode matrix file into a 1-mode network layer, with node ids given on first row and first column.
        /// Will add new nodes if the resolver is set to add missing nodes. Note that the order of rows and column nodes can be different:
        /// it keeps track of these separately. Uses the normal Network.AddEdge() method: that will take care of the addMissingNodes.
        /// If the resolver is in label mode, row and column headers are node labels rather than node ids: headers that can't
        /// be resolved to nodes (i.e. unknown labels when missing nodes are not added) are ignored.
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
        /// <param name="resolver">The <see cref="NodeIdResolver"/> that translates node identifiers to node ids.</param>
        /// <exception cref="FileNotFoundException">Thrown if the file is not found</exception>
        /// <exception cref="Exception">Exceptions when something went wrong.</exception>
        internal static void ImportOneModeMatrix(string filepath, Network network, LayerOneMode layerOneMode, char separator, NodeIdResolver resolver)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: {filepath}");
            string[,] cells = ReadCells(filepath, separator);
            int nbrRows = cells.GetLength(0), nbrCols = cells.GetLength(1);
            if (nbrRows != nbrCols)
                throw new Exception($"Number of rows ({nbrRows}) different than number of columns ({nbrCols}) in file '{filepath}'");
            uint?[] rowIds = new uint?[nbrRows - 1];
            uint?[] colIds = new uint?[nbrCols - 1];
            for (int i = 1; i < nbrCols; i++)
            {
                colIds[i - 1] = ResolveMatrixHeader(cells[0, i], resolver, filepath, "Column");
                rowIds[i - 1] = ResolveMatrixHeader(cells[i, 0], resolver, filepath, "Row");
            }
            float[,] data = Misc.ConvertStringCellsToFloatCells(cells, 1);
            bool addMissingNodes = resolver.AddMissingNodes;
            bool isDirected = layerOneMode.Directionality == EdgeDirectionality.Directed;
            for (int r = 1; r < nbrRows; r++)
            {
                if (rowIds[r - 1] is not uint rowId)
                    continue;
                for (int c = isDirected ? 1 : r; c < nbrCols; c++)
                    if (data[r - 1, c - 1] != 0 && colIds[c - 1] is uint colId)
                        network.AddEdge(layerOneMode, rowId, colId, data[r - 1, c - 1], addMissingNodes);
            }
        }

        /// <summary>
        /// Imports a two-mode edgelist file into a 2-mode network layer.
        /// </summary>
        /// <remarks>The edgelist file must contain exactly two columns: the first column represents node
        /// ids (as unsigned integers),  and the second column represents affiliation codes (as non-empty strings). Rows
        /// with invalid or empty values are ignored. Note that affiliation codes must be unique - this is not checked/validated here.
        /// Node identifiers are resolved through the provided <see cref="NodeIdResolver"/>, i.e. either as numeric node ids or as
        /// string labels. If filterCol is non-negative, only lines where that column equals filterValue are imported.</remarks>
        /// <param name="filepath">The path to the file containing the two-mode edgelist. The file must have two columns separated by the
        /// specified <paramref name="separator"/>.</param>
        /// <param name="network">The <see cref="Network"/> instance to which the hyperedges will be added.</param>
        /// <param name="layerTwoMode">The two-mode layer of the network where the hyperedges will be added.</param>
        /// <param name="separator">The character used to separate columns in the edgelist file.</param>
        /// <param name="resolver">The <see cref="NodeIdResolver"/> that translates node identifiers to node ids.</param>
        /// <param name="filterCol">The column index to filter lines on (negative for no filtering).</param>
        /// <param name="filterValue">The value that the filter column must have for the line to be imported.</param>
        /// <exception cref="FileNotFoundException">Thrown if the file is not found</exception>
        /// <exception cref="Exception">Exceptions when something went wrong.</exception>
        internal static void ImportTwoModeEdgelist(string filepath, Network network, LayerTwoMode layerTwoMode, int nodeCol, int affCol, char separator, bool hasHeader, NodeIdResolver resolver, int filterCol = -1, string? filterValue = null)
        {
            // Check that file exists
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: {filepath}");
            // Prepare reader, counters, temp vars
            using var reader = new StreamReader(filepath);
            string? line;
            int lineNumber = 0;
            // Get max col index referred to
            int maxColIndex = Math.Max(nodeCol, affCol);
            bool useFilter = filterCol >= 0;
            if (useFilter)
                maxColIndex = Math.Max(maxColIndex, filterCol);
            string hyperedgeName;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (lineNumber == 1 && hasHeader)
                    continue;

                if (string.IsNullOrWhiteSpace(line))
                    continue;
                // Split up the line by separator character
                string[] columns = line.Split(separator);
                // Check that it contains necessary columns
                if (columns.Length <= maxColIndex)
                    continue;
                // Skip rows that don't match the filter
                if (useFilter && !MatchesFilter(columns[filterCol], filterValue))
                    continue;
                // Silent continue if not able to parse node identifier
                if (!resolver.IsValidToken(columns[nodeCol]))
                    continue;
                // Get hyperedge name
                hyperedgeName = Misc.TrimQuotes(columns[affCol]).Trim();
                // Silent continue if no name
                if (hyperedgeName.Length < 1)
                    continue;
                if (!Misc.IsNameWithinBinaryLimit(hyperedgeName))
                    throw new InvalidOperationException($"NameTooLong: {hyperedgeName}");
                // Resolve node id: skips nodes that are missing (unless missing nodes are added)
                if (!resolver.TryResolve(columns[nodeCol], out uint nodeId))
                    continue;
                layerTwoMode._addAffiliation(nodeId, hyperedgeName);
            }
        }

        /// <summary>
        /// Imports a two-mode matrix file into a two-mode network layer, with node ids given on first column and affiliation
        /// (hyperedge) names given on first row. Will add new nodes if addMissingNodes is true. Uses the normal
        /// Network.AddHyperedge() method: that will take care of validation and the addMissingNodes option.
        /// If the resolver is in label mode, row headers are node labels rather than node ids: rows whose labels can't be
        /// resolved to nodes (i.e. unknown labels when missing nodes are not added) are ignored.
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
        /// <param name="resolver">The <see cref="NodeIdResolver"/> that translates node identifiers to node ids.</param>
        /// <exception cref="FileNotFoundException">Thrown if the file is not found</exception>
        /// <exception cref="Exception">Exceptions when something went wrong.</exception>
        internal static void ImportTwoModeMatrix(string filepath, Network network, LayerTwoMode layerTwoMode, char separator, NodeIdResolver resolver)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: {filepath}");
            string[,] cells = ReadCells(filepath, separator);
            int nbrRows = cells.GetLength(0), nbrCols = cells.GetLength(1);
            uint?[] rowIds = new uint?[nbrRows - 1];
            string[] colNames = new string[nbrCols - 1];
            for (int i = 1; i < nbrRows; i++)
                rowIds[i - 1] = ResolveMatrixHeader(cells[i, 0], resolver, filepath, "Row");
            for (int i = 1; i < nbrCols; i++)
            {
                colNames[i - 1] = cells[0, i].Trim();
                if (!Misc.IsNameWithinBinaryLimit(colNames[i - 1]))
                    throw new InvalidOperationException($"NameTooLong: {colNames[i - 1]}");

            }
            float[,] data = Misc.ConvertStringCellsToFloatCells(cells, 1);
            bool addMissingNodes = resolver.AddMissingNodes;
            for (int c = 0; c < colNames.Length; c++)
            {
                for (int r = 0; r < rowIds.Length; r++)
                    if (data[r, c] > 0 && rowIds[r] is uint rowId)
                        network.AddAffiliation(layerTwoMode, colNames[c], rowId, addMissingNodes, true);
            }
        }
        #endregion


        #region Methods (private)
        /// <summary>
        /// Checks whether a column value matches the filter value (ignoring surrounding quotes and whitespace).
        /// </summary>
        /// <param name="columnValue">The value in the filter column.</param>
        /// <param name="filterValue">The value to match.</param>
        /// <returns>True if the values match, false otherwise.</returns>
        private static bool MatchesFilter(string columnValue, string? filterValue)
        {
            return Misc.TrimQuotes(columnValue.Trim()) == (filterValue ?? "");
        }

        /// <summary>
        /// Resolves a row or column header of a matrix file to a node id. In numeric mode, the header must be an
        /// unsigned integer (otherwise an exception is thrown), and the node id is returned as-is: missing nodes are then
        /// handled when adding edges. In label mode, the header is resolved (and possibly added) through the resolver,
        /// returning null if it can't be resolved.
        /// </summary>
        /// <param name="header">The header cell.</param>
        /// <param name="resolver">The <see cref="NodeIdResolver"/> that translates node identifiers to node ids.</param>
        /// <param name="filepath">The filepath (for error messages).</param>
        /// <param name="headerKind">'Row' or 'Column' (for error messages).</param>
        /// <returns>The node id, or null if it couldn't be resolved.</returns>
        /// <exception cref="Exception">Thrown if the header is not an unsigned integer in numeric mode.</exception>
        private static uint? ResolveMatrixHeader(string header, NodeIdResolver resolver, string filepath, string headerKind)
        {
            if (!resolver.IsLabelMode)
            {
                if (!uint.TryParse(header, out uint nodeId))
                    throw new Exception($"{headerKind} header '{header}' in file '{filepath}' not an unsigned integer.");
                return nodeId;
            }
            return resolver.TryResolve(header, out uint id) ? id : null;
        }

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
            {
                for (int c = 0; c < listOfCells[r].Length; c++)
                    cells[r, c] = listOfCells[r][c].Trim();
                for (int c = listOfCells[r].Length; c < nbrCols; c++)
                    cells[r, c] = "";

            }
            return cells;
        }
        #endregion
    }
}
