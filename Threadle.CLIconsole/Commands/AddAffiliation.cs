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
    /// Class representing the 'addaff' CLI command.
    /// </summary>
    public class AddAffiliation : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "addaff(network = [var:network], layername = [str], nodeid = [uint], hypername = [str], *addmissingnode = ['true'(default),'false'], *addmissingaffiliation = ['true'(default),'false'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Adds an affiliation between node 'nodeid' and the hyperedge named 'hypername' in layer 'layername' in network [var:network]. The specified layer must be 2-mode. If the node-hyperedge affiliation already exists, nothing happens. If the specified node id does not exist in the Nodeset, it is by default created and added, but by setting 'addmissingnodes' to 'false' prevents this. If the specified hyperedge does not exist, it is by default created and added, but by setting 'addmissingaffiliations' to 'false' prevents this.";

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
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg2");
            string hyperName = command.GetArgumentThrowExceptionIfMissingOrNull("hypername", "arg3");
            bool addMissingNode = command.GetArgumentParseBool("addmissingnodes", true);
            bool addMissingAffiliation = command.GetArgumentParseBool("addmissingaffiliations", true);
            return CommandResult.FromOperationResult(network.AddAffiliation(layerName, hyperName, nodeId, addMissingNode, addMissingAffiliation));
        }
    }
}
