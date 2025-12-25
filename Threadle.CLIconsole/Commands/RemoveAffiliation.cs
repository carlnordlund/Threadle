using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'removeaff' CLI command.
    /// </summary>
    internal class RemoveAffiliation : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "removeaff(network = [var:network], layername = [str], nodeid = [uint], hypername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Removes the affiliation (if one exists) between node 'nodeid' and the hyperedge named 'hypername' in the specified layer 'layername' in network [var:network]. The specified layer must be 2-mode. Gives a warning if the node-hyperedge affiliation does not exist.";

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
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg2");
            string hyperName = command.GetArgumentThrowExceptionIfMissingOrNull("hypername", "arg3");
            return CommandResult.FromOperationResult(network.RemoveAffiliation(layerName, hyperName, nodeId));
        }
    }
}
