using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.Metadata.Ecma335;

namespace Threadle.Core.Utilities
{
    public static class FileManager
    {
        #region Fields
        static readonly Dictionary<string, Type> _structureTypes = new()
        {
            { "nodeset", typeof(Nodeset) },
            { "network", typeof(Network) }
        };
        #endregion

        #region Methods (public)
        /// <summary>
        /// Public-facing method for loading a data structure from file. Returns an OperationResult that holds
        /// a StructureResult, which in turn can hold one or more IStructure objects.
        /// Catches all exceptions, converts to OperationResults.
        /// </summary>
        /// <param name="filepath">The filepath to the file to load.</param>param>
        /// <param name="structureTypeString">The type of structure to load ('network' or 'nodeset')</param>
        /// <param name="format">The file format (currently only TsvGzip by default)</param>
        /// <returns>Returns an OperationResult with a StructureResult object holding the loaded structures.</returns>
        public static OperationResult<StructureResult> Load(string filepath, string structureTypeString, FileFormat format = FileFormat.TsvGzip)
        {
            try
            {
                if (!_structureTypes.TryGetValue(structureTypeString, out var structureType))
                    return OperationResult<StructureResult>.Fail("TypeNotSupported", $"Loading type '{structureTypeString}' not supported.");
                if (structureType == typeof(Nodeset))
                    return OperationResult<StructureResult>.Ok(LoadNodeset(filepath, format));
                else if (structureType == typeof(Network))
                    return OperationResult<StructureResult>.Ok(LoadNetwork(filepath, format));
                else
                    return OperationResult<StructureResult>.Fail("LoadError", $"Load not implemented for type '{structureType}'.");
            }
            catch (Exception e)
            {
                return OperationResult<StructureResult>.Fail("LoadError", $"Unexpected error while loading: {e.Message}");
            }
        }

        /// <summary>
        /// Public-facing method for saving a data structure to file. Returns an OperationResult object to inform how it went.
        /// If saving a network object, an optional nodesetFilepath argument can be provided where the nodeset should be saved.
        /// The method selects subsequent file writing function based on what kind of IStructure it is.
        /// </summary>
        /// <param name="structure">The structure to save</param>
        /// <param name="filepath">The filepath to save to.</param>
        /// <param name="format">The file format (currently only TsvGzip by default)</param>
        /// <param name="nodesetFilepath">The optional filepath where to save the nodeset (if saving a network)</param>
        /// <returns>Returns an OperationResult informing how well it went.</returns>
        public static OperationResult Save(IStructure structure, string filepath, FileFormat format = FileFormat.TsvGzip, string? nodesetFilepath = null)
        {
            try
            {
                return structure switch
                {
                    Nodeset nodeset => SaveNodeset(nodeset, filepath, format),
                    Network network => SaveNetwork(network, filepath, format, nodesetFilepath),
                    _ => OperationResult.Fail("UnsupportedType", $"Save not implemented for type '{structure.GetType().Name}'.")
                };
            }
            catch (Exception e)
            {
                return OperationResult.Fail("SaveError", $"Unexpected error while saving: {e.Message}");
            }
        }

        /// <summary>
        /// Public-facing method to import relational data to a 1-mode or 2-mode layer from a data file.
        /// The file can be either as an edgelist or a matrix/table.
        /// </summary>
        /// <param name="filepath">The filepath to the file to import.</param>
        /// <param name="network">The Network object to import the data to.</param>
        /// <param name="layer">The ILayer to install the data to.</param>
        /// <param name="format">A string describing the file format (either 'edgelist' or 'matrix')</param>
        /// <param name="separator">The value-separating character/string used in the data file.</param>
        /// <param name="addMissingNodes">A boolean indicating whether newly discovered node id's should be added to the Nodeset of the network or not</param>
        /// <returns>Returns an OperationResult informing how well this went.</returns>
        public static OperationResult ImportLayer(string filepath, Network network, ILayer layer, string format, string separator, bool addMissingNodes)
        {
            try
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
                return OperationResult.Ok();
            }
            catch (Exception e)
            {
                return OperationResult.Fail("LoadError", $"Unexpected error while loading: {e.Message}");
            }
        }

        /// <summary>
        /// Sets the current working directory as safe as possible.
        /// </summary>
        /// <param name="path">The path to the new working directory.</param>
        /// <param name="baseDirectory">An optional argument specifying the parent folder within which the new working directory must be within.</param>
        /// <returns>Returns an OperationResult informing how well it went.</returns>
        public static OperationResult SafeSetCurrentDirectory(string path, string? baseDirectory = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return OperationResult.Fail("MissingFilePath", "Path cannot be null or whitespace.");
                string fullPath = baseDirectory == null ? Path.GetFullPath(path) : Path.GetFullPath(path, baseDirectory);
                if (baseDirectory != null)
                {
                    string fullBase = Path.GetFullPath(baseDirectory);
                    if (!fullPath.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
                        return OperationResult.Fail("UnauthorizedFilepath", $"Path '{fullPath}' is outside the allowed base directory '{fullBase}'.");
                }
                if (!Directory.Exists(fullPath))
                    return OperationResult.Fail("FilepathNotFound", $"Directory does not exist: '{fullPath}'.");
                Directory.SetCurrentDirectory(fullPath);
                return OperationResult.Ok($"Set current working directory to '{fullPath}'.");

            }
            catch (Exception e)
            {
                return OperationResult.Fail("SetWorkingDirectoryError", $"Unexpected error while setting working directory: {e.Message}");
            }
        }

