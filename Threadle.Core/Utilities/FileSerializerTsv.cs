using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Class for loading/saving structures in Tab-separated values (TSV) format
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
        /// Method for loading a nodeset from file (TSV format).
        /// Could throw exceptions that must be caught.
        /// </summary>
        /// <param name="filepath">The filepath to the file.</param>
        /// <returns>A Nodeset object.</returns>
        internal static Nodeset LoadNodesetFromFile(string filepath, FileFormat format)
        {
            using var fileStream = File.OpenRead(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, format, CompressionMode.Decompress);
            using var reader = new StreamReader(stream, Utf8NoBom);

            // Passing filepath only to set the Filepath property for nodeset
            Nodeset nodeset = ReadNodesetFromFile(filepath, reader);
            //nodeset.Filepath = filepath;
            //nodeset.IsModified = false;
            return nodeset;
        }

        /// <summary>
        /// Method for saving a nodeset to file (TSV format).
        /// Could throw exceptions that must be caught.
        /// </summary>
        /// <param name="nodeset">The Nodeset to save to file.</param>
        /// <param name="filepath">The filepath to save to.</param>
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
        /// Method for loading a Network from file (TSV format). Could possibly also
        /// load a Nodeset object if that is specified in the file.
        /// Could throw exceptions that must be caught.
        /// </summary>
        /// <param name="filepath">The filepath to the file.</param>
        /// <returns>A StructureResult object containing the Network object, and possibly also a Nodeset object.</returns>
        internal static StructureResult LoadNetworkFromFile(string filepath, FileFormat format)
        {
            using var fileStream = File.OpenRead(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, format, CompressionMode.Decompress);
            using var reader = new StreamReader(stream, Utf8NoBom);

            return ReadNetworkFromFile(filepath, reader);
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
        {
            return format == FileFormat.TsvGzip ? new GZipStream(stream, mode) : stream;

            //return filepath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            //    ? new GZipStream(stream, mode)
            //    : stream;
        }

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
            //if (!string.IsNullOrEmpty(nodesetFileReference))
                
            writer.WriteLine($"NodesetFile: {network.Nodeset.Filepath}");
            var sb = new StringBuilder();
            foreach ((string layerName, ILayer layer) in network.Layers)
            {
                writer.WriteLine();
                writer.WriteLine("# Layer");
                if (layer is LayerOneMode layerOneMode)
                {
                    writer.WriteLine($"LayerMode: 1");
                    writer.WriteLine($"LayerName: {layerOneMode.Name}");
                    writer.WriteLine($"Directionality: {layerOneMode.Directionality.ToString().ToLower()}");
                    writer.WriteLine($"ValueType: {layerOneMode.EdgeValueType.ToString().ToLower()}");
                    writer.WriteLine($"Selfties: {layerOneMode.Selfties.ToString().ToLower()}");
                    string nodelist = string.Empty;
                    foreach ((uint nodeId, IEdgeset edgeset) in layerOneMode.Edgesets)
                        if ((nodelist = edgeset.GetNodelistAlterString(nodeId)).Length > 0)
                            writer.WriteLine($"{nodeId}{nodelist}");
                }
                else if (layer is LayerTwoMode layerTwoMode)
                {
                    writer.WriteLine($"LayerMode: 2");
                    writer.WriteLine($"LayerName: {layerTwoMode.Name}");

                    foreach ((string hyperName, Hyperedge hyperedge) in layerTwoMode.AllHyperEdges)
                    {
                        sb.Clear();
                        sb.Append(hyperName);
                        if (hyperedge.NodeIds.Count > 0)
                            sb.Append("\t" + string.Join("\t", hyperedge.NodeIds));
                        writer.WriteLine(sb.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Support method to read a Network object from file, and possibly also its Nodeset object.
        /// </summary>
        /// <param name="filepath">Filepath to the file.</param>
        /// <param name="reader">The StreamReader object</param>
        /// <returns>A StructureResult object containing the Network object, and possibly also a Nodeset object.</returns>
        private static StructureResult ReadNetworkFromFile(string filepath, StreamReader reader)
        {
            var network = new Network("");
            Nodeset? nodeset = null;
            string? nodesetFileReference = null;
            string? line;
            ILayer? currentLayer = null;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;
                if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
                {
                    network.Name = line.Substring("Name:".Length).Trim();
                    continue;
                }
                if (line.StartsWith("NodesetFile:", StringComparison.OrdinalIgnoreCase))
                {
                    nodesetFileReference = line.Substring("NodesetFile:".Length).Trim();
                    continue;
                }
                if (line.StartsWith("LayerMode:", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentLayer != null)
                        network.Layers.Add(currentLayer.Name, currentLayer);
                    string layerModeStr = line.Substring("LayerMode:".Length).Trim();
                    if (layerModeStr.Equals("1"))
                        currentLayer = new LayerOneMode();
                    else if (layerModeStr.Equals("2"))
                        currentLayer = new LayerTwoMode();
                    continue;
                }
                if (currentLayer != null && line.Contains(":"))
                {
                    if (line.StartsWith("LayerName:", StringComparison.OrdinalIgnoreCase))
                        currentLayer.Name = line.Substring("LayerName:".Length).Trim();
                    else if (currentLayer is LayerOneMode currentLayerOneMode)
                    {
                        if (line.StartsWith("Directionality:", StringComparison.OrdinalIgnoreCase))
                        {
                            string dirString = line.Substring("Directionality:".Length).Trim().ToLower();
                            currentLayerOneMode.Directionality = dirString.Equals("directed") ? EdgeDirectionality.Directed : EdgeDirectionality.Undirected;
                            currentLayerOneMode.TryInitFactory();
                        }
                        else if (line.StartsWith("ValueType:", StringComparison.OrdinalIgnoreCase))
                        {
                            var valString = line.Substring("ValueType:".Length).Trim().ToLower();
                            currentLayerOneMode.EdgeValueType = valString switch
                            {
                                "valued" => EdgeType.Valued,
                                _ => EdgeType.Binary
                            };
                            currentLayerOneMode.TryInitFactory();
                        }
                        else if (line.StartsWith("Selfties:", StringComparison.OrdinalIgnoreCase))
                        {
                            string selftiesStr = line.Substring("Selfties:".Length).Trim().ToLower();
                            currentLayerOneMode.Selfties = selftiesStr.Equals("true");
                        }
                    }
                    continue;
                }
                if (currentLayer is LayerOneMode layerOneMode)
                {
                    var parts = line.Split('\t');
                    if (parts.Length == 0)
                        continue;
                    uint ego = uint.Parse(parts[0]);

                    if (layerOneMode.EdgeValueType == EdgeType.Binary)
                    {
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (string.IsNullOrWhiteSpace(parts[i]))
                                continue;
                            uint alter = uint.Parse(parts[i]);
                            layerOneMode.AddEdge(ego, alter);
                        }
                    }
                    else
                    {
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (string.IsNullOrWhiteSpace(parts[i]))
                                continue;
                            var subparts = parts[i].Split(';', 2);
                            uint alter = uint.Parse(subparts[0]);
                            float val = Misc.FixConnectionValue(float.Parse(subparts[1], CultureInfo.InvariantCulture), layerOneMode.EdgeValueType);
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
                        layerTwoMode.AddHyperedge(hyperName, Misc.NodesIdsStringToArray(parts[1], '\t'));
                    else
                        layerTwoMode.AddHyperedge(hyperName, null);
                }
            }
            if (currentLayer != null)
                network.Layers.Add(currentLayer.Name, currentLayer);
            network.Filepath = filepath;
            network.IsModified = false;

            if (nodesetFileReference != null)
            {
                FileFormat nodesetFormat = Misc.GetFileFormatFromFileEnding(nodesetFileReference);
                nodeset = LoadNodesetFromFile(nodesetFileReference, nodesetFormat);
                network.SetNodeset(nodeset);
                return new StructureResult(network, new Dictionary<string, IStructure>
                {
                    { "nodeset", nodeset}
                });
            }
            else
            {
                HashSet<uint> allIds = network.GetAllIdsMentioned();
                nodeset = new Nodeset(network.Name + "_nodeset", allIds);
                network.SetNodeset(nodeset);
            }
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
                header += "\t" + string.Join("\t", attributeDefs.Select(a => $"{a.Name}:{a.Type}"));
            writer.WriteLine(header);
            foreach (uint node in nodeset.NodeIdArray)
            {
                var row = new List<string> { node.ToString() };
                foreach (var attrDef in attributeDefs)
                {
                    var attr = nodeset.GetNodeAttribute(node, attrDef.Name);
                    row.Add(attr.Success ? attr.Value.ToString() : "");
                }
                writer.WriteLine(string.Join("\t", row));
            }
        }

        /// <summary>
        /// Support method to read a Nodeset object from file.
        /// </summary>
        /// <param name="filepath">Filepath to the file.</param>
        /// <param name="reader">The StreamReader object</param>
        /// <returns>A Nodeset object.</returns>
        private static Nodeset ReadNodesetFromFile(string filepath, StreamReader reader)
        {
            var header = reader.ReadLine() ?? throw new InvalidDataException("Missing header line.");
            var columns = header.Split('\t');
            var nodeset = new Nodeset { Name = columns[0], Filepath = filepath, IsModified = false };
            int nbrCols = columns.Length;
            string[] attributeNames = new string[nbrCols-1];
            for (int i = 1; i < nbrCols; i++)
            {
                var parts = columns[i].Split(':', 2);
                if (parts.Length != 2) throw new InvalidDataException($"Invalid column header: {columns[i]}");
                attributeNames[i - 1] = parts[0];
                nodeset.DefineNodeAttribute(parts[0], parts[1]);
            }
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('\t');
                uint nodeId = uint.Parse(parts[0]);
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
