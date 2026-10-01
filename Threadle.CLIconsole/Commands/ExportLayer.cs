using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'exportlayer' CLI command.
    /// </summary>
    public class ExportLayer : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "exportlayer(network = [var:network], layername = [str], file = \"[str]\", *format = ['edgelist'(default),'matrix'], *header = ['true'(true),'false'], *sep = [char(default:'\\t')], *labelattr = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Exports data of an existing layer 'layername' in an existing network 'network' to file 'file'. The exported file is in edgelist or matrix format: edgelist is default but this can be set with the 'format' argument. The layer can either be a 1-mode or 2-mode layer. For binary 1-mode layers, the edgelist consists of two columns, where the column header is 'from'/'to' for directional layers, and 'node1'/'node2' for symmetric layers. For valued 1-mode layers, the edgelist has a third column where the column header is 'value'. For 2-mode layers, the first column is 'node' and the second column is 'affiliation' (i.e. hyperedge name). All values are separated with the tab character by default, but this can be set with the 'sep' argument. The header row is shown by default, but this can be disabled with the 'header' argument. Note: when exporting to matrix format, the matrices only contain the nodes (and hyperedges) that are included in this particular layer. Those nodes/hyperedges will then be sorted. By default, node identifiers are written as their numeric node id. The optional 'labelattr' argument names a node attribute (of any type) whose value is written instead, mirroring importlayer()'s 'labelattr'; a string type and unique values are preferable but not required or checked — a node lacking a value for the attribute falls back to its numeric id individually, and the command fails if 'labelattr' doesn't name an existing node attribute at all.";

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
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            string filepath = command.GetArgumentThrowExceptionIfMissingOrNull("file", "arg2");
            char separator = command.GetArgumentParseString("sep", "\t").FirstOrDefault('\t');
            bool header = command.GetArgumentParseBool("header", true);
            string format = command.GetArgumentParseString("format", "edgelist");
            string? labelAttr = command.GetArgument("labelattr");

            OperationResult result;

            if (!network.Layers.TryGetValue(layerName, out var layer))
                return CommandResult.Fail("LayerNotFound", $"!Error: Layer '{layerName}' not found.");

            result = format switch
            {
                "edgelist" => FileManager.ExportLayerEdgelist(layer, filepath, separator, header, network.Nodeset, labelAttr),
                "matrix" => FileManager.ExportLayerMatrix(layer, filepath, separator, header, network.Nodeset, labelAttr),
                _ => OperationResult.Fail("UnsupporedExportFormat", $"Format '' not supported.")
            };

            return CommandResult.FromOperationResult(result);
        }
    }
}