using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities.Enums;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Class for loading/saving structures in Tab-separated values (TSV) format.
    /// These readers use more verbose/responsive methods than the binaryreader: as this is human-readable
    /// files, it is more likely that a human would be in there messing things up compared to binary files.
    /// Thus, it is better to use the methods that return OperationResult objects, for possible bug-checking.
    /// Also: as TSV files preferably are for smaller networks, the extra time incurred by these validating methods
    /// for adding nodes, edges etc. are called less compared to the presumedly larger binary files.
    /// </summary>
    internal static class FileSerializerTsv
    {
        #region Fields
        /// <summary>
        /// Creates a reusable UTF-8 encoding object (without a Byte Order Mark) for use throughout the class.
        /// </summary>
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        #endregion


        #region Methods (internal)
        /// <summary>
        /// Method for loading a nodeset from file (TSV format), potentially attaching a gzip
        /// decompressor (see <see cref="WrapIfCompressed(Stream, string, FileFormat, CompressionMode)"/>).
        /// Could throw exceptions that must be caught.
        /// </summary>
        /// <param name="filepath">The filepath to the file.</param>
        /// <param name="format">The <see cref="FileFormat"/> of the file</param>
        /// <returns>A <see cref="Nodeset"/> object.</returns>
        internal static Nodeset LoadNodesetFromFile(string filepath, FileFormat format)
        {
            using var fileStream = File.OpenRead(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, format, CompressionMode.Decompress);
            using var buffered = new BufferedStream(stream, 1 << 20);
            using var reader = new StreamReader(buffered, Utf8NoBom);
            return ReadNodesetFromFile(filepath, reader);
        }

        /// <summary>
        /// Method for saving a nodeset to file (TSV format), potentially attaching a gzip compressor
        /// (see <see cref="WrapIfCompressed(Stream, string, FileFormat, CompressionMode)"/>).
        /// Could throw exceptions that must be caught.
        /// </summary>
        /// <param name="nodeset">The Nodeset to save to file.</param>
        /// <param name="filepath">The filepath to save to.</param>
        /// <param name="format">The <see cref="FileFormat"/> of the file</param>
        internal static void SaveNodesetToFile(Nodeset nodeset, string filepath, FileFormat format)
        {
            using var fileStream = File.Create(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, format, CompressionMode.Compress);
            using var writer = new StreamWriter(stream, Utf8NoBom);
            WriteNodesetToFile(nodeset, writer);
            nodeset.Filepath = filepath;
            nodeset.IsModified = false;
        }

        /// <summary>
        /// Method for loading a Network from file (TSV format). Will always also
        /// load the specified Nodeset object, which must be specified in the file.
        /// Always attaches a buffered reader with the size 1 MB (1<<20).
        /// Could throw exceptions that must be caught.
        /// </summary>
        /// <param name="filepath">The filepath to the file.</param>
        /// <returns>A StructureResult object containing the Network object, and possibly also a Nodeset object.</returns>
        internal static StructureResult LoadNetworkFromFile(string filepath, FileFormat format, bool packLayers = false)
        {
            using var fileStream = File.OpenRead(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, format, CompressionMode.Decompress);
            using var buffered = new BufferedStream(stream, 1 << 20);
            using var reader = new StreamReader(buffered, Utf8NoBom);
            return ReadNetworkFromFile(filepath, reader, packLayers);
        }

        /// <summary>
        /// Method for saving a Network to file (TSV format). Could possibly also
        /// save a Nodeset object if the nodesetFileReference is specified.
        /// </summary>
        /// <param name="network">The Network to save to file.</param>
        /// <param name="filepath">The filepath to save the Network to.</param>
        /// <param name="nodesetFileReference">The filepath to save the Nodeset of the Network to.</param>
        internal static void SaveNetworkToFile(Network network, string filepath, FileFormat format)
        {
            using var fileStream = File.Create(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, format, CompressionMode.Compress);
            using var writer = new StreamWriter(stream, Utf8NoBom);
            WriteNetworkToFile(network, writer, network.Nodeset.Filepath);
            network.Filepath = filepath;
            network.IsModified = false;
        }
        #endregion


        #region Methods (private)
        /// <summary>
        /// Support method to potentially attach a GZipStream around the existing stream.
        /// Checks the filepath: if it ends with .gz, then the GZip is attached.
        /// </summary>
        /// <param name="stream">The file stream.</param>
        /// <param name="filepath">The filepath.</param>
        /// <param name="mode">The used CompressionMode</param>
        /// <returns>The original stream or the GZip stream.</returns>
        private static Stream WrapIfCompressed(Stream stream, string filepath, FileFormat format, CompressionMode mode)
            => format == FileFormat.TsvGzip ? new GZipStream(stream, mode) : stream;
        

        /// <summary>
        /// Support method to write a Network object to file, and possibly also its Nodeset object.
        /// </summary>
        /// <param name="network">The Network object to save to file.</param>
        /// <param name="writer">The stream writer.</param>
        /// <param name="nodesetFileReference">Filepath to where to save the Nodeset object (optional).</param>
        private static void WriteNetworkToFile(Network network, StreamWriter writer, string? nodesetFileReference)
        {
            writer.WriteLine("# Network Metadata");
            writer.WriteLine($"Name: {network.Name}");

            writer.WriteLine($"NodesetFile: {Path.GetFileName(network.Nodeset.Filepath)}");
            var sb = new StringBuilder();
            foreach ((string layerName, ILayer layer) in network.Layers)
            {
                writer.WriteLine();
                writer.WriteLine("# Layer");
                if (layer is ILayerOneMode layerOneMode)
                {
                    writer.WriteLine($"LayerMode: 1");
                    writer.WriteLine($"LayerName: {layerOneMode.Name}");
                    writer.WriteLine($"Directionality: {layerOneMode.Directionality.ToString().ToLower()}");
                    writer.WriteLine($"ValueType: {layerOneMode.EdgeValueType.ToString().ToLower()}");
                    writer.WriteLine($"Selfties: {layerOneMode.Selfties.ToString().ToLower()}");

                    bool isValued = layerOneMode.IsValued;

                    foreach (var (egoId, alters, values) in layerOneMode.GetAllEgoData())
                    {
                        ReadOnlySpan<uint> alterSpan = alters.Span;
                        if (alterSpan.IsEmpty)
                            continue;
                        sb.Clear();
                        sb.Append(egoId);
                        if (isValued)
                        {
                            ReadOnlySpan<float> valSpan = values.Span;
                            for (int k = 0; k < alterSpan.Length; k++)
                                sb.Append($"\t{alterSpan[k]};{valSpan[k].ToString(CultureInfo.InvariantCulture)}");
                        }
                        else
                        {
                            for (int k = 0; k < alterSpan.Length; k++)
                                sb.Append($"\t{alterSpan[k]}");
                        }
                        writer.WriteLine(sb.ToString());
                    }
                }
                else if (layer is ILayerTwoMode layerTwoMode)
                {
                    writer.WriteLine($"LayerMode: 2");
                    writer.WriteLine($"LayerName: {layerTwoMode.Name}");

                    foreach (var (hyperName, nodeIds) in layerTwoMode.GetAllHyperedgeData())
                    {
                        sb.Clear();
                        sb.Append(hyperName);
                        if (nodeIds.Length > 0)
                            sb.Append("\t" + string.Join("\t", nodeIds));
                        writer.WriteLine(sb.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Internal method to read a Network (and its Nodeset) from a tsv file. Note that the Nodeset is alays loaded.
        /// </summary>
        /// <param name="filepath">Filepath to the file.</param>
        /// <param name="reader">The StreamReader object</param>
        /// <returns>A StructureResult object containing the Network object, and possibly also a Nodeset object.</returns>
        private static StructureResult ReadNetworkFromFile(string filepath, StreamReader reader, bool compactLayers)
        {
            string networkName = "";
            Nodeset? nodeset = null;
            string? line;

            var collectedLayers = new Dictionary<string, ILayer>();
            ILayer? currentLayer = null;
            int pendingMode = 1;
            string? pendingName = null;
            EdgeDirectionality pendingDir = EdgeDirectionality.Undirected;
            EdgeType pendingValueType = EdgeType.Binary;
            bool pendingSelfties = false;

            void FlushIfNeeded()
            {
                if (currentLayer == null && pendingName!=null)
                    currentLayer = pendingMode == 1 ?
                        new LayerOneMode(pendingName, pendingDir, pendingValueType, pendingSelfties) :
                        new LayerTwoMode { Name = pendingName };
                if (currentLayer!=null)
                {
                    collectedLayers.Add(currentLayer.Name, compactLayers ? Misc.PackLayer(currentLayer) : currentLayer);
                    currentLayer = null;
                }
                pendingName = null;
            }

            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;
                if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
                {
                    networkName = line.Substring("Name:".Length).Trim();
                    continue;
                }
                if (line.StartsWith("NodesetFile:", StringComparison.OrdinalIgnoreCase))
                {
                    string nodesetFilename = line.Substring("NodesetFile:".Length).Trim();
                    if (nodesetFilename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                        || nodesetFilename.Contains('/') || nodesetFilename.Contains('\\'))
                        throw new InvalidDataException("Nodeset filename in network file contains invalid characters.");

                    string networkDir = Path.GetDirectoryName(Path.GetFullPath(filepath))!;
                    string resolvedNodesetPath = Path.Combine(networkDir, nodesetFilename);

                    if (!File.Exists(resolvedNodesetPath))
                        throw new FileNotFoundException(
                            $"Nodeset file '{nodesetFilename}' not found in '{networkDir}'. The nodeset file must be in the same directory as the network file.");

                    FileFormat nodesetFormat = Misc.GetFileFormatFromFileEnding(resolvedNodesetPath);
                    nodeset = LoadNodesetFromFile(resolvedNodesetPath, nodesetFormat);
                    nodeset.IsModified = false;
                    continue;
                }
                if (line.StartsWith("LayerName:", StringComparison.OrdinalIgnoreCase))
                {
                    FlushIfNeeded();
                    pendingName = line.Substring("LayerName:".Length).Trim();
                    continue;
                }
                if (line.Contains(':'))
                {
                    if (line.StartsWith("LayerMode:", StringComparison.OrdinalIgnoreCase))
                        pendingMode = line.Substring("LayerMode:".Length).Trim().Equals("2") ? 2 : 1;
                    else if (line.StartsWith("Directionality:", StringComparison.OrdinalIgnoreCase))
                        pendingDir = line.Substring("Directionality:".Length).Trim().ToLower().Equals("directed") ? EdgeDirectionality.Directed : EdgeDirectionality.Undirected;
                    else if (line.StartsWith("ValueType:", StringComparison.OrdinalIgnoreCase))
                        pendingValueType = line.Substring("ValueType:".Length).Trim().ToLower().Equals("valued") ? EdgeType.Valued : EdgeType.Binary;
                    else if (line.StartsWith("Selfties:", StringComparison.OrdinalIgnoreCase))
                        pendingSelfties = line.Substring("Selfties:".Length).Trim().ToLower().Equals("true");
                    continue;
                }

                if (currentLayer == null && pendingName != null)
                {
                    currentLayer = pendingMode == 1
                        ? new LayerOneMode(pendingName, pendingDir, pendingValueType, pendingSelfties)
                        : new LayerTwoMode { Name = pendingName };
                    pendingName = null;
                }
                if (currentLayer is LayerOneMode layerOneMode)
                {
                    var parts = line.Split('\t');
                    if (parts.Length == 0)
                        continue;
                    if (!uint.TryParse(parts[0], out uint ego))
                        continue;

                    if (layerOneMode.IsBinary)
                    {
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (string.IsNullOrWhiteSpace(parts[i]))
                                continue;
                            if (uint.TryParse(parts[i], out uint partnerNodeId))
                                layerOneMode.AddEdge(ego, partnerNodeId);
                        }
                    }
                    else
                    {
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (string.IsNullOrWhiteSpace(parts[i]))
                                continue;
                            var subparts = parts[i].Split(';', 2);
                            if (subparts.Length < 2)
                                continue;

                            if (!uint.TryParse(subparts[0], out uint alter))
                                continue;

                            if (!float.TryParse(subparts[1], CultureInfo.InvariantCulture, out float val))
                                continue;

                            layerOneMode.AddEdge(ego, alter, val);
                        }
                    }
                }
                else if (currentLayer is LayerTwoMode layerTwoMode)
                {
                    var parts = line.Split('\t', 2);
                    if (parts.Length == 0)
                        continue;
                    string hyperName = parts[0];
                    if (parts.Length > 1)
                        layerTwoMode._addHyperedge(hyperName, Misc.SplitStringToUintArray(parts[1], '\t'));
                    else
                        layerTwoMode._addHyperedge(hyperName);
                }
            }
            FlushIfNeeded();
            if (nodeset == null)
                throw new InvalidDataException("Nodeset file reference must be specified before layer definitions.");

            var network = new Network(networkName, nodeset);
            foreach (var kvp in collectedLayers)
                network.Layers.Add(kvp.Key, kvp.Value);
            network.Filepath = filepath;
            network.IsModified = false;
            nodeset.IsModified = false;
            return new StructureResult(network, new Dictionary<string, IStructure>
            {
                { "nodeset", nodeset }
            });
        }

        /// <summary>
        /// Support method to write a Nodeset object to file.
        /// </summary>
        /// <param name="nodeset">The Nodeset object to save to file.</param>
        /// <param name="writer">The stream writer.</param>
        private static void WriteNodesetToFile(Nodeset nodeset, StreamWriter writer)
        {
            var attributeDefs = nodeset.NodeAttributeDefinitionManager.GetAllNodeAttributeDefinitions().ToList();
            int nbrAttributes = attributeDefs.Count;
            var header = nodeset.Name;
            if (attributeDefs.Count > 0)
                header += "\t" + string.Join("\t", attributeDefs.Select(a => $"{a.AttrName}:{a.AttrType}"));
            writer.WriteLine(header);
            foreach (uint node in nodeset.NodeIdArray)
            {
                var row = new List<string> { node.ToString() };
                foreach (var attrDef in attributeDefs)
                {
                    string cellValue = "";

                    if (attrDef.AttrType == NodeAttributeType.String)
                    {
                        var strResult = nodeset.GetNodeAttributeString(node, attrDef.AttrName);
                        cellValue = strResult.Success ? strResult.Value! : "";
                    }
                    else
                    {
                        var attr = nodeset.GetNodeAttribute(node, attrDef.AttrName);
                        cellValue = attr.Success ? attr.Value.Value.ToString(attr.Value.Type) : "";
                    }
                    row.Add(cellValue);
                }
                writer.WriteLine(string.Join("\t", row));
            }
        }

        /// <summary>
        /// Support method to read a Nodeset object from file in the TSV format.
        /// </summary>
        /// <param name="filepath">Filepath to the file.</param>
        /// <param name="reader">The StreamReader object</param>
        /// <returns>A Nodeset object.</returns>
        private static Nodeset ReadNodesetFromFile(string filepath, StreamReader reader)
        {
            var header = reader.ReadLine() ??
                throw new InvalidDataException("Missing header line.");
            var columns = header.Split('\t');
            var nodeset = new Nodeset { Name = columns[0], Filepath = filepath, IsModified = false };
            int nbrCols = columns.Length;
            string[] attributeNames = new string[nbrCols - 1];
            for (int i = 1; i < nbrCols; i++)
            {
                var parts = columns[i].Split(':', 2);
                if (parts.Length != 2) throw new InvalidDataException($"Invalid column header: {columns[i]}");
                attributeNames[i - 1] = parts[0];
                nodeset.DefineNodeAttribute(parts[0], parts[1]);
            }
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var parts = line.Split('\t');
                if (!uint.TryParse(parts[0], out uint nodeId))
                    continue;
                nodeset.AddNode(nodeId);
                for (int i = 1; i < parts.Length && i <= nbrCols; i++)
                {
                    var val = parts[i];
                    if (string.IsNullOrEmpty(val)) continue;
                    nodeset.SetNodeAttribute(nodeId, attributeNames[i - 1], val);
                }
            }
            nodeset.IsModified = false;
            nodeset.Filepath = filepath;
            return nodeset;
        }
        #endregion
    }
}
