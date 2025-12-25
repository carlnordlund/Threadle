using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'addhyper' CLI command.
    /// </summary>
    public class AddHyper : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "addhyper(network = [var:network], layername = [str], hypername = [str], *nodes = [semicolon-separated uints], *addmissingnodes = ['true'(default),'false'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates and adds a hyperedge (aka context or affiliation) called 'hypername' to layer 'layername' in the specified network. The layer must be 2-mode. This can then represent a particular school class or specific social event to which a set of nodes are affiliated. If there already is a hyperedge with the specified name, that is first removed. Can be provided with an optional list of nodes that are connected by this hyperedge. If the specified node id's do not exist in the Nodeset, they are by default created and added, but by setting 'addmissingnodes' to 'false' prevents this. Duplicate node id values are discarded.";

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
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            string hyperName = command.GetArgumentThrowExceptionIfMissingOrNull("hypername", "arg2");
            //OperationResult opResult;
            if (command.GetArgument("nodes") is string nodesString)
            {
                uint[] nodeIds = Misc.NodesIdsStringToArray(nodesString)
                    ?? throw new Exception($"!Error: Couldn't parse semicolon-separated list with nodes.");
                bool addMissingNodes = command.GetArgumentParseBool("addmissingnodes", true);
                return CommandResult.FromOperationResult(network.AddHyperedge(layerName, hyperName, nodeIds, addMissingNodes));
                //opResult = network.AddHyperedge(layerName, hyperName, nodeIds, addMissingNodes);
            }
            else
                return CommandResult.FromOperationResult(network.AddHyperedge(layerName, hyperName));
                //opResult = network.AddHyperedge(layerName, hyperName);
            //return CommandResult.FromOperationResult(opResult);
        }
    }
}
