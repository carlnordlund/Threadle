using System.Globalization;
using System.Text;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Class for importing and exporting networks in the Pajek .net format (http://mrvar.fdv.uni-lj.si/pajek/),
    /// as used by Pajek and read/written by many other tools (igraph, UCINET, NetworkX, ...). Only the classic
    /// '*Vertices'/'*Arcs'/'*Edges' triplet-list sections are supported — adjacency-list sections ('*Arcslist',
    /// '*Edgeslist') are rejected, and other optional sections ('*Partition', '*Vector', ...) are ignored.
    /// Mapping between Pajek .net and Threadle:
    /// - Pajek vertex ids are always a dense 1..N sequence, so they are used directly as Threadle node ids. A
    ///   vertex's quoted (or bare) label, if present, is stored in a string node attribute (named by 'labelAttr').
    /// - Pajek has no general node-attribute mechanism beyond that single label, so nothing else is imported for
    ///   nodes, and nothing but that label is exported for them either.
    /// - Each '*Arcs' section becomes a directed 1-mode layer; each '*Edges' section becomes an undirected one. A
    ///   section's optional ':k "name"' suffix becomes the layer name; an unnamed section is named after the file
    ///   (if it is the only 1-mode section in the file) or 'arcsN'/'edgesN' otherwise. A layer is valued if any of
    ///   its edges carries a third (weight) column.
    /// - Pajek .net has no hyperedge concept, so 2-mode layers are not exported, and the format is always
    ///   imported/exported as the whole network (no single-layer subset, unlike graphml/gexf).
    /// On export, since Threadle node ids need not be a dense 1..N sequence, nodes are renumbered to Pajek's
    /// required 1..N range (in ascending node-id order), with the original Threadle node id written as each
    /// vertex's label so it stays visible in the file; re-importing such a file therefore creates fresh sequential
    /// node ids rather than recovering the originals, same as Threadle's graphml importer does for non-integer ids.
    /// </summary>
    internal static class FileSerializerPajek
    {
        #region Methods (internal)
        /// <summary>
        /// Imports a Pajek .net file as a new Network (with a new Nodeset).
        /// </summary>
        /// <param name="filepath">The Pajek .net file.</param>
        /// <param name="labelAttr">Name of the string node attribute to store each vertex's label in (null or empty: labels are discarded).</param>
        /// <returns>A <see cref="StructureResult"/> with the Network, and the Nodeset as an additional structure.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the file is not found.</exception>
        /// <exception cref="InvalidDataException">Thrown if the file can not be imported.</exception>
        internal static StructureResult Import(string filepath, string? labelAttr)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: {filepath}");

            string[] lines = File.ReadAllLines(filepath);
            int lineIndex = FindVerticesSection(lines, out int nbrVertices);
            string?[] labels = ReadVertices(lines, ref lineIndex, nbrVertices);
            List<EdgeBlock> blocks = ReadEdgeBlocks(lines, lineIndex, nbrVertices);

            string name = Path.GetFileNameWithoutExtension(filepath);
            var nodeset = new Nodeset(name + "_nodeset");
            for (int vid = 1; vid <= nbrVertices; vid++)
                nodeset._addNodeWithoutAttribute((uint)vid);
            SetLabels(nodeset, labels, labelAttr);

            var network = new Network(name, nodeset);
            CreateLayers(network, blocks, name);

            network.IsModified = true;
            nodeset.IsModified = true;
            return new StructureResult(network, new Dictionary<string, IStructure>
            {
                { "nodeset", nodeset }
            });
        }

        /// <summary>
        /// Exports a network to a Pajek .net file. Always exports the whole network (no single-layer subset — the
        /// format has no concept of one): every 1-mode layer becomes its own '*Arcs'/'*Edges' section; 2-mode
        /// layers are skipped, since Pajek .net has no hyperedge concept.
        /// </summary>
        /// <param name="network">The network to export.</param>
        /// <param name="filepath">The filepath to write to.</param>
        internal static void Export(Network network, string filepath)
        {
            Nodeset nodeset = network.Nodeset;
            uint[] sortedIds = [.. nodeset.NodeIdArray.OrderBy(id => id)];
            var pajekId = new Dictionary<uint, int>(sortedIds.Length);
            for (int i = 0; i < sortedIds.Length; i++)
                pajekId[sortedIds[i]] = i + 1;

            var oneModeLayers = network.Layers.Values.OfType<ILayerOneMode>().ToList();
            bool singleLayer = oneModeLayers.Count == 1;

            using var writer = new StreamWriter(filepath, false, new UTF8Encoding(false));
            writer.WriteLine($"*Vertices {sortedIds.Length}");
            foreach (uint nodeId in sortedIds)
                writer.WriteLine($"{pajekId[nodeId]} \"{nodeId}\"");

            int relationIndex = 0;
            foreach (var layer in oneModeLayers)
            {
                relationIndex++;
                string header = layer.IsDirectional ? "*Arcs" : "*Edges";
                writer.WriteLine(singleLayer ? header : $"{header} :{relationIndex} \"{layer.Name}\"");

                foreach (var (egoId, alters, values) in layer.GetAllEgoData())
                {
                    ReadOnlySpan<uint> alterSpan = alters.Span;
                    ReadOnlySpan<float> valueSpan = values.Span;
                    for (int i = 0; i < alterSpan.Length; i++)
                        WriteEdgeLine(writer, pajekId[egoId], pajekId[alterSpan[i]], layer.IsValued ? valueSpan[i] : 1, layer.IsValued);
                }
                // GetAllEgoData() never yields self-loops for symmetric layers: add them explicitly
                if (layer.IsSymmetric && layer.Selfties)
                    foreach (uint nodeId in sortedIds)
                        if (layer.CheckEdgeExists(nodeId, nodeId))
                            WriteEdgeLine(writer, pajekId[nodeId], pajekId[nodeId], layer.GetEdgeValue(nodeId, nodeId), layer.IsValued);
            }
        }
        #endregion


        #region Nested types
        /// <summary>A parsed '*Arcs'/'*Edges' section, with endpoints given as 1-based Pajek vertex ids.</summary>
        private sealed class EdgeBlock
        {
            internal bool Directed;
            internal string? Name;
            internal readonly List<(int From, int To, float Weight, bool HasWeight)> Edges = [];
        }
        #endregion


        #region Methods (private): parsing
        /// <summary>
        /// Tokenizes a line, keeping quoted substrings (which may contain spaces) as single tokens.
        /// </summary>
        private static List<string> Tokenize(string line)
        {
            var tokens = new List<string>();
            int i = 0, n = line.Length;
            while (i < n)
            {
                while (i < n && char.IsWhiteSpace(line[i])) i++;
                if (i >= n) break;
                if (line[i] == '"')
                {
                    int start = ++i;
                    while (i < n && line[i] != '"') i++;
                    tokens.Add(line[start..i]);
                    if (i < n) i++;
                }
                else
                {
                    int start = i;
                    while (i < n && !char.IsWhiteSpace(line[i])) i++;
                    tokens.Add(line[start..i]);
                }
            }
            return tokens;
        }

        /// <summary>
        /// Scans for the '*Vertices' section, returning the index of the first vertex line and the vertex count.
        /// </summary>
        private static int FindVerticesSection(string[] lines, out int nbrVertices)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var tokens = Tokenize(lines[i]);
                if (tokens.Count == 0) continue;
                if (tokens[0].Equals("*Vertices", StringComparison.OrdinalIgnoreCase))
                {
                    if (tokens.Count < 2 || !int.TryParse(tokens[1], NumberStyles.None, CultureInfo.InvariantCulture, out nbrVertices))
                        throw new InvalidDataException("'*Vertices' line must be followed by a non-negative vertex count.");
                    return i + 1;
                }
            }
            throw new InvalidDataException("No '*Vertices' section found: not a valid Pajek .net file.");
        }

        /// <summary>
        /// Reads the 'nbrVertices' vertex lines following '*Vertices', returning each vertex's label (1-indexed;
        /// index 0 is unused). Advances 'lineIndex' past the lines consumed.
        /// </summary>
        private static string?[] ReadVertices(string[] lines, ref int lineIndex, int nbrVertices)
        {
            var labels = new string?[nbrVertices + 1];
            int read = 0;
            while (read < nbrVertices && lineIndex < lines.Length)
            {
                var tokens = Tokenize(lines[lineIndex]);
                lineIndex++;
                if (tokens.Count == 0) continue;
                if (!int.TryParse(tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out int vid) || vid < 1 || vid > nbrVertices)
                    throw new InvalidDataException($"Invalid or out-of-range vertex id on line {lineIndex}: '{tokens[0]}'.");
                if (tokens.Count > 1)
                    labels[vid] = tokens[1];
                read++;
            }
            if (read < nbrVertices)
                throw new InvalidDataException($"Expected {nbrVertices} vertex lines after '*Vertices', found {read}.");
            return labels;
        }

        /// <summary>
        /// Reads every '*Arcs'/'*Edges' section from 'lineIndex' onward. Unrecognized sections ('*Partition',
        /// '*Vector', a repeated '*Vertices', ...) are skipped, along with their body lines. '*Arcslist'/
        /// '*Edgeslist' (adjacency-list style) are explicitly rejected rather than silently ignored, since they
        /// would otherwise silently drop edges the caller expects to see imported.
        /// </summary>
        private static List<EdgeBlock> ReadEdgeBlocks(string[] lines, int lineIndex, int nbrVertices)
        {
            var blocks = new List<EdgeBlock>();
            EdgeBlock? current = null;

            for (; lineIndex < lines.Length; lineIndex++)
            {
                var tokens = Tokenize(lines[lineIndex]);
                if (tokens.Count == 0) continue;
                string head = tokens[0];
                if (head.Length > 0 && head[0] == '*')
                {
                    current = null;
                    if (head.Equals("*Arcslist", StringComparison.OrdinalIgnoreCase) || head.Equals("*Edgeslist", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Pajek adjacency-list sections ('*Arcslist'/'*Edgeslist') are not supported; only '*Arcs'/'*Edges' triplet-list sections are.");
                    bool isArcs = head.Equals("*Arcs", StringComparison.OrdinalIgnoreCase);
                    bool isEdges = head.Equals("*Edges", StringComparison.OrdinalIgnoreCase);
                    if (isArcs || isEdges)
                    {
                        current = new EdgeBlock { Directed = isArcs, Name = tokens.Count > 2 ? tokens[2] : null };
                        blocks.Add(current);
                    }
                    continue;
                }
                if (current == null || tokens.Count < 2)
                    continue;
                if (!int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int from) ||
                    !int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int to))
                    throw new InvalidDataException($"Invalid edge endpoints on line {lineIndex + 1}: '{lines[lineIndex]}'.");
                if (from < 1 || from > nbrVertices || to < 1 || to > nbrVertices)
                    throw new InvalidDataException($"Edge references a vertex id outside 1..{nbrVertices} on line {lineIndex + 1}: '{lines[lineIndex]}'.");
                float weight = 1f;
                bool hasWeight = tokens.Count > 2 && float.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out weight);
                current.Edges.Add((from, to, hasWeight ? weight : 1f, hasWeight));
            }
            return blocks;
        }
        #endregion


        #region Methods (private): building the network
        /// <summary>
        /// Defines the label node attribute (if any vertex has a label and 'labelAttr' is given) and sets it.
        /// </summary>
        private static void SetLabels(Nodeset nodeset, string?[] labels, string? labelAttr)
        {
            if (string.IsNullOrWhiteSpace(labelAttr) || !Array.Exists(labels, l => l != null))
                return;
            var defineResult = nodeset.DefineNodeAttribute(labelAttr, NodeAttributeType.String);
            if (!defineResult.Success)
                throw new InvalidDataException(defineResult.Message);
            byte attrIndex = defineResult.Value;
            for (int vid = 1; vid < labels.Length; vid++)
                if (labels[vid] is string label)
                    nodeset.SetNodeAttribute((uint)vid, attrIndex, new NodeAttributeValue(nodeset.GetOrAddStringToPool(label)));
        }

        /// <summary>
        /// Creates one 1-mode layer per parsed edge block and adds its edges.
        /// </summary>
        private static void CreateLayers(Network network, List<EdgeBlock> blocks, string fileName)
        {
            int arcCounter = 0, edgeCounter = 0;
            foreach (var block in blocks)
            {
                string layerName;
                if (!string.IsNullOrWhiteSpace(block.Name))
                    layerName = block.Name!.Trim();
                else if (blocks.Count == 1)
                    layerName = fileName;
                else if (block.Directed)
                    layerName = $"arcs{++arcCounter}";
                else
                    layerName = $"edges{++edgeCounter}";

                bool valued = block.Edges.Exists(e => e.HasWeight);
                bool selfties = block.Edges.Exists(e => e.From == e.To);
                var layer = new LayerOneMode(layerName, block.Directed ? EdgeDirectionality.Directed : EdgeDirectionality.Undirected, valued ? EdgeType.Valued : EdgeType.Binary, selfties);
                var addResult = network.AddLayer(layerName, layer);
                if (!addResult.Success)
                    throw new InvalidDataException(addResult.Message);

                foreach (var edge in block.Edges)
                    layer._addEdge((uint)edge.From, (uint)edge.To, edge.Weight);
                layer._deduplicateEdgesets();
            }
        }
        #endregion


        #region Methods (private): writing
        /// <summary>Writes an edge line, including the weight column only for valued layers.</summary>
        private static void WriteEdgeLine(StreamWriter writer, int from, int to, float weight, bool writeWeight)
        {
            if (writeWeight)
                writer.WriteLine($"{from} {to} {weight.ToString("R", CultureInfo.InvariantCulture)}");
            else
                writer.WriteLine($"{from} {to}");
        }
        #endregion
    }
}