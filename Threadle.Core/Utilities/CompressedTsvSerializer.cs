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
    public static class CompressedTsvSerializer
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private static Stream WrapIfCompressed(Stream stream, string filepath, CompressionMode mode)
        {
            return filepath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                ? new GZipStream(stream, mode)
                : stream;
        }

        public static void SaveNodesetToFile(Nodeset nodeset, string filepath)
        {
            using var fileStream = File.Create(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, CompressionMode.Compress);
            using var writer = new StreamWriter(stream, Utf8NoBom);            
            WriteNodesetToFile(nodeset, writer);
            nodeset.Filepath = filepath;
            nodeset.IsModified = false;
        }

        public static Nodeset LoadNodesetFromFile(string filepath)
        {
            using var fileStream = File.OpenRead(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, CompressionMode.Decompress);
            using var reader = new StreamReader(stream, Utf8NoBom);

            Nodeset nodeset = ReadNodesetFromFile(reader);
            nodeset.Filepath = filepath;
            nodeset.IsModified = false;
            return nodeset;
        }

        public static void SaveNetworkToFile(Network network, string filepath, string? nodesetFileReference =null)
        {
            using var fileStream = File.Create(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, CompressionMode.Compress);
            using var writer = new StreamWriter(stream, Utf8NoBom);

            WriteNetworkModelToFile(network, writer, nodesetFileReference);
            network.Filepath = filepath;
            network.IsModified = false;
        }

        //internal static void SaveMatrixToFile(MatrixStructure matrix, string filepath)
        //{
        //    using var fileStream = File.Create(filepath);
        //    using var stream = WrapIfCompressed(fileStream, filepath, CompressionMode.Compress);
        //    using var writer = new StreamWriter(stream, Utf8NoBom);

        //    WriteMatrixToFile(matrix, writer);
        //}

        public static StructureResult LoadNetworkFromFile(string filepath)
        {
            using var fileStream = File.OpenRead(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, CompressionMode.Decompress);
            using var reader = new StreamReader(stream, Utf8NoBom);

            return ReadNetworkFromFile(filepath, reader);
        }


        private static void WriteNetworkModelToFile(Network network, StreamWriter writer, string? nodesetFileReference)
        {
            writer.WriteLine("# Network Metadata");
            writer.WriteLine($"Name: {network.Name}");
            if (!string.IsNullOrEmpty(nodesetFileReference))
                writer.WriteLine($"NodesetFile: {nodesetFileReference}");

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
                    writer.WriteLine($"ValueType: {layerOneMode.ValueType.ToString().ToLower()}");
                    writer.WriteLine($"Selfties: {layerOneMode.Selfties.ToString().ToLower()}");

                    // Iterate through all IEdgeSet objects in this layer, get the EdgeSet string for each
                    // and write it to writer. Note that the GetNodelistAlters() returns different kinds of
                    // content depending on what kind of IEdgeSet it is
                    string ret = string.Empty;
                    foreach ((uint nodeId, IEdgeset edgeset) in layerOneMode.Edgesets)
                        if ((ret = edgeset.GetNodelistAlterString(nodeId)).Length > 0)
                            writer.WriteLine($"{nodeId}\t{ret}");

                    // In a way, not so nice to have file-data-generating content in the IEdgeset classes,
                    // as those should strictly be for storing data, not demonstrating








                            //bool directed = (layerOneMode.Directionality == EdgeDirectionality.Directed);
                            //bool binary = (layerOneMode.ValueType == EdgeValueType.Binary);

                            ////foreach ((uint sourceNodeId, IConnectionCollection connectionCollection) in layerOneMode.Connections)
                            //if (layerOneMode.IsBinary)
                            //{

                            //    foreach ((uint sourceNodeId, IEdgeSet edgeset) in layerOneMode.Edgesets)
                            //    {
                            //        if (edgeset.GetNbrOutboundEdges() > 0)
                            //        {
                            //            sb.Clear();
                            //            sb.Append(sourceNodeId);
                            //            foreach (uint partnerNodeId in edgeset.GetOutboundNodeIds())
                            //            //foreach (Connection conn in connectionCollection.GetOutboundConnections())
                            //            {
                            //                if (directed || partnerNodeId >= sourceNodeId)
                            //                    // Might have errors here for valued and signed networks
                            //                    //sb.Append("\t" + partnerNodeId.ToString() + (binary ? "" : ";" + conn.value.ToString()));
                            //                    sb.Append("\t" + partnerNodeId.ToString());
                            //            }
                            //            if (sb.Length > sourceNodeId.ToString().Length)
                            //                writer.WriteLine(sb.ToString());
                            //        }
                            //    }
                            //}
                            //else if (layerOneMode.IsValued)
                            //{
                            //    // NOT GOOD HERE - need to rewrite!

                            //    // Not efficient here: I know that edgeset is valued, so I should get connection structs
                            //    foreach ((uint sourceNodeId, IEdgeSet edgeset) in layerOneMode.Edgesets)
                            //    {
                            //        if (edgeset.GetNbrOutboundEdges() > 0)
                            //        {
                            //            sb.Clear();
                            //            sb.Append(sourceNodeId);
                            //            foreach (uint partnerNodeId in edgeset.GetOutboundNodeIds())
                            //            //foreach (Connection conn in connectionCollection.GetOutboundConnections())
                            //            {
                            //                if (directed || partnerNodeId >= sourceNodeId)
                            //                    // Might have errors here for valued and signed networks
                            //                    //sb.Append("\t" + partnerNodeId.ToString() + (binary ? "" : ";" + conn.value.ToString()));
                            //                    sb.Append("\t" + partnerNodeId.ToString());
                            //            }
                            //            if (sb.Length > sourceNodeId.ToString().Length)
                            //                writer.WriteLine(sb.ToString());
                            //        }
                            //    }


                            //}
                }
                else if (layer is LayerTwoMode layerTwoMode)
                {
                    writer.WriteLine($"LayerMode: 2");
                    writer.WriteLine($"LayerName: {layerTwoMode.Name}");

                    foreach ((string hyperName, HyperEdge hyperedge) in layerTwoMode.AllHyperEdges)
                    {
                        sb.Clear();
                        sb.Append(hyperName);
                        if (hyperedge.nodeIds.Count > 0)
                            sb.Append("\t" + string.Join("\t", hyperedge.nodeIds));
                        writer.WriteLine(sb.ToString());
                    }
                }


                //// Only implemented for 1-mode so far
                //if (!(kv.Value is LayerOneMode layerOneMode))
                //    continue;
                //writer.WriteLine("# Layer");
                //writer.WriteLine($"Layer Name: {layerOneMode.Name}");
                //writer.WriteLine($"Directionality: {layerOneMode.Directionality.ToString().ToLower()}");
                //writer.WriteLine($"ValueType: {layerOneMode.ValueType.ToString().ToLower()}");
                //writer.WriteLine($"Selfties: {layerOneMode.Selfties.ToString().ToLower()}");
                //writer.WriteLine();

                //foreach (var sourceNodeId in layerOneMode.Connections.Keys)
                //{
                //    bool directed = (layerOneMode.Directionality == EdgeDirectionality.Directed);
                //    bool binary = (layerOneMode.ValueType == EdgeValueType.Binary);
                //    sb.Clear();
                //    sb.Append(sourceNodeId);
                //    foreach (Connection conn in layerOneMode.Connections[sourceNodeId].GetOutboundConnections())
                //    {
                //        if (directed || conn.partnerNodeId > sourceNodeId)
                //            sb.Append("\t" + conn.partnerNodeId.ToString() + (binary ? "" : conn.value.ToString()));
                //    }
                //    if (sb.Length > sourceNodeId.ToString().Length)
                //        writer.WriteLine(sb.ToString());
                //}
                //writer.WriteLine();
            }
        }

        private static StructureResult ReadNetworkFromFile(string filepath, StreamReader reader)
        {
            //throw new NotImplementedException();
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
                    {
                        // Working on a previous layer, so save that first
                        network.Layers.Add(currentLayer.Name, currentLayer);
                    }

                    string layerModeStr = line.Substring("LayerMode:".Length).Trim();
                    if (layerModeStr.Equals("1"))
                        currentLayer = new LayerOneMode();
                    else if (layerModeStr.Equals("2"))
                        currentLayer = new LayerTwoMode();
                    continue;
                }
                if (currentLayer != null && line.Contains(":"))
                {
                    // Working with a layer and got some parameters to set for this layer
                    if (line.StartsWith("LayerName:", StringComparison.OrdinalIgnoreCase))
                    {
                        // applies for both 1-mode and 2-mode
                        currentLayer.Name = line.Substring("LayerName:".Length).Trim();
                    }
                    else if (currentLayer is LayerOneMode currentLayerOneMode)
                    {
                        if (line.StartsWith("Directionality:", StringComparison.OrdinalIgnoreCase))
                        {
                            string dirString = line.Substring("Directionality:".Length).Trim().ToLower();
                            currentLayerOneMode.Directionality = dirString.Equals("directed") ? EdgeDirectionality.Directed : EdgeDirectionality.Undirected;
                            // Given that I got directionality, try initializing the factory
                            currentLayerOneMode.TryInitFactory();
                        }
                        else if (line.StartsWith("ValueType:", StringComparison.OrdinalIgnoreCase))
                        {
                            var valString = line.Substring("ValueType:".Length).Trim().ToLower();
                            currentLayerOneMode.ValueType = valString switch
                            {
                                "valued" => EdgeType.Valued,
                                //"signed" => EdgeValueType.Signed,
                                _ => EdgeType.Binary
                            };
                            // Given that I got value type, try initializing the factory
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

                    if (layerOneMode.ValueType == EdgeType.Binary)
                    {
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (string.IsNullOrWhiteSpace(parts[i]))
                                continue;
                            uint alter = uint.Parse(parts[i]);
                            // The AddEdge() below does layer-specific checks on directionality, selfties
                            // and UserSetting checks on outbound only and BlockMultiedges
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
                            float val = Misc.FixConnectionValue(float.Parse(subparts[1], CultureInfo.InvariantCulture), layerOneMode.ValueType);
                            // The AddEdge() below does layer-specific checks on directionality, selfties
                            // and UserSetting checks on outbound only and BlockMultiedges
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
                    // By calling the AddHyperedge() in layerTwoMode, the ValidateNode setting is bypassed, as it should!
                    if (parts.Length > 1)
                        layerTwoMode.AddHyperedge(hyperName, Misc.NodesIdsStringToArray(parts[1], '\t'));
                    //network.AddHyperedge(layerTwoMode, hyperName, Misc.NodesIdsStringToArray(parts[1], '\t'));
                    else
                        layerTwoMode.AddHyperedge(hyperName, null);
                        //layerTwoMode.AddHyperedge(hyperName, new HyperEdge());
                }
            }
            if (currentLayer != null)
                // Reached last line and likely have the last layer to save
                network.Layers.Add(currentLayer.Name, currentLayer);

            network.Filepath = filepath;
            network.IsModified = false;

            if (nodesetFileReference != null)
            {
                nodeset = LoadNodesetFromFile(nodesetFileReference);
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
            return new StructureResult(network, new Dictionary<string, IStructure>
            {
                { "nodeset", nodeset }
            });
        }

        private static Nodeset ReadNodesetFromFile(StreamReader reader)
        {
            var header = reader.ReadLine() ?? throw new InvalidDataException("Missing header line.");
            var columns = header.Split('\t');
            var nodeset = new Nodeset { Name = columns[0] };

            //var attributeDefs = new List<NodeAttributeDefinition>();
            //var attributeDefTuples = new List<(string name, string type)>();

            int nbrCols = columns.Length;
            string[] attributeNames = new string[nbrCols-1];

            for (int i = 1; i < nbrCols; i++)
            {
                var parts = columns[i].Split(':', 2);
                if (parts.Length != 2) throw new InvalidDataException($"Invalid column header: {columns[i]}");
                //var name = parts[0];
                //var type = Enum.Parse<AttributeType>(parts[1], ignoreCase: true);

                //AttributeType type = Misc.GetAttributeType(parts[1]);

                //attributeDefs.Add(new NodeAttributeDefinition(name, type));
                //attributeDefTuples.Add((parts[0], parts[1]));

                //nodeset.DefineNodeAttribute()

                attributeNames[i - 1] = parts[0];
                nodeset.DefineNodeAttribute(parts[0], parts[1]);
            }

            
            //foreach (var attrDef in attributeDefs)
            //    nodeset.DefineNodeAttribute()
            //    nodeset.DefineNodeAttribute(attrDef.Name, attrDef.Type);

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

                    //var def = attributeDefs[i - 1];
                    //NodeAttribute attr = def.Type switch
                    //{
                    //    AttributeType.Int => new NodeAttribute(int.Parse(val)),
                    //    AttributeType.Float => new NodeAttribute(float.Parse(val, CultureInfo.InvariantCulture)),
                    //    AttributeType.Char => new NodeAttribute(val[0]),
                    //    AttributeType.Bool => new NodeAttribute(bool.Parse(val)),
                    //    _ => throw new NotSupportedException($"Unsupported type: {def.Type}")
                    //};

                    //nodeset.SetNodeAttribute(nodeId, def.Name, attr);
                }
            }
            //nodeset.RebuildNodeIdToIndexDictionary();
            return nodeset;
        }



        private static void WriteNodesetToFile(Nodeset nodeset, StreamWriter writer)
        {
            //var attributeDefs = nodeset.NodeAttributes;

            //var header = nodeset.Name;

            //if (attributeDefs.Length > 0)
            //    header += "\t" + string.Join("\t", attributeDefs.Select(a => $"{a.Name}:{a.Type}"));
            //writer.WriteLine(header);

            //foreach (var node in nodeset.NodeArray)
            //{
            //    var row = new List<string> { node.Id.ToString() };
            //    foreach (var attrDef in attributeDefs)
            //    {
            //        var attr = nodeset.GetNodeAttribute(node.Id, attrDef.Name);
            //        row.Add(attr.Value.ToString() ?? "");
            //    }
            //    writer.WriteLine(string.Join("\t", row));
            //}
        }

        //private static void WriteMatrixToFile(MatrixStructure matrix, StreamWriter writer)
        //{
        //    string content = matrix.Content;
        //    writer.Write(content);
        //}

        //public static Nodeset LoadNodesetFromFile(string filepath)
        //{
        //    using var fileStream = File.OpenRead(filepath);
        //    using var stream = WrapIfCompressed(fileStream, filepath, CompressionMode.Decompress);
        //    using var reader = new StreamReader(stream);

        //    var header = reader.ReadLine() ?? throw new InvalidDataException("Missing header line.");
        //    var columns = header.Split('\t');

        //    var attributeDefs = new List<NodeAttributeDefinition>();
        //    for (int i = 1; i < columns.Length; i++)
        //    {
        //        var parts = columns[i].Split(':');
        //        if (parts.Length != 2) throw new InvalidDataException($"Invalid column header: {columns[i]}");
        //        var name = parts[0];
        //        var type = Enum.Parse<AttributeType>(parts[1], ignoreCase: true);
        //        attributeDefs.Add(new NodeAttributeDefinition(name, type));
        //    }

        //    var nodeset = new Nodeset { Name = Path.GetFileNameWithoutExtension(filepath) };
        //    foreach (var attrDef in attributeDefs)
        //        nodeset.DefineNodeAttribute(attrDef.Name, attrDef.Type);

        //    while (!reader.EndOfStream)
        //    {
        //        var line = reader.ReadLine();
        //        if (string.IsNullOrWhiteSpace(line)) continue;

        //        var parts = line.Split('\t');
        //        uint nodeId = uint.Parse(parts[0]);
        //        nodeset.AddNode(nodeId);

        //        for (int i = 1; i < parts.Length && i <= attributeDefs.Count; i++)
        //        {
        //            var val = parts[i];
        //            if (string.IsNullOrEmpty(val)) continue;

        //            var def = attributeDefs[i - 1];
        //            NodeAttribute attr = def.Type switch
        //            {
        //                AttributeType.Int => new NodeAttribute(int.Parse(val)),
        //                AttributeType.Float => new NodeAttribute(float.Parse(val, CultureInfo.InvariantCulture)),
        //                AttributeType.Char => new NodeAttribute(val[0]),
        //                AttributeType.Bool => new NodeAttribute(bool.Parse(val)),
        //                _ => throw new NotSupportedException($"Unsupported type: {def.Type}")
        //            };

        //            nodeset.SetNodeAttribute(nodeId, def.Name, attr);
        //        }
        //    }
        //    nodeset.RebuildNodeIdToIndexDictionary();
        //    return nodeset;
        //}
    }
}
