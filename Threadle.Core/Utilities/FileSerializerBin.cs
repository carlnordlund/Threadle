using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model;
using K4os.Compression;
using K4os.Compression.LZ4.Streams;

namespace Threadle.Core.Utilities
{
    internal static class FileSerializerBin
    {

        private static readonly string Magic = "TNDS";
        private const byte FormatVersion = 1;


        /// <summary>
        /// Method for loading a nodeset from file (TSV format).
        /// Could throw exceptions that must be caught.
        /// </summary>
        /// <param name="filepath">The filepath to the file.</param>
        /// <returns>A Nodeset object.</returns>
        //internal static Nodeset LoadNodesetFromFile(string filepath)
        //{
        //    using var fileStream = File.OpenRead(filepath);
        //    using var stream = WrapIfCompressed(fileStream, filepath, CompressionMode.Decompress);
        //    using var reader = new StreamReader(stream, Utf8NoBom);

        //    Nodeset nodeset = ReadNodesetFromFile(filepath, reader);
        //    //nodeset.Filepath = filepath;
        //    //nodeset.IsModified = false;
        //    return nodeset;
        //}

        /// <summary>
        /// Method for saving a nodeset to file (TSV format).
        /// Could throw exceptions that must be caught.
        /// </summary>
        /// <param name="nodeset">The Nodeset to save to file.</param>
        /// <param name="filepath">The filepath to save to.</param>
        internal static void SaveNodesetToFile(Nodeset nodeset, string filepath)
        {
            using var fileStream = File.Create(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, CompressionMode.Compress);
            using var writer = new BinaryWriter(stream);
            WriteNodesetToFile(nodeset, writer);
            nodeset.Filepath = filepath;
            nodeset.IsModified = false;
        }




        /// <summary>
        /// Support method to potentially attach a GZipStream around the existing stream.
        /// Checks the filepath: if it ends with .gz, then the GZip is attached.
        /// </summary>
        /// <param name="stream">The file stream.</param>
        /// <param name="filepath">The filepath.</param>
        /// <param name="mode">The used CompressionMode</param>
        /// <returns>The original stream or the GZip stream.</returns>
        private static Stream WrapIfCompressed(Stream stream, string filepath, CompressionMode mode)
        {
            if (!filepath.EndsWith(".lz4", StringComparison.OrdinalIgnoreCase))
                return stream;
            if (mode == CompressionMode.Compress)
                return LZ4Stream.Encode(stream);
            else
                return LZ4Stream.Decode(stream);
        }


        /// <summary>
        /// Support method to write a Nodeset object to file.
        /// </summary>
        /// <param name="nodeset">The Nodeset object to save to file.</param>
        /// <param name="writer">The stream writer.</param>
        private static void WriteNodesetToFile(Nodeset nodeset, BinaryWriter writer)
        {
            // Magic bytes (4)
            writer.Write(Encoding.ASCII.GetBytes(Magic));

            // Format version (1)
            writer.Write(FormatVersion);
            
            // Nodeset name (length + string)
            WriteString(writer, nodeset.Name);

            // Get all node attribute definitions
            var attributeDefs = nodeset.NodeAttributeDefinitionManager.GetAllNodeAttributeDefinitions().ToList();

            // Nbr of node attributes (4)
            writer.Write(attributeDefs.Count);

            Dictionary<string, int> nameToIndex = [];

            for (int i = 0; i < attributeDefs.Count; i++)
            {
                // Node attribute name (length + string)
                WriteString(writer, attributeDefs[i].Name);
                // Node attribute type (1)
                writer.Write((byte)attributeDefs[i].Type);
                nameToIndex[attributeDefs[i].Name] = i;
            }

            var nodeIds = nodeset.NodeIdArray;
            // Nbr nodes
            writer.Write(nodeIds.Length);

            foreach (var nodeId in nodeIds)
            {
                // Node id (4)
                writer.Write(nodeId);

                // Get node attributes for this node
                Dictionary<string, NodeAttributeValue> nodeAttrValues = nodeset.GetNodeAttributeValues(nodeId);

                // Nbr of node attributes
                writer.Write(nodeAttrValues.Count);

                foreach (var nodeAttrValue in nodeAttrValues)
                {
                    // Node attribute index (mapping from node attribute name to the index it got above) [4]
                    writer.Write(nameToIndex[nodeAttrValue.Key]);

                    //Node attribute value
                    writer.Write(nodeAttrValue.Value.RawValueAsInt());
                }
            }
        }

        private static void WriteString(BinaryWriter writer, string? value)
        {
            if (value == null)
            {
                writer.Write(-1);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}
