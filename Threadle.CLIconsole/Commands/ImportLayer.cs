using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'importlayer' CLI command.
    /// </summary>
    public class ImportLayer : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "importlayer(network = [var:network], layername = [str], file = \"[str]\", format = ['edgelist','matrix'], *node1col = [int(default:0)], *node2col = [int(default:1)], *valuecol = [int(default:2)], *nodecol = [int(default:0)], *affcol = [int(default:1)], *header = ['true','false'(default)], *sep = [char(default:'\\t')], *addmissingnodes = ['true','false'(default)], *labelattr = [str], *filtercol = [int], *filtervalue = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Imports data to an existing layer 'layername' in an existing network 'network' from file 'file'. The imported file is either in edgelist or matrix/table format, with the optional value-separating character given by 'sep' (defaults to tab). The 'addmissingnodes' boolean instructs what to do if encountering node id's that are not in the Nodeset: if set to true, these will be created and added to the Nodeset, if set to false, the relation will be ignored. Edgelists are assumed to be without headers but that can be adjusted with the 'header' argument. However, if a line can't be parsed as nodes and affiliations (as a header is), that's ok: it will just ignore. The matrix format assumes that the first row and first column are headers: this is compulsory in order to identify node ids (and affiliation names). For 1-mode layers and edgelist format, the first two column must contain node id's (for directional data, the first column is source node, and the second column is destination node). If the layer is for valued edges, a third column is expected that holds the value of ties. These columns can be adjusted with the 'node1col' and 'node2col' and, when applicable, the 'valuecol' arguments, where one can specify the column indexes to use (note that these start with 0). For 1-mode layers and matrix format, both rows and columns must contain node id's and the matrix must be square-shaped. For 2-mode layers and edgelist format, the first column contains the node id and the second column contains hyperedge labels (i.e. affiliations). This can be adjusted with the 'nodecol' and 'affcol' arguments (again, the first column has index 0). For 2-mode layers and matrix/table format, the first column (i.e. row headers) contain node ids, and the first row (i.e. column headers) contain hyperedge labels (i.e. affiliations). Importing 2-mode data will always add hyperedges when missing. By default, node identifiers in the file must be unsigned integers that are used as node ids. If the node identifiers are instead strings (e.g. names), the 'labelattr' argument can be given the name of a string node attribute (e.g. 'label'): node identifiers are then treated as labels that are looked up in this attribute. Labels not found in the Nodeset are, if 'addmissingnodes' is true, given new node ids (continuing after the highest existing node id) with their labels stored in this attribute. The attribute is created if it doesn't exist. As existing labels are reused, several layers can be imported in sequence using the same 'labelattr'. For edgelists, the 'filtercol' and 'filtervalue' arguments can be used to only import lines where column 'filtercol' has the value 'filtervalue' (e.g. when an edgelist has a column specifying the type of relation, making it possible to import each relation to its own layer).";

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
            string format = command.GetArgumentThrowExceptionIfMissingOrNull("format", "arg3");
            char separator = command.GetArgumentParseString("sep", "\t").FirstOrDefault('\t');
            bool addMissingNodes = command.GetArgumentParseBool("addmissingnodes", false);
            bool header = command.GetArgumentParseBool("header", false);
            string? labelAttr = command.GetArgument("labelattr");
            string? filterValue = command.GetArgument("filtervalue");
            int filterCol = command.GetArgumentParseInt("filtercol", -1);
            if (command.GetArgument("filtercol") is not null && filterCol < 0)
                return CommandResult.Fail("InvalidColumnIndex", $"!Error: 'filtercol' must be a non-negative integer.");
            if ((filterCol >= 0) != (filterValue is not null))
                return CommandResult.Fail("MissingArgument", $"!Error: 'filtercol' and 'filtervalue' must be provided together.");
            if (filterCol >= 0 && format.ToLowerInvariant() != "edgelist")
                return CommandResult.Fail("InvalidArgument", $"!Error: 'filtercol' and 'filtervalue' can only be used with the 'edgelist' format.");

            if (!network.Layers.TryGetValue(layerName, out var layer))
                return CommandResult.Fail("LayerNotFound", $"!Error: Layer '{layerName}' not found.");

            OperationResult result;

            switch (layer, format.ToLowerInvariant())
            {
                case (LayerOneMode layerOneMode, "edgelist"):
                    int node1col = command.GetArgumentParseInt("node1col", 0);
                    if (node1col < 0)
                        return CommandResult.Fail("InvalidColumnIndex", $"!Error: 'node1col' must be a non-negative integer.");
                    int node2col = command.GetArgumentParseInt("node2col", 1);
                    if (node2col < 0)
                        return CommandResult.Fail("InvalidColumnIndex", $"!Error: 'node2col' must be a non-negative integer.");
                    int valuecol = command.GetArgumentParseInt("valuecol", 2);
                    if (valuecol < 0)
                        return CommandResult.Fail("InvalidColumnIndex", $"!Error: 'valuecol' must be a non-negative integer.");
                    result = FileManager.ImportOneModeEdgeList(filepath, network, layerOneMode, node1col, node2col, valuecol, header, separator, addMissingNodes, labelAttr, filterCol, filterValue);
                    break;
                case (LayerOneMode layerOneMode, "matrix"):
                    result = FileManager.ImportOneModeMatrix(filepath, network, layerOneMode, separator, addMissingNodes, labelAttr);
                    break;
                case (LayerTwoMode layerTwoMode, "edgelist"):
                    int nodeCol = command.GetArgumentParseInt("nodecol", 0);
                    if (nodeCol < 0)
                        return CommandResult.Fail("InvalidColumnIndex", $"!Error: 'nodecol' must be a non-negative integer.");
                    int affCol = command.GetArgumentParseInt("affcol", 1);
                    if (affCol < 0)
                        return CommandResult.Fail("InvalidColumnIndex", $"!Error: 'affcol' must be a non-negative integer.");
                    result = FileManager.ImportTwoModeEdgeList(filepath, network, layerTwoMode, nodeCol, affCol, header, separator, addMissingNodes, labelAttr, filterCol, filterValue);
                    break;
                case (LayerTwoMode layerTwoMode, "matrix"):
                    result = FileManager.ImportTwoModeMatrix(filepath, network, layerTwoMode, separator, addMissingNodes, labelAttr);
                    break;
                default:
                    return CommandResult.Fail("UnsupportedImportFormat", $"Format '{format}' not supported for this layer type.");
            }
            return CommandResult.FromOperationResult(result);
        }
    }
}