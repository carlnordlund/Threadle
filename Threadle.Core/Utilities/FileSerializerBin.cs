using System.IO.Compression;
using System.Text;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Class for loading/saving structures in Threadle's binary formats.
    /// These readers use less verbose and more direct methods for adding edges etc. As the binary files are
    /// not human-readable, it is less likely that a human would be in there messing things up compared to
    /// tsv files. Therefore, it is assumed here that all data in the binary files is correct and does not need
    /// validation.
    /// </summary>

    internal static class FileSerializerBin
    {
        #region Fields
        /// <summary>
        /// Magic bytes for Threadle's binary files for Nodesets vs Networks.
        /// </summary>
        private static readonly string MagicNodeset = "TNDS";
        private static readonly string MagicNetwork = "TNTW";

        /// <summary>
        /// Future-looking: if we ever were to change the file format later on, this is format version 1.
        /// Then additional readers can be implemented for would-be future format versions.
        /// </summary>
        private const byte FormatVersion = 1;
        #endregion


        #region Methods (internal)
        /// <summary>
        /// Method for loading a nodeset from file (binary format).
        /// Always attached a buffered reader, set to 1 MB. If the FileFormat is BinGzip,
        /// the <see cref="WrapIfCompressed(Stream, string, FileFormat, CompressionMode)"/> attaches
        /// a Gzip decompressor.
        /// Could throw exceptions that has to be caught.
        /// </summary>
        /// <param name="filepath">The file to load the Nodeset from.</param>
        /// <param name="format">The <see cref="FileFormat"/> to use.</param>
        /// <returns>A <see cref="Nodeset"/> object.</returns>
        internal static Nodeset LoadNodesetFromFile(string filepath, FileFormat format)
        {
            using var fileStream = File.OpenRead(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, format, CompressionMode.Decompress);
            using var buffered = new BufferedStream(stream, 1 << 20);
            using var reader = new BinaryReader(buffered);
            return ReadNodesetFromFile(filepath, reader);
        }

        /// <summary>
        /// Method for saving a nodeset to binary file (binary format). If format is <see cref="FileFormat.BinGzip"/>,
        /// a gzip decompressor is attaced.
        /// Could throw exceptions that must be caught.
        /// </summary>
        /// <param name="nodeset">The Nodeset to save to file.</param>
        /// <param name="filepath">The filepath to save to.</param>
        /// <param name="format">The <see cref="FileFormat"/> to use.</param>
        internal static void SaveNodesetToFile(Nodeset nodeset, string filepath, FileFormat format)
        {
            using var fileStream = File.Create(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, format, CompressionMode.Compress);
            using var writer = new BinaryWriter(stream);
            WriteNodesetToFile(nodeset, writer);
            nodeset.Filepath = filepath;
            nodeset.IsModified = false;
        }

        /// <summary>
        /// Method for loading a Network from file (binary format). Will also load the Nodeset that this
        /// network relates to.
        /// Always attached a buffered reader, set to 1 MB. If the FileFormat is BinGzip,
        /// the <see cref="WrapIfCompressed(Stream, string, FileFormat, CompressionMode)"/> attaches
        /// a Gzip decompressor.
        /// Could throw exceptions that has to be caught.
        /// </summary>
        /// <param name="filepath">The file to load the Nodeset from.</param>
        /// <param name="format">The <see cref="FileFormat"/> to use.</param>
        /// <returns>A <see cref="StructureResult"/> containing the Network and Nodeset objects.</returns>
        internal static StructureResult LoadNetworkFromFile(string filepath, FileFormat format)
        {
            using var fileStream = File.OpenRead(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, format, CompressionMode.Decompress);
            using var buffered = new BufferedStream(stream, 1 << 20);
            using var reader = new BinaryReader(buffered);
            return ReadNetworkFromFile(filepath, reader);
        }

        /// <summary>
        /// Method for saving a Network to binary file (binary format). If format is <see cref="FileFormat.BinGzip"/>,
        /// a gzip compressor is attaced. If the related Nodeset is modified, that will also be saved.
        /// Could throw exceptions that must be caught.
        /// </summary>
        /// <param name="nodeset">The Nodeset to save to file.</param>
        /// <param name="filepath">The filepath to save to.</param>
        /// <param name="format">The <see cref="FileFormat"/> to use.</param>
        internal static void SaveNetworkToFile(Network network, string filepath, FileFormat format)
        {
            using var fileStream = File.Create(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, format, CompressionMode.Compress);
            using var writer = new BinaryWriter(stream);
            WriteNetworkToFile(network, writer);
            network.Filepath = filepath;
            network.IsModified = false;
        }
        #endregion


        #region Methods (private)
        /// <summary>
        /// Support method to potentially attach a GZipStream around the existing stream.
        /// If the provided format is <see cref="FileFormat.BinGzip"/>, it attaches a Gzip
        /// compressor/decompressor as stated by the <see cref="CompressionMode"/>. Any other fileformat
        /// will return the standard stream.
        /// </summary>
        /// <param name="stream">The file stream.</param>
        /// <param name="filepath">The filepath.</param>
        /// <param name="format">The <see cref="FileFormat"/>.</param>
        /// <param name="mode">The used CompressionMode</param>
        /// <returns>The original stream or the GZip stream.</returns>
        private static Stream WrapIfCompressed(Stream stream, string filepath, FileFormat format, CompressionMode mode)
        {
            return format == FileFormat.BinGzip ? new GZipStream(stream, mode) : stream;
        }

        /// <summary>
        /// Internal method to read a Network (and its Nodeset) from a binary file. Note that the Nodeset is alays loaded.
        /// Will throw exceptions that must be caught.
        /// </summary>
        /// <param name="filepath">The filepath to the binary file (only used to set the <see cref="Network.Filepath"/> property).</param>
        /// <param name="reader">The binary reader to read from.</param>
        /// <returns>A <see cref="StructureResult"/> containing the Network and Nodeset.</returns>
        /// <exception cref="InvalidDataException">Thrown if the binary file isn't a Threadle Network file or if it is the wrong version.</exception>
        private static StructureResult ReadNetworkFromFile(string filepath, BinaryReader reader)
        {
            // Check magic bytes - should be the MagicNetwork characters (TNTW)
            var magicBytes = reader.ReadBytes(4);
            string magic = Encoding.ASCII.GetString(magicBytes);
            if (magic != MagicNetwork)
                throw new InvalidDataException($"Invalid magic bytes in file '{filepath}': not Threadle Network binary file (TNTW).");

            // Check file version - should be the same as here implemented
            byte version = reader.ReadByte();
            if (version != FormatVersion)
                throw new InvalidDataException($"Unsupported version {version} in file '{filepath}'. Expected version: {FormatVersion}.");

            // Get Network name
            string networkName = ReadString(reader);

            // Get Nodeset filepath (compulsory here)
            string nodesetFilepath = ReadString(reader);

            // Load and initialize Nodeset:
            FileFormat nodesetFormat = Misc.GetFileFormatFromFileEnding(nodesetFilepath);
            Nodeset nodeset = LoadNodesetFromFile(nodesetFilepath, nodesetFormat);

            // Create network with the recently loaded Nodeset
            Network network = new Network(networkName, nodeset);
            network.Filepath = filepath;

            // Get nbr Layers
            int nbrLayers = reader.ReadByte();

            // Loop through the layers to load
            for (int i = 0; i < nbrLayers; i++)
            {
                string layerName = ReadString(reader);
                int mode = reader.ReadByte();
                if (mode == 1)
                {
                    // Get properties of the 1-mode layer
                    EdgeDirectionality edgeDirectionality = (EdgeDirectionality)reader.ReadByte();
                    EdgeType edgeType = (EdgeType)reader.ReadByte();
                    bool selfties = reader.ReadBoolean();

                    // Create 1-mode layer
                    LayerOneMode layerOneMode = new LayerOneMode(layerName, edgeDirectionality, edgeType, selfties);

                    // Add it to network's layers
                    network.Layers.Add(layerName, layerOneMode);

                    // Get nbr of nodelist rows
                    int nbrEdgesets = reader.ReadInt32();

                    // Initialize the capacity of the Edgeset dictionary
                    layerOneMode._initSizeEdgesetDictionary(nbrEdgesets);

                    if (layerOneMode.IsBinary)
                    {
                        // Looping for binary nodelist rows
                        for (uint j = 0; j < nbrEdgesets; j++)
                        {
                            // Get ego of nodelist row
                            uint nodeIdEgo = reader.ReadUInt32();

                            // Get nbr of alters
                            int nbrAlters = reader.ReadInt32();

                            // Prepare array of alters
                            uint[] nodeIdsAlters = new uint[nbrAlters];

                            for (uint k = 0; k < nbrAlters; k++)
                            {
                                nodeIdsAlters[k] = reader.ReadUInt32();
                            }
                            layerOneMode._addBinaryEdges(nodeIdEgo, nodeIdsAlters);
                        }
                    }
                    else
                    {
                        // Looping for valued nodelist rows
                        for (uint j = 0; j < nbrEdgesets; j++)
                        {
                            // Get ego of nodelist row
                            uint nodeIdEgo = reader.ReadUInt32();

                            // Get nbr of alters
                            int nbrAlters = reader.ReadInt32();

                            // Prepare array of alters
                            List<(uint alterId, float value)> nodeIdsAlters = new(nbrAlters);

                            for (uint k = 0; k < nbrAlters; k++)
                            {
                                // Read both the partner node id and the float value and put into the List of tuples
                                nodeIdsAlters.Add((reader.ReadUInt32(), reader.ReadSingle()));
                            }
                            // Add all valued edges connected with the ego
                            layerOneMode._addValuedEdges(nodeIdEgo, nodeIdsAlters);
                        }
                    }
                }
                else if (mode == 2)
                {
                    // Create 2-mode layer with the specified name
                    LayerTwoMode layerTwoMode = new LayerTwoMode(layerName);

                    // Add it to network's layers
                    network.Layers.Add(layerName, layerTwoMode);

                    // Get nbr of hyperedges in this layer
                    uint nbrHyperedges = reader.ReadUInt32();

                    // Iterate through all hyperedges
                    for (uint j = 0; j < nbrHyperedges; j++)
                    {
                        // Get the name of this hyperedge
                        string hyperedgeName = ReadString(reader);

                        // Get the number of nodes connected to this hyperedge
                        uint nbrNodes = reader.ReadUInt32();

                        // Create an array of node ids connected to this hyperedge
                        uint[] nodeIds = new uint[nbrNodes];
                        for (uint k = 0; k < nbrNodes; k++)
                            nodeIds[k] = reader.ReadUInt32();

                        // Create and add hyperedge to this layer
                        layerTwoMode._addHyperedge(hyperedgeName, nodeIds);
                    }
                }
                else
                    throw new InvalidDataException($"Layer mode not recognized in file '{filepath}': {mode} - must be 1 or 2.");
            }
            return new StructureResult(network, new Dictionary<string, IStructure>
            {
                { "nodeset", nodeset }
            });
        }


        /// <summary>
        /// Support method to read a Nodeset object from file.
        /// </summary>
        /// <param name="filepath">Filepath to the file.</param>
        /// <param name="reader">The BinaryReader object.</param>
        /// <returns>A Nodeset object.</returns>
        private static Nodeset ReadNodesetFromFile(string filepath, BinaryReader reader)
        {
            // Check magic  bytes - should be the MagicNodeset characters (TNDS)
            var magicBytes = reader.ReadBytes(4);
            string magic = Encoding.ASCII.GetString(magicBytes);
            if (magic != MagicNodeset)
                throw new InvalidDataException($"Invalid magic bytes in file '{filepath}': not Threadle Nodeset binary file (TNDS).");

            // Check file version - should be the same as here implemented
            byte version = reader.ReadByte();
            if (version != FormatVersion)
                throw new InvalidDataException($"Unsupported version {version} in file '{filepath}'. Expected version: {FormatVersion}.");

            // Get nodeset name
            string nodesetName = ReadString(reader);

            // Initialize Nodeset object - set name, filepath, ismodified
            var nodeset = new Nodeset { Name = nodesetName, Filepath = filepath, IsModified = false };

            // Get nbr of defined node attributes
            byte attrCount = reader.ReadByte();

            // Prepare collection of node attributes
            //var attributeDefs = new List<(string name, NodeAttributeType type)>(attrCount);
            NodeAttributeType[] attrDefs = new NodeAttributeType[attrCount];

            // Iterate node attribute data and define node attributes
            for (int i = 0; i < attrCount; i++)
            {
                string attrName = ReadString(reader);
                byte typeByte = reader.ReadByte();
                NodeAttributeType type = (NodeAttributeType)typeByte;

                nodeset.NodeAttributeDefinitionManager.DefineNewNodeAttribute(attrName, type);
                //attributeDefs.Add((attrName, type));
                attrDefs[i] = type;
            }

            // Get nbr of nodes WITHOUT attributes
            int nbrNodesWithoutAttributes = reader.ReadInt32();

            // I should prepare size of this hashset now!
            nodeset.InitSizeNodesWithoutAttributes(nbrNodesWithoutAttributes);

            // Iterate through all nodes without attributes
            for (int i = 0; i < nbrNodesWithoutAttributes; i++)
            {
                // Read nodeId
                uint nodeId = reader.ReadUInt32();

                // Add node without any validation checks etc.
                nodeset._addNodeWithoutAttribute(nodeId);
            }

            // Get nbr of nodes WITH attributes
            int nbrNodesWithAttributes = reader.ReadInt32();

            // Prepare capacity of collection of nodes with attributes
            nodeset.InitSizeNodesWithAttributes(nbrNodesWithAttributes);

            for (int i = 0; i < nbrNodesWithAttributes; i++)
            {
                // Read nodeId
                uint nodeId = reader.ReadUInt32();

                // Get nbr attributes for this node
                byte nodeAttrCount = reader.ReadByte();

                // Prepare storage for these node attributes
                List<byte> attrIndexes = new(nodeAttrCount);
                List<NodeAttributeValue> attrValues = new(nodeAttrCount);

                // Loop through the attributes of this node
                for (int a = 0; a < nodeAttrCount; a++)
                {
                    // Read attrIndex
                    byte attrIndex = reader.ReadByte();
                    //var def = attributeDefs[attrIndex];
                    // Get raw value
                    int rawValue = reader.ReadInt32();

                    // Convert to NodeAttributeValue
                    //NodeAttributeValue value = NodeAttributeValue.FromRaw(rawValue, def.type);
                    NodeAttributeValue value = NodeAttributeValue.FromRaw(rawValue, attrDefs[attrIndex]);

                    // Build up the attribute storage
                    attrIndexes.Add(attrIndex);
                    attrValues.Add(value);
                }

                // Add this node along with the attribute storage (tuple)
                nodeset._addNodeWithAttributes(nodeId, (attrIndexes, attrValues));
            }
            nodeset.Filepath = filepath;
            nodeset.IsModified = false;
            return nodeset;
        }


        /// <summary>
        /// Support method to write a Nodeset object to file.
        /// </summary>
        /// <param name="nodeset">The Nodeset object to save to file.</param>
        /// <param name="writer">The stream writer.</param>
        private static void WriteNodesetToFile(Nodeset nodeset, BinaryWriter writer)
        {
            // MagicNodeset bytes (4)
            writer.Write(Encoding.ASCII.GetBytes(MagicNodeset));

            // Format version (1)
            writer.Write(FormatVersion);

            // Nodeset name (length + string)
            WriteString(writer, nodeset.Name);

            // Get all node attribute definitions
            var attributeDefs = nodeset.NodeAttributeDefinitionManager.GetAllNodeAttributeDefinitions().ToList();

            // Nbr of node attributes (4)
            writer.Write((byte)attributeDefs.Count);

            //Dictionary<string, byte> nameToIndex = [];
            // The "internal" index is the attrIndex, as stored in NodeAttributeDefinitionManager
            // The "here" is the order in which the attribute is stored here.
            // So when I later write the attribute index, I will be using the index as presented here.
            // This means that saving and loading a bin will also compress the indexes
            Dictionary<byte, byte> internalToHere = [];

            for (byte i = 0; i < attributeDefs.Count; i++)
            {
                WriteString(writer, attributeDefs[i].AttrName);
                writer.Write((byte)attributeDefs[i].AttrType);
                //nameToIndex[attributeDefs[i].Name] = i;
                internalToHere[attributeDefs[i].Index] = i;
            }

            // Get array of nodes without attributes
            var nodeIdsWithoutAttributes = nodeset.NodeIdArrayWithoutAttributes;

            // Write number of nodes without attributes
            writer.Write(nodeIdsWithoutAttributes.Length);

            // Write each node id for those without attributes
            foreach (uint nodeId in nodeIdsWithoutAttributes)
                writer.Write(nodeId);

            // Get array of nodes with attributes
            var nodeIdsWithAttributes = nodeset.NodeIdArrayWithAttributes;

            // Write number of nodes with attributes
            writer.Write(nodeIdsWithAttributes.Length);

            foreach (uint nodeId in nodeIdsWithAttributes)
            {
                // Node id (4)
                writer.Write(nodeId);

                // Get node attributes for this node
                //Dictionary<string, NodeAttributeValue> nodeAttrValues = nodeset.GetNodeAttributeValues(nodeId);

                var attributes = nodeset.GetNodeAttributeTuple(nodeId)!;


                // Nbr of node attributes
                writer.Write((byte)attributes.Value.AttrIndexes.Count);

                // Loop through the node attributes for this node and write
                for (int i = 0; i < attributes.Value.AttrIndexes.Count; i++)
                {
                    // Map from the real internal attrIndex value to the one used here
                    writer.Write((byte)internalToHere[attributes.Value.AttrIndexes[i]]);

                    // Write out the node attribute value in raw format
                    writer.Write(attributes.Value.AttrValues[i].RawValueAsInt());
                }
            }
        }

        /// <summary>
        /// Support method to write a network to file
        /// </summary>
        /// <param name="network">The Network to write</param>
        /// <param name="writer">The binary writer to write to</param>
        private static void WriteNetworkToFile(Network network, BinaryWriter writer)
        {
            // MagicNodeset bytes (4)
            writer.Write(Encoding.ASCII.GetBytes(MagicNetwork));

            // Format version (1)
            writer.Write(FormatVersion);

            // Network name (length + string)
            WriteString(writer, network.Name);

            // Nodeset filepath
            WriteString(writer, network.Nodeset.Filepath);

            // Nbr of layers (max 255 layers)
            writer.Write((byte)network.Layers.Count);

            foreach (var layer in network.Layers)
            {
                // Write the name of this layer
                WriteString(writer, layer.Key);

                // Check if it is 1-mode or 2-mode: different writing logics
                if (layer.Value is LayerOneMode layerOneMode)
                {
                    // LAYER IS 1-MODE

                    // Write mode (1)
                    writer.Write((byte)1);

                    // Write directionality enum as a byte
                    writer.Write((byte)layerOneMode.Directionality);

                    // Write value type as a byte
                    writer.Write((byte)layerOneMode.EdgeValueType);

                    // Write selfties boolean as a byte (0:false, 1:true)
                    writer.Write(layerOneMode.Selfties);

                    // Get nbr of edgesets (nodelist rows)
                    int nbrEdgesets = layerOneMode.Edgesets.Count;

                    // Write nbr of nodelist rows
                    writer.Write(nbrEdgesets);

                    if (layerOneMode.IsValued)
                    {
                        // Layer is 1-mode valued: store both alter and edge value
                        foreach ((uint nodeId, IEdgeset edgeset) in layerOneMode.Edgesets)
                        {
                            if (edgeset is IEdgesetValued edgesetValued)
                            {
                                // Write ego node id
                                writer.Write(nodeId);

                                // Get partnerNodeIds (upper triangle for symmetric data)
                                IReadOnlyList<Connection> partnerConnections = edgesetValued.GetNodelistAlterConnections(nodeId);

                                // Note: need to include all nodeId even for those with empty partnerNodeIds: this is because it has to fit nbrEdgesets above

                                // Write nbr of alters this node id has
                                writer.Write(partnerConnections.Count);

                                // Iterate through alters and write partnerNodeId and float value
                                foreach (Connection conn in partnerConnections)
                                {
                                    // Writes the partner node id
                                    writer.Write(conn.partnerNodeId);

                                    // Writes the float value of this edge
                                    writer.Write(conn.value);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Layer is 1-mode and binary: store only alter
                        foreach ((uint nodeId, IEdgeset edgeset) in layerOneMode.Edgesets)
                        {
                            if (edgeset is IEdgesetBinary edgesetBinary)
                            {
                                // Ego node id
                                writer.Write(nodeId);

                                // Get List of alter nodeids. As symmetric should only be stored once, pass along the
                                // current nodeId to only get alter node ids that are larger than the ego nodeid
                                List<uint> partnerNodeIds = edgesetBinary.GetNodelistAlterUints(nodeId);

                                // Write nbr of alters this node id has
                                writer.Write(partnerNodeIds.Count);

                                // Iterate through alters and write partnerNodeId
                                foreach (uint partnerNodeId in partnerNodeIds)
                                    writer.Write(partnerNodeId);
                            }
                        }
                    }
                }
                else if (layer.Value is LayerTwoMode layerTwoMode)
                {
                    // LAYER IS 2-MODE
                    ///////////

                    // Write mode (2)
                    writer.Write((byte)2);

                    // Write the number of hyperedges in this layer
                    writer.Write(layerTwoMode.AllHyperEdges.Count);

                    foreach ((string hyperName, Hyperedge hyperedge) in layerTwoMode.AllHyperEdges)
                    {
                        // Write the name of the hyperedge
                        writer.Write(hyperName);

                        // Write the number of affiliated nodes to this hyperedge
                        writer.Write(hyperedge.NbrNodes);

                        foreach (uint nodeId in hyperedge.NodeIds)
                            writer.Write(nodeId);
                    }
                }
            }
        }

        /// <summary>
        /// Support method for writing a string to the binary writer.
        /// First byte is length of string, remaining bytes are the actual
        /// string
        /// </summary>
        /// <param name="writer">The binarywriter to write to</param>
        /// <param name="value">The string to write</param>
        private static void WriteString(BinaryWriter writer, string? value)
        {
            if (value == null)
            {
                writer.Write(-1);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }

        /// <summary>
        /// Support method for reading a string from the binary reader.
        /// First byte is length of string, remaining bytes are the actual string.
        /// </summary>
        /// <param name="reader">The binary reader to read from.</param>
        /// <returns>The ASCII string.</returns>
        private static string ReadString(BinaryReader reader)
        {
            byte len = reader.ReadByte();
            if (len == 0)
                return string.Empty;
            byte[] bytes = reader.ReadBytes(len);
            return Encoding.ASCII.GetString(bytes);
        }
        #endregion

    }
}
