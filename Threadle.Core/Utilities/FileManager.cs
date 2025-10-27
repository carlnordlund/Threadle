using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Utilities
{
    public static class FileManager
    {
        static Dictionary<string, Type> structureTypes = new()
        {
            { "nodeset", typeof(Nodeset) },
            { "network", typeof(Network) }
        };

        public static void Save(IStructure structure, string filepath, FileFormat format = FileFormat.TsvGzip, string? nodesetFilepath = null)
        {
            switch (structure)
            {
                case Nodeset nodeset:
                    SaveNodeset(nodeset, filepath, format);
                    break;
                case Network network:
                    SaveNetwork(network, filepath, format, nodesetFilepath);
                    break;
                default:
                    throw new NotSupportedException($"Error: Save not implemented for type '{structure.GetType()}'.");
            }
        }


        public static StructureResult Load(string filepath, string structureTypeString, FileFormat format = FileFormat.TsvGzip)
        {
            Type structureType = structureTypes[structureTypeString]
                ?? throw new NotSupportedException($"Error: Structure '{structureTypeString}' not supported.");

            return structureType switch
            {
                var t when t == typeof(Nodeset) => LoadNodeset(filepath, format),
                var t when t == typeof(Network) => LoadNetwork(filepath, format),
                _ => throw new NotSupportedException($"Error: Load not implemented for type '{structureType}'.")
            };
        }

        private static void SaveNodeset(Nodeset nodeset, string filepath, FileFormat format)
        {
            if (format == FileFormat.TsvGzip)
                CompressedTsvSerializer.SaveNodesetToFile(nodeset, filepath);
        }

        private static StructureResult LoadNodeset(string filepath, FileFormat format)
        {
            Nodeset nodeset;
            if (format == FileFormat.TsvGzip)
            {
                nodeset = CompressedTsvSerializer.LoadNodesetFromFile(filepath)
                    ?? throw new Exception($"Error: Failed to load Nodeset '{filepath}'.");
            }
            else
            {
                throw new NotImplementedException($"Error: File format '{format}' is not supported.");
            }
            return new StructureResult(nodeset);
        }

        private static void SaveNetwork(Network network, string filepath, FileFormat format, string? nodesetFilepath = null)
        {
            if (format == FileFormat.TsvGzip)
            {
                if (nodesetFilepath != null)
                    CompressedTsvSerializer.SaveNodesetToFile(network.Nodeset, nodesetFilepath);
                CompressedTsvSerializer.SaveNetworkToFile(network, filepath, nodesetFilepath);
            }
        }


        private static StructureResult LoadNetwork(string filepath, FileFormat format)
        {
            StructureResult result;
            if (format == FileFormat.TsvGzip)
            {
                result = CompressedTsvSerializer.LoadNetworkFromFile(filepath)
                    ?? throw new Exception($"Error: Failed to load Network '{filepath}'");
            }
            else
            {
                throw new NotImplementedException($"File format '{format}' is not supported.");
            }
            return result;

        }

        //public static StructureResult Import(string filepath, string formatString, Nodeset? nodeset = null, string separator="\t", bool rowHeaders=true, bool columnHeaders=true)
        //{
        //    // Import network given various formats.
        //    // If nodeset is provided: use that when importing. If not provided: create and store as extra structure in StructureResult
        //    return formatString switch
        //    {
        //        "matrix" => MatrixFileSerializer.Load(filepath, nodeset, separator, rowHeaders, columnHeaders),
        //        _ => throw new Exception($"Error: Format {formatString} not known/implemented.")
        //    };
            
            
        //    throw new NotImplementedException();
        //}

        public static void ImportLayer(string filepath, Network network, ILayer layer, string format, string separator, bool addMissingNodes)
        {
            if (layer is LayerOneMode layerOneMode)
            {
                if (format.Equals("edgelist", StringComparison.OrdinalIgnoreCase))
                    FormatImporters.ImportOneModeEdgelist(filepath, network, layerOneMode, separator, addMissingNodes);
                else if (format.Equals("matrix", StringComparison.OrdinalIgnoreCase))
                    FormatImporters.ImportOneModeMatrix(filepath, network, layerOneMode, separator, addMissingNodes);
            }
            else if (layer is LayerTwoMode layerTwoMode)
            {
                if (format.Equals("edgelist", StringComparison.OrdinalIgnoreCase))
                    FormatImporters.ImportTwoModeEdgelist(filepath, network, layerTwoMode, separator, addMissingNodes);
                else if (format.Equals("matrix", StringComparison.OrdinalIgnoreCase))
                    FormatImporters.ImportTwoModeMatrix(filepath, network, layerTwoMode, separator, addMissingNodes);
            }
            else
                throw new NotImplementedException();
        }

        public static string SafeSetCurrentDirectory(string path, string? baseDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new Exception($"Error: Path cannot be null or whitespace.");

            string fullPath = baseDirectory == null ? Path.GetFullPath(path) : Path.GetFullPath(path, baseDirectory);
            if (baseDirectory!=null)
            {
                string fullBase = Path.GetFullPath(baseDirectory);
                if (!fullPath.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException($"!Error: Path '{fullPath}' is outside the allowed base directory '{fullBase}'.");
            }

            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException($"!Error: Directory does not exist: '{fullPath}'.");

            Directory.SetCurrentDirectory(fullPath);
            return fullPath;
        }

        public static string GetCurrentDirectory()
        {
            return Directory.GetCurrentDirectory();
        }
    }
}
