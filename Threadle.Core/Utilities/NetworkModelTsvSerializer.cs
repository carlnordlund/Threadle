using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Utilities
{
    public static class NetworkModelTsvSerializer
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private static Stream WrapIfCompressed(Stream stream, string filepath, CompressionMode mode)
        {
            return filepath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                ? new GZipStream(stream, mode)
                : stream;
        }

        public static void SaveToFile(Network network, string filepath, string? nodesetFileReference = null)
        {
            using var fileStream = File.Create(filepath);
            using var stream = WrapIfCompressed(fileStream, filepath, CompressionMode.Compress);
            using var writer = new StreamWriter(stream, Utf8NoBom);

        }

        //public static NetworkModel LoadFromFile(string filepath, Nodeset? providedNodeset, out Nodeset usedNodeset, out bool generatedNodeset)
        //{
        //    var lines = File.ReadAllLines(filepath);
        //    var network = new NetworkModel("");
        //    HashSet<uint> discoveredNodeIds = new();

        //    Nodeset? loadedNodeset = null;
        //    string? networkName = null;

        //    int index = 0;
        //    for (; index < lines.Length; index++)
        //    {
        //        string line = lines[index].Trim();
        //        if (string.IsNullOrWhiteSpace(line))
        //            continue;
        //        if (line.StartsWith("Network Name:"))
        //        {
        //            networkName = line.Split("Network Name:")[1].Trim();
        //            network.Name = networkName;
        //        }
        //        else if (line.StartsWith("Nodeset File:"))
        //        {
        //            string nodesetPath = line.Split("Nodeset File:")[1].Trim();
        //            loadedNodeset = NodesetTsvSerializer.LoadFromFile(nodesetPath);
        //        }
        //        else if (line.StartsWith("Layer Name:"))
        //        {
        //            break;
        //        }
        //    }

        //    if (providedNodeset!=null)
        //    {
        //        usedNodeset = providedNodeset;
        //        generatedNodeset = false;
        //    }
        //    else if (loadedNodeset!=null)
        //    {
        //        usedNodeset = loadedNodeset;
        //        generatedNodeset = false;
        //    }
        //    else
        //    {
        //        usedNodeset = new Nodeset();
        //        generatedNodeset = true;
        //    }

        //    while (index < lines.Length)
        //    {
        //        string line = lines[index].Trim();
        //        if (string.IsNullOrEmpty(line))
        //        {
        //            index++;
        //            continue;
        //        }
        //        if (line.StartsWith("Layer Name:"))
        //        {
        //            string name = GetField(line, "Layer Name:");
        //            string direction = GetField(line, "Directionality:");
        //            string valueType = GetField(line, "ValueType:");
        //            string selfties = GetField(line, "Selfties:");

                    
        //        }
        //    }
        //    // More code to add, but doing save function first...

        //    return network;
        //}

        //private static string GetField(string line, string v)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
