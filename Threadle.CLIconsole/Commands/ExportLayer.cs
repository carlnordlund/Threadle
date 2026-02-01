using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the '[...]' CLI command.
    /// </summary>
    public class ExportLayer : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "exportlayer(network = [var:network], layername = [str], file = \"[str]\", *header = ['true'(true),'false'], *sep = [char(default:'\\t')])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Exports data of an existing layer 'layername' in an existing network 'network' to file 'file'. The exported file is in edgelist format only: due to potentially very large files, matrix format is not available as an export format. The layer can either be a 1-mode or 2-mode layer. For binary 1-mode layers, the edgelist consists of two columns, where the column header is 'from'/'to' for directional layers, and 'node1'/'node2' for symmetric layers. For valued 1-mode layers, the edgelist has a third column where the column header is 'value'. For 2-mode layers, the first column is 'node' and the second column is 'affiliation' (i.e. hyperedge name). All values are separated with the tab character by default, but this can be set with the 'sep' argument. The header row is shown by default, but this can be disabled with the 'header' argument.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            string filepath = command.GetArgumentThrowExceptionIfMissingOrNull("file", "arg2");
            char separator = command.GetArgumentParseString("sep", "\t")[0];
            bool header = command.GetArgumentParseBool("header", true);

            OperationResult result;

            if (!network.Layers.TryGetValue(layerName, out var layer))
                return CommandResult.Fail("LayerNotFound", $"!Error: Layer '{layerName}' not found.");
            result = FileManager.ExportLayerEdgelist(layer, filepath, separator, header);

            //if (layer is LayerOneMode layerOneMode)
            //    result = FileManager.ExportOneModeEdgeList(layerOneMode, filepath, separator, header);
            //else if (layer is LayerTwoMode layerTwoMode)
            //    result = FileManager.ExportTwoModeEdgeList(layerTwoMode, filepath, separator, header);
            //else
            //    return CommandResult.Fail("NotImplemented", $"Exporting layer type not implemented: {layer.GetType().Name}");

            return CommandResult.FromOperationResult(result);
        }
    }
}
