using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;
using Threadle.Core.Utilities.Enums;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'export()' CLI command.
    /// </summary>
    public class Export : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "export(network = [var:network], format = ['gexf','graphml','pajek'], file = [str], +layername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Exports a network to an external file format: 'gexf' (Gephi format), 'graphml' or 'pajek' (Pajek .net). As gexf is a single-layer format, the layer to export must be specified with the 'layername' argument. For graphml, all nodes, node attributes and layers are exported, unless 'layername' is given, in which case only that layer is exported. When exporting several layers to graphml, each edge gets a 'layer' attribute, and 1-mode layers are exported as edges and 2-mode layers as hyperedges. For pajek, the whole network is always exported ('layername' is ignored): every 1-mode layer becomes its own '*Arcs' (directed) or '*Edges' (undirected) section, named via a ':k \"name\"' suffix when there is more than one; 2-mode layers are skipped, since Pajek .net has no hyperedge concept. Pajek .net also has no general node-attribute mechanism, so only each node's own id is written, as its vertex label; nodes are renumbered to Pajek's required dense 1..N range for the file, with the original id kept visible in that label. Such a file can be imported back with 'importnetwork()'.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console variable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            // This is implemented in preparation of future additional export formats, e.g. Pajek's net etc.
            // The format resolver is however in the FileManager.ExportNetworkToFile - here is just parsing
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;
            ExportFormat format = command.GetArgumentParseEnumThrowExceptionIfMissingOrNull<ExportFormat>("format", "arg1");
            string file = command.GetArgumentThrowExceptionIfMissingOrNull("file", "arg2");
            string layerName = command.GetArgumentParseString("layername", "");
            return CommandResult.FromOperationResult(FileManager.ExportNetworkToFile(network, format, layerName, file));
        }
    }
}