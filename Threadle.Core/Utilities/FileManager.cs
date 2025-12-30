using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.Metadata.Ecma335;

namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Public-facing class for loading and saving data in Threadle
    /// </summary>
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
                return structureTypeString.ToLower() switch
                {
                    "nodeset" => LoadNodeset(filepath),
                    "network" => LoadNetwork(filepath),
                    _ => OperationResult<StructureResult>.Fail("LoadError", $"Load not implemented for type '{structureTypeString}'.")
                };
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
        public static OperationResult Save(IStructure structure, string filepath)
        {
            try
            {
                if (filepath is null || filepath.Length == 0)
                    return OperationResult.Fail("MissingFilepath", $"No filepath for structure '{structure.Name}' provided.");
                    
                return structure switch
                {
                    Nodeset nodeset => SaveNodeset(nodeset, filepath),
                    Network network => SaveNetwork(network, filepath),
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
        public static OperationResult ImportLayer(string filepath, Network network, ILayer layer, string format, char separator, bool addMissingNodes)
        {
            try
            {
                switch (layer, format.ToLowerInvariant())
                {
                    case (LayerOneMode layerOneMode, "edgelist"):
                        LayerImporters.ImportOneModeEdgelist(filepath, network, layerOneMode, separator, addMissingNodes);
                        break;
                    //case (LayerOneMode layerOneMode, "matrix"):
                    //    LayerImporters.ImportOneModeMatrix(filepath, network, layerOneMode, separator, addMissingNodes);
                    //    break;
                    //case (LayerTwoMode layerTwoMode, "edgelist"):
                    //    LayerImporters.ImportTwoModeEdgelist(filepath, network, layerTwoMode, separator, addMissingNodes);
                    //    break;
                    //case (LayerTwoMode layerTwoMode, "matrix"):
                    //    LayerImporters.ImportTwoModeMatrix(filepath, network, layerTwoMode, separator, addMissingNodes);
                    //    break;

                    default:
                        return OperationResult.Fail("UnsupportedOption", "The specific layer/format combination for importing is not supported.");

                }
                return OperationResult.Ok("");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("ImportError", "Unexpected error when importing layer: " + ex.Message);

            }
            //try
            //{
            //    OperationResult result = (layer, format.ToLower()) switch
            //    {
            //        (LayerOneMode layerOneMode, "edgelist") => LayerImporters.ImportOneModeEdgelist(filepath, network, layerOneMode, separator, addMissingNodes),
            //        (LayerOneMode layerOneMode, "matrix") => LayerImporters.ImportOneModeMatrix(filepath, network, layerOneMode, separator, addMissingNodes),
            //        (LayerTwoMode layerTwoMode, "edgelist") => LayerImporters.ImportTwoModeEdgelist(filepath, network, layerTwoMode, separator, addMissingNodes),
            //        (LayerTwoMode layerTwoMode, "matrix") => LayerImporters.ImportTwoModeMatrix(filepath, network, layerTwoMode, separator, addMissingNodes),
            //        _ => OperationResult.Fail("UnsupportedOptions", "The specific layer/format combination for imports is not supported.")
            //    };
            //    return result;
            //}
            //catch (Exception e)
            //{
            //    return OperationResult.Fail("LoadError", $"Unexpected error while loading: {e.Message}");
            //}

            //return OperationResult.Fail("NotYetImplemented", "Redoing importlayer");
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

        /// <summary>
        /// Loads a text file and returns it as a string[] array in an OperationResult.
        /// </summary>
        /// <param name="filepath">The filepath to the text file to read.</param>
        /// <returns>An OperationResult, with a string[] array if all went well.</returns>
        public static OperationResult<string[]> LoadTextfile(string filepath)
        {
            try
            {
                var lines = TextFileReader.LoadFile(filepath);
                return OperationResult<string[]>.Ok(lines, $"Loaded {lines.Length} lines from file '{filepath}'.");
            }
            catch (Exception e)
            {
                return OperationResult<string[]>.Fail("LoadError", $"Unexpected error while loading: {e.Message}");
            }
        }
        #endregion

        #region Methods (private)
        /// <summary>
        /// Internal method to save a Nodeset to file.
        /// The file format is derived from the filepath ending.
        /// </summary>
        /// <param name="nodeset">The Nodeset to save.</param>
        /// <param name="filepath">The filepath to save to.</param>
        /// <returns>Returns an OperationResult informing how well it went.</returns>
        private static OperationResult SaveNodeset(Nodeset nodeset, string filepath)
        {
            try
            {
                FileFormat format = Misc.GetFileFormatFromFileEnding(filepath);
                if (format == FileFormat.None)
                    return OperationResult.Fail("UnsupportedFormat", $"Save format not supported.");

                switch (format)
                {
                    case FileFormat.Tsv:
                    case FileFormat.TsvGzip:
                        FileSerializerTsv.SaveNodesetToFile(nodeset, filepath, format);
                        return OperationResult.Ok($"Saved nodeset '{nodeset.Name}' to file: {filepath}");
                    case FileFormat.Bin:
                    case FileFormat.BinGzip:
                        FileSerializerBin.SaveNodesetToFile(nodeset, filepath, format);
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
        /// Internal method to save a Network structure to file. If the Nodeset that the Network refers to is not yet saved,
        /// the operation is aborted, informing that the Nodeset must be saved first. If the Nodeset has been modified since
        /// last save, this method also saves the Nodeset.
        /// Note that the file format is derived from the filepath ending.
        /// </summary>
        /// <param name="network">The Network to save.</param>
        /// <param name="filepath">The filepath to save to.</param>
        /// <returns>An OperationResult informing how it went.</returns>
        private static OperationResult SaveNetwork(Network network, string filepath)
        {
            try
            {
                // Get ref to the Nodeset that this Network is referring to.
                Nodeset nodeset = network.Nodeset;

                // Imperative that this Nodeset is first stored on file (separately, so that it has a Filepath property).
                // If not: throw back a Fail
                if (nodeset.Filepath is null || nodeset.Filepath.Length == 0)
                    return OperationResult.Fail("UnsavedNodeset",$"Network '{network.Name}' uses nodeset '{nodeset.Name}' which is not yet saved to file. Save that first!");
                
                // Get fileformat of the network based on the filepath
                FileFormat format = Misc.GetFileFormatFromFileEnding(filepath);

                switch (format)
                {
                    case FileFormat.Tsv:
                    case FileFormat.TsvGzip:
                        // If the nodeset has been modified since last save, save this first
                        if (nodeset.IsModified)
                        {
                            // Do a fileformat check on the nodeset as well and send that along to this. The Nodeset might be stored in different format
                            FileFormat formatNodeset = Misc.GetFileFormatFromFileEnding(nodeset.Filepath);
                            FileSerializerTsv.SaveNodesetToFile(nodeset, nodeset.Filepath, formatNodeset);
                            // Then save Network
                            FileSerializerTsv.SaveNetworkToFile(network, filepath, format);
                            // And return OpResult to inform that both were saved.
                            return OperationResult.Ok($"Saved network '{network.Name}' to file: {filepath}, and saved nodeset '{nodeset.Name}' to file: {nodeset.Filepath}.");
                        }
                        // Nodeset has not been modified since last save, so just save network and inform about that
                        FileSerializerTsv.SaveNetworkToFile(network, filepath, format);
                        return OperationResult.Ok($"Saved network '{network.Name}' to file: {filepath}");
                    case FileFormat.Bin:
                    case FileFormat.BinGzip:
                        if (nodeset.IsModified)
                        {
                            FileFormat formatNodeset = Misc.GetFileFormatFromFileEnding(nodeset.Filepath);
                            FileSerializerBin.SaveNodesetToFile(nodeset, nodeset.Filepath, formatNodeset);
                            FileSerializerBin.SaveNetworkToFile(network, filepath, format);
                            return OperationResult.Ok($"Saved network '{network.Name}' to file: {filepath}, and saved nodeset '{nodeset.Name}' to file: {nodeset.Filepath}.");

                        }
                        FileSerializerBin.SaveNetworkToFile(network, filepath, format);
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
        /// Returns an OperationResult wrapping a StructureResult.
        /// Note that the file format is derived from the filepath ending.
        /// </summary>
        /// <param name="filepath">The filepath to load from.</param>
        /// <returns>An OperationResult, with a StructureResult containing a Nodeset if all went well.</returns>
        private static OperationResult<StructureResult> LoadNodeset(string filepath)
        {
            try
            {
                FileFormat format = Misc.GetFileFormatFromFileEnding(filepath);
                if (format == FileFormat.None)
                    return OperationResult<StructureResult>.Fail("UnsupportedFormat", $"File ending '{Path.GetFileName(filepath)}' not supported for loading Nodeset.");

                Nodeset nodeset;
                switch (format)
                {
                    case FileFormat.Tsv:
                    case FileFormat.TsvGzip:
                        nodeset = FileSerializerTsv.LoadNodesetFromFile(filepath, format);
                        return OperationResult<StructureResult>.Ok(new StructureResult(nodeset));
                    case FileFormat.Bin:
                    case FileFormat.BinGzip:
                        nodeset = FileSerializerBin.LoadNodesetFromFile(filepath, format);
                        return OperationResult<StructureResult>.Ok(new StructureResult(nodeset));
                }
                return OperationResult<StructureResult>.Fail("UnsupportedFormat", $"File ending '{Path.GetFileName(filepath)}' not supported for loading Nodeset.");
            }
            catch (Exception e)
            {
                return OperationResult<StructureResult>.Fail("IOError", $"Unexpected error while loading nodeset: {e.Message}");
            }
        }

        /// <summary>
        /// Internal method to load a Network from file. Also loads the Nodeset that the Network refers to.
        /// Throws exceptions if something happens, though these are always caught by caller.
        /// Note that the file format is derived from the filepath ending.
        /// </summary>
        /// <param name="filepath">The filepath to load from.</param>
        /// <returns>An OperationResult, with a StructureResult containing a Network and a Nodeset if all went well.</returns>
        private static OperationResult<StructureResult> LoadNetwork(string filepath)
        {
            try
            {
                FileFormat format = Misc.GetFileFormatFromFileEnding(filepath);
                if (format == FileFormat.None)
                    return OperationResult<StructureResult>.Fail("UnsupportedFormat", $"File ending '{Path.GetFileName(filepath)}' not supported for loading Network.");

                StructureResult structureResult;
                switch (format)
                {
                    case FileFormat.Tsv:
                    case FileFormat.TsvGzip:
                        structureResult = FileSerializerTsv.LoadNetworkFromFile(filepath, format);
                        return OperationResult<StructureResult>.Ok(structureResult);
                    case FileFormat.Bin:
                    case FileFormat.BinGzip:
                        structureResult = FileSerializerBin.LoadNetworkFromFile(filepath, format);
                        return OperationResult<StructureResult>.Ok(structureResult);
                }
                return OperationResult<StructureResult>.Fail("UnsupportedFormat", $"File ending '{Path.GetFileName(filepath)}' not supported for loading Network.");
            }
            catch (Exception e)
            {
                return OperationResult<StructureResult>.Fail("IOError", $"Unexpected error while loading network: {e.Message}");
            }
        }
        #endregion
    }
}
