using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'shortestpath' CLI command.
    /// </summary>
    public class ShortestPath : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[int/dict] = shortestpath(network = [var:network], node1id = [uint], node2id = [uint], *layernames = [semicolon-separated], *returnpath = ['false'(default),'true'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates the shortest path from node1id to node2id in a network. Uses all layers unless specific layers are specified as a semicolon-separated list. Note that shortest path measures are directional: for directional layers, the shortest path may indeed be different in the other direction. For symmetric layers, this is however moot. Returns -1 if no path exists. Set returnpath=true to also return the sequence of node ids along the shortest path.";

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
            uint node1id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node1id", "arg1");
            uint node2id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node2id", "arg2");
            string layerNames = command.GetArgumentParseString("layernames", "");
            bool returnPath = command.GetArgumentParseBool("returnpath", false);
            string[]? layers = (layerNames.Length > 0) ? layerNames.Split(';') : null;

            var result = Analyses.ShortestPath(network, layers, node1id, node2id, returnPath);
            if (!result.Success)
                return CommandResult.FromOperationResult(result);

            int distance = result.Value!.Distance;

            if (distance == -1)
                return CommandResult.Ok($"No path found from node '{node1id}' to '{node2id}'", -1);

            if (!returnPath || result.Value.Path == null)
                return CommandResult.Ok($"Shortest path from node '{node1id}' to '{node2id}' is {distance}", distance);

            string pathStr = string.Join(";", result.Value.Path);
            return CommandResult.Ok(
                $"Shortest path from node '{node1id}' to '{node2id}' is {distance}: {pathStr}",
                new Dictionary<string, object> { ["Distance"] = distance, ["Path"] = pathStr });
        }
    }
}