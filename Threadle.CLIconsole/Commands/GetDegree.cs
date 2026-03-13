using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the '[...]' CLI command.
    /// </summary>
    public class GetDegree : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[uint] = getdegree(network = [var:network], nodeid = [uint], *layernames = [semicolon-separated layer names], *direction = ['both','in','out'(default)], *unique = [false,true(default)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates the degree centrality for the specified node in the network, either for the specified layer, layers or all layers (if layernames is omitted). By default outdegree is calculated, but this can be changed. When having multiple layers, the same alter is by default counted only once, but this can be changed with the unique parameter.";

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
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg1");
            string layerNames = command.GetArgumentParseString("layernames", "");
            string[]? layers = (layerNames.Length > 0) ? layerNames.Split(';') : null;
            EdgeTraversal edgeTraversal = command.GetArgumentParseEnum<EdgeTraversal>("direction", EdgeTraversal.Out);
            bool uniqueAlters = command.GetArgumentParseBool("unique", true);
            OperationResult<uint[]> result = network.GetNodeAlters(layers, nodeId, edgeTraversal, uniqueAlters);
            if (!result.Success)
                return CommandResult.FromOperationResult(result);
            int degree = result.Value!.Length;
            return CommandResult.Ok($"Degree of node {nodeId}: {degree}", degree);
        }
    }
}
