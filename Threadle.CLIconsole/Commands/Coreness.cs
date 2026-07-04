using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'coreness' CLI command.
    /// </summary>
    public class Coreness : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "coreness(network=[var:network], *layernames=[semicolon-separated], *attrname=[str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Computes the coreness (k-shell index) of each node via k-core decomposition, storing the result as an integer node attribute. A node's coreness is the highest k such that it belongs to the k-core — the maximal subgraph where every node has degree at least k. Edge weights and directionality are ignored (all edges treated as undirected binary). Only 1-mode layers are accepted — project 2-mode layers first with projecttwomodetoonemode(). Reference: Batagelj & Zaversnik (2003) arXiv:cs/0310049.";

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
            string layerNames = command.GetArgumentParseString("layernames", "");
            string? attrName = command.GetArgumentParseString("attrname", "");
            string[]? layers = layerNames.Length > 0 ? layerNames.Split(';') : null;
            return CommandResult.FromOperationResult(Analyses.Coreness(network, layers, attrName));
        }
    }
}
