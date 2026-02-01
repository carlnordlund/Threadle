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
    /// Public-facing class for loading and saving data in Threadle.
    /// </summary>
    public static class FileManager
    {

        #region Methods (public)
        /// <summary>
        /// Public-facing method for loading a data structure from file. Returns an OperationResult that holds
        /// a StructureResult, which in turn can hold one or more IStructure objects.
        /// Catches all exceptions, converts to OperationResults.
        /// Note that file import methods use internal methods that bypass validation for performance.
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
                    _ => OperationResult<StructureResult>.Fail("IOLoadError", $"Load not implemented for type '{structureTypeString}'.")
                };
            }
            catch (Exception e)
            {
                return OperationResult<StructureResult>.Fail("IOLoadError", $"Unexpected error while loading: {e.Message}");
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
                    _ => OperationResult.Fail("UnsupportedStructureType", $"Save not implemented for type '{structure.GetType().Name}'.")
                };
            }
            catch (Exception e)
            {
                return OperationResult.Fail("IOSaveError", $"Unexpected error while saving: {e.Message}");
            }
        }

        /// <summary>
        /// Public-facing method for importing an edgelist from file to a 1-mode layer.
        /// </summary>
        /// <param name="filepath">The filepath to the file to import.</param>
        /// <param name="network">The Network object to import the data to.</param>
        /// <param name="layer">The <see cref="LayerOneMode"/> to install the data to.</param>
        /// <param name="node1col">The column index where the first node ids are.</param>
        /// <param name="node2col">The column index where the second node ids are.</param>
        /// <param name="valueCol">The column index where edge values are (for layers with valued edges).</param>
        /// <param name="hasHeader">Boolean to indicate whether the first line has headers (that should be ignored).</param>
        /// <param name="separator">The value-separating character/string used in the data file.</param>
        /// <param name="addMissingNodes">A boolean indicating whether newly discovered node id's should be added to the Nodeset of the network or not.</param>
        /// <returns>An OperationResult informing how well it went.</returns>
        public static OperationResult ImportOneModeEdgeList(string filepath, Network network, LayerOneMode layer, int node1col, int node2col, int valueCol, bool hasHeader, char separator, bool addMissingNodes)
        {
            try
            {
                LayerImportExport.ImportOneModeEdgelist(filepath, network, layer, node1col, node2col, valueCol, hasHeader, separator, addMissingNodes);
                return OperationResult.Ok($"Imported edgelist to 1-mode layer '{layer.Name}'");

            }
            catch (Exception ex)
            {
                return OperationResult.Fail("IOImportError", "Unexpected error when importing edgelist to 1-mode layer: " + ex.Message);
            }
        }

        /// <summary>
        /// Public-facing method for importing a matrix from file to a 1-mode layer.
        /// </summary>
        /// <param name="filepath">The filepath to the file to import.</param>
        /// <param name="network">The Network object to import the data to.</param>
        /// <param name="layer">The <see cref="LayerOneMode"/> to install the data to.</param>
        /// <param name="separator">The value-separating character/string used in the data file.</param>
        /// <param name="addMissingNodes">A boolean indicating whether newly discovered node id's should be added to the Nodeset of the network or not.</param>
        /// <returns>An OperationResult informing how well it went.</returns>
        public static OperationResult ImportOneModeMatrix(string filepath, Network network, LayerOneMode layer, char separator, bool addMissingNodes)
        {
            try
            {
                LayerImportExport.ImportOneModeMatrix(filepath, network, layer, separator, addMissingNodes);
                return OperationResult.Ok($"Imported matrix to 1-mode layer '{layer.Name}'");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("IOImportError", "Unexpected error when importing matrix to 1-mode layer: " + ex.Message);
            }
        }

        //public static OperationResult ExportOneModeEdgeList(LayerOneMode layerOneMode, string filepath, char separator, bool header)
        //{
        //    try
        //    {
        //        LayerImporters.ExportOneModeEdgeList(layerOneMode, filepath, separator, header);
        //        return OperationResult.Ok($"Exported 1-mode layer '{layerOneMode.Name}' to file: {filepath}");
        //    }
        //    catch (Exception ex)
        //    {
        //        return OperationResult.Fail("IOExportError", "Unexpected error when exporting 1-mode layer to edgelist:: " + ex.Message);
        //    }
        //}

        /// <summary>
        /// Public-facing method for importing an edgelist to a 2-mode layer.
        /// Whereas unknown nodes would be included or excluded, unknown affiliation/hyperedges are always added.
        /// </summary>
        /// <param name="filepath">The filepath to the file to import.</param>
        /// <param name="network">The Network object to import the data to.</param>
        /// <param name="layer">The <see cref="LayerTwoMode"/> to install the data to.</param>
        /// <param name="nodeCol">The column index where the node ids are.</param>
        /// <param name="affCol">The column index where the affiliation (hyerpedge) name is.</param>
        /// <param name="hasHeader">Boolean to indicate whether the first line has headers (that should be ignored).</param>
        /// <param name="separator">The value-separating character/string used in the data file.</param>
        /// <param name="addMissingNodes">A boolean indicating whether newly discovered node id's should be added to the Nodeset of the network or not.</param>
        /// <returns>An OperationResult informing how well it went.</returns>
        public static OperationResult ImportTwoModeEdgeList(string filepath, Network network, LayerTwoMode layer, int nodeCol, int affCol, bool hasHeader, char separator, bool addMissingNodes)
        {
            try
            {
                LayerImportExport.ImportTwoModeEdgelist(filepath, network, layer, nodeCol, affCol, separator, addMissingNodes);
                return OperationResult.Ok($"Imported edgelist to 2-mode layer '{layer.Name}'");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("IOImportError", "Unexpected error when importing edgelist to 2-mode layer: " + ex.Message);
            }
        }



        /// <summary>
        /// Public-facing method for importing a matrix/table to a 2-mode layer.
        /// The first column must contain node ids, the first row must contain affiliation/hyperedge names.
        /// Whereas unknown nodes would be included or excluded, unknown affiliation/hyperedges are always added.
        /// </summary>
        /// <param name="filepath">The filepath to the file to import.</param>
        /// <param name="network">The Network object to import the data to.</param>
        /// <param name="layer">The <see cref="LayerTwoMode"/> to install the data to.</param>
        /// <param name="separator">The value-separating character/string used in the data file.</param>
        /// <param name="addMissingNodes">A boolean indicating whether newly discovered node id's should be added to the Nodeset of the network or not.</param>
        /// <returns>An OperationResult informing how well it went.</param>
        public static OperationResult ImportTwoModeMatrix(string filepath, Network network, LayerTwoMode layer, char separator, bool addMissingNodes)
        {
            try
            {
                //LayerImporters.ImportTwoModeEdgelist(filepath, network, layer, nodeCol, affCol, separator, addMissingNodes);
                LayerImportExport.ImportTwoModeMatrix(filepath, network, layer, separator, addMissingNodes);
                return OperationResult.Ok($"Imported edgelist to 2-mode layer '{layer.Name}'");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("IOImportError", "Unexpected error when importing matrix/table to 2-mode layer: " + ex.Message);
            }
        }

        public static OperationResult ExportLayerEdgelist(ILayer layer, string filepath, char separator, bool header)
        {
            try
            {
                if (layer is LayerOneMode layerOneMode)
                    LayerImportExport.ExportOneModeEdgeList(layerOneMode, filepath, separator, header);
                else if (layer is LayerTwoMode layerTwoMode)
                    LayerImportExport.ExportTwoModeEdgeList(layerTwoMode, filepath, separator, header);
                else
                    return OperationResult.Fail("IOExportError", $"Did not recognize layer type of layer '{layer.Name}'.");
                return OperationResult.Ok($"Exported layer '{layer.Name}' to file: {filepath}");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("IOExportError", "Unexpected error when exporting layer to edgelist: " + ex.Message);
            }
        }


        //public static OperationResult ExportTwoModeEdgeList(LayerTwoMode layerTwoMode, string filepath, char separator, bool header)
        //{
        //    try
        //    {
        //        LayerImporters.ExportTwoModeEdgeList(layerTwoMode, filepath, separator, header);
        //        return OperationResult.Ok($"Exported 2-mode layer '{layerTwoMode.Name}' to file: {filepath}");
        //    }
        //    throw new NotImplementedException();
        //}

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
                    return OperationResult.Fail("MissingFilepath", "Path cannot be null or whitespace.");
                string fullPath = baseDirectory == null ? Path.GetFullPath(path) : Path.GetFullPath(path, baseDirectory);
                if (baseDirectory != null)
                {
                    string fullBase = Path.GetFullPath(baseDirectory);
                    if (!fullPath.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
                        return OperationResult.Fail("UnauthorizedFilepath", $"Path '{fullPath}' is outside the allowed base directory '{fullBase}'.");
                }
                if (!Directory.Exists(fullPath))
                    return OperationResult.Fail("FileNotFound", $"Directory does not exist: '{fullPath}'.");
                Directory.SetCurrentDirectory(fullPath);
                return OperationResult.Ok($"Set current working directory to '{fullPath}'.");

            }
            catch (Exception e)
            {
                return OperationResult.Fail("IOSetDirectoryError", $"Unexpected error while setting working directory: {e.Message}");
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
        /// Gets the content (directories and files) of the current working directory, wrapped as a potentially nested 
        /// string-object dictionary. Note that more information can be obtained: I have commented out that info below.
        /// </summary>
        /// <param name="path">Optional filepath: if null, the current working will be used.</param>
        /// <returns>Returns a string-object dictionary with info about the directories and files.</returns>
        public static OperationResult<Dictionary<string,object>> GetDirectoryListing(string? path = null)
        {
            try
            {
                string targetPath = string.IsNullOrEmpty(path) ? Directory.GetCurrentDirectory() : path;
                if (!Directory.Exists(targetPath))
                    return OperationResult<Dictionary<string, object>>.Fail("DirectoryNotFound", $"Directory not found: {targetPath}");
                var directories = Directory.GetDirectories(targetPath)
                    .Select(d => new DirectoryInfo(d))
                    .OrderBy(d => d.Name)
                    .Select(d => new Dictionary<string,object>
                    {
                        ["Name"] = d.Name//,
                        //["Type"] ="Directory",
                        //["Modified"] = d.LastWriteTime
                    }
                    )
                    .ToList();
                var files = Directory.GetFiles(targetPath)
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.Name)
                    .Select(f => new Dictionary<string, object>
                    {
                        ["Name"] = f.Name//,
                        //["Type"] = "File",
                        //["Size"] = f.Length,
                        //["Modified"] = f.LastWriteTime
                    }
                    )
                    .ToList();

                var result = new Dictionary<string, object>
                {
                    ["Path"] = targetPath,
                    ["Directories"] = directories,
                    ["Files"] = files,
                    ["TotalDirectories"] = directories.Count,
                    ["TotalFiles"] = files.Count
                };
                return OperationResult<Dictionary<string, object>>.Ok(result, $"Listed {directories.Count} directories and {files.Count} files.");
            }
            catch (UnauthorizedAccessException uaex)
            {
                return OperationResult<Dictionary<string, object>>.Fail("AccessDenied", "Access denied to directory: " + uaex.Message);
            }
            catch (Exception ex)
            {
                return OperationResult<Dictionary<string, object>>.Fail("DirectoryError", $"Error listing directory: {ex.Message}");
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
                return OperationResult<string[]>.Fail("IOLoadError", $"Unexpected error while loading: {e.Message}");
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
                    return OperationResult.Fail("UnsupportedFileFormat", $"Save format not supported.");

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
                        return OperationResult.Fail("UnsupportedFileFormat", $"Save format '{format}' not supported.");
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
                    return OperationResult.Fail("ConstraintUnsavedNodeset",$"Network '{network.Name}' uses nodeset '{nodeset.Name}' which is not yet saved to file. Save that first!");
                
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
                        return OperationResult.Fail("UnsupportedFileFormat", $"Save format '{format}' not supported.");
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
                    return OperationResult<StructureResult>.Fail("UnsupportedFileFormat", $"File ending '{Path.GetFileName(filepath)}' not supported for loading Nodeset.");

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
                return OperationResult<StructureResult>.Fail("UnsupportedFileFormat", $"File ending '{Path.GetFileName(filepath)}' not supported for loading Nodeset.");
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
                    return OperationResult<StructureResult>.Fail("UnsupportedFileFormat", $"File ending '{Path.GetFileName(filepath)}' not supported for loading Network.");

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
                return OperationResult<StructureResult>.Fail("UnsupportedFileFormat", $"File ending '{Path.GetFileName(filepath)}' not supported for loading Network.");
            }
            catch (Exception e)
            {
                return OperationResult<StructureResult>.Fail("IOError", $"Unexpected error while loading network: {e.Message}");
            }
        }



        #endregion
    }
}
