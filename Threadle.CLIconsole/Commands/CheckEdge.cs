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
    /// Class representing the 'checkedge' CLI command.
    /// </summary>
    public class CheckEdge : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[bool] = checkedge(network = [var:network], layername = [str], node1Id = [int], node2Id = [int])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Checks if an edge exists between node1Id (from) and node2Id (to) in the specified layer 'layername', which can be either 1-mode or 2-mode. If the layer is 1-mode directional, node1Id is the source and node2Id is the destination. Returns the string 'true' if an edge is found, otherwise 'false'. For 2-mode layers, an edge exists if the two nodes share at least one affiliation (hyperedge).";

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
            uint node1Id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node1Id", "arg2");
            uint node2Id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node2Id", "arg3");
            var result = network.CheckEdgeExists(layerName, node1Id, node2Id);
            return CommandResult.FromOperationResult(result, result.Value);
        }
    }
}
