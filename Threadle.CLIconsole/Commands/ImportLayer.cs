using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

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
        public string Syntax => "importlayer(network = [var:network], layername = [str], file = \"[str]\", format = ['edgelist','matrix'], *sep = [char(default:'\\t')], *addmissingnodes = ['true','false'(default)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Imports data to an existing layer 'layername' in an existing network 'network' from file 'file'. The imported file is either in edgelist or matrix/table format, with the optional value-separating character given by 'sep' (defaults to tab). The 'addmissingnodes' boolean instructs what to do if encountering node id's that are not in the Nodeset: if set to true, these will be created and added to the Nodeset, if set to false, the relation will be ignored. Edgelists are assumed to be without headers, but if there is one (that cannot be parsed as nodes and affiliations), that's ok: it will just ignore. The matrix format assumes that the first row and first column are headers: this is compulsory. For 1-mode layers and edgelist format, the first two column must contain node id's - if the layer has valued data, a third column is expected that holds the value of ties. For 1-mode layers and matrix format, both rows and columns must contain node id's and the matrix must be square-shaped. For 2-mode layers and edgelist format, the first column contains the node id and the second column contains hyperedge labels (i.e. affiliations). For 2-mode layers and matrix/table format, the first column (i.e. row headers) contain node ids, and the first row (i.e. column headers) contain hyperedge labels (i.e. affiliations).";

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
            //string networkName = command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0");
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;

            //Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            string filepath = command.GetArgumentThrowExceptionIfMissingOrNull("file", "arg2");
            string format = command.GetArgumentThrowExceptionIfMissingOrNull("format", "arg3");
            string separator = command.GetArgumentParseString("sep", "\t");
            bool addMissingNodes = command.GetArgumentParseBool("addmissingnodes", false);
            if (!network.Layers.TryGetValue(layerName, out var layer))
                return CommandResult.Fail("LayerNotFound", $"!Error: Layer '{layerName}' not found.");
            OperationResult result = FileManager.ImportLayer(filepath, network, layer, format, separator, addMissingNodes);
            return CommandResult.FromOperationResult(result);
        }
    }
}