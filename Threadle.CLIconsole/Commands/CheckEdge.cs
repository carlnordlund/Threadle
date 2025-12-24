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
    /// Class representing the 'checkedge' CLI command.
    /// </summary>
    public class CheckEdge : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[bool] = checkedge(network = [var:network], layername = [str], node1id = [int], node2id = [int])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Checks if an edge exists between node1id (from) and node2id (to) in the specified layer 'layername', which can be either 1-mode or 2-mode. If the layer is 1-mode directional, node1id is the source and node2id is the destination. Returns the string 'true' if an edge is found, otherwise 'false'. For 2-mode layers, an edge exists if the two nodes share at least one affiliation (hyperedge).";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="Command"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(Command command, CommandContext context)
        {
            string networkName = command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0");
            if (CommandHelpers.TryGetVariable<Network>(context, networkName, out var network) is CommandResult commandResult)
                return commandResult;
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            uint node1id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node1id", "arg2");
            uint node2id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node2id", "arg3");
            var result = network.CheckEdgeExists(layerName, node1id, node2id);
            // Will this work? Not yet tested.
            // Issue: OperationResult has a value with it, and I'm attaching that
            // as the payload for the CommandResult object

            return CommandResult.FromOperationResult(result, result.Value);
            //if (result.Success)
            //    return CommandResult.Ok(result.Message, result.Value);
            //else
            //    return CommandResult.Fail(result.Code, result.Message);
        }
    }
}
