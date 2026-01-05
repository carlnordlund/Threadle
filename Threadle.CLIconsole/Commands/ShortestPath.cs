using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'getwd' CLI command.
    /// </summary>
    public class ShortestPath : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[int] = shortestpath(network = [var:network], node1id = [uint], node2id = [uint], *layername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates the shortest path from node1id to node2id in a network. Uses all layers unless a specific layer is specified. Note that shortest path measures are directional: for directional layers, the shortest path may indeed be different in the other direction. For symmetric layers, this is however moot.";

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
            uint node1id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node1id", "arg1");
            uint node2id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node2id", "arg2");
            string layerName = command.GetArgumentParseString("layername", "");
            var shortestPathResult = Analyses.ShortestPath(network, layerName, node1id, node2id);
            if (!shortestPathResult.Success)
                return CommandResult.FromOperationResult(shortestPathResult);
            return CommandResult.Ok($"Shortest path from node '{node1id}' to '{node2id}' is {shortestPathResult.Value}",shortestPathResult.Value);
        }
    }
}