        /// <summary>
        /// Gets the current working directory, wrapped as a string in an OperationResult object.
        /// </summary>
        /// <returns>Returns an OperationResult informing how well this went.</returns>
        public static OperationResult<string> GetCurrentDirectory()
        {
            try
            {
                string currDir = Directory.GetCurrentDirectory();
                return OperationResult<string>.Ok(currDir);
            }
            catch (Exception e)
            {
                return OperationResult<string>.Fail("IOError", $"An unexpected error when getting current directory: {e.Message}.");
            }
        }
        #endregion

        #region Methods (private)
        /// <summary>
        /// Internal method to save a Nodeset structure to file
        /// </summary>
        /// <param name="nodeset">The Nodeset to save.</param>
        /// <param name="filepath">The filepath to save to.</param>
        /// <param name="format">The file format to save to (only FileFormat.TsvGzip implemented so far).</param>
        /// <returns>Returns an OperationResult informing how well it went.</returns>
        private static OperationResult SaveNodeset(Nodeset nodeset, string filepath, FileFormat format)
        {
            try
            {
                switch (format)
                {
                    case FileFormat.TsvGzip:
                        CompressedTsvSerializer.SaveNodesetToFile(nodeset, filepath);
                        return OperationResult.Ok($"Saved nodeset '{nodeset.Name}' to file: {filepath}");
                    default:
                        return OperationResult.Fail("UnsupportedFormat", $"Save format '{format}' not supported.");
                }
            }
            catch (Exception e)
            {
                return OperationResult.Fail("IOError", $"Unexpected error while saving nodeset: {e.Message}");
            }
        }

        /// <summary>
        /// Internal method to save a Network structure to file. Could potentially also save the Nodeset associated to this network.
        /// </summary>
        /// <param name="network">The Network to save.</param>
        /// <param name="filepath">The filepath to save to.</param>
        /// <param name="format">The file format to save to (only FileFormat.TsvGzip implemented so far).</param>
        /// <param name="nodesetFilepath">The filepath to save the Nodeset to (optional)</param>
        /// <returns></returns>
        private static OperationResult SaveNetwork(Network network, string filepath, FileFormat format, string? nodesetFilepath = null)
        {
            try
            {
                switch (format)
                {
                    case FileFormat.TsvGzip:
                        if (!string.IsNullOrEmpty(nodesetFilepath))
                            CompressedTsvSerializer.SaveNodesetToFile(network.Nodeset, nodesetFilepath);
                        CompressedTsvSerializer.SaveNetworkToFile(network, filepath, nodesetFilepath);
                        return OperationResult.Ok($"Saved network '{network.Name}' to file: {filepath}");
                    default:
                        return OperationResult.Fail("UnsupportedFormat", $"Save format '{format}' not supported.");
                }
            }
            catch (Exception e)
            {
                return OperationResult.Fail("IOError", $"Unexpected error while saving network: {e.Message}");
            }
        }

        /// <summary>
        /// Internal method to load a Nodeset from file.
        /// Throws exceptions if something happens, though these are always caught by caller.
        /// </summary>
        /// <param name="filepath">The filepath to load from.</param>
        /// <param name="format">The file format to save to (only FileFormat.TsvGzip implemented so far).</param>
        /// <returns>A StructureResult containing the Nodeset object.</returns>
        /// <exception cref="Exception">Thrown if there is an error loading the Nodeset.</exception>
        /// <exception cref="NotImplementedException">Thrown if a non-implemented file format is used.</exception>
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

        /// <summary>
        /// Internal method to load a Network from file. If the file for the network refers to a Nodeset,
        /// that Nodeset is also loaded.
        /// Throws exceptions if something happens, though these are always caught by caller.
        /// </summary>
        /// <param name="filepath">The filepath to load from.</param>
        /// <param name="format">The file format to save to (only FileFormat.TsvGzip implemented so far).</param>
        /// <returns>Returns a StructureResult containing the network and, when applicable, the Nodeset.</returns>
        /// <exception cref="Exception">Thrown if there is an error loading the Network.</exception>
        /// <exception cref="NotImplementedException">Thrown if a non-implemented file format is used.</exception>
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
        #endregion

    }
}
