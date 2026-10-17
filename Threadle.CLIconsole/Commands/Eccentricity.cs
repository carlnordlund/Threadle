using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'eccentricity' CLI command.
    /// </summary>
    public class Eccentricity : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "eccentricity(network=[var:network], layername=[str], *attrname=[str], *traversal=['out'(default),'in','both'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates each node's eccentricity (the length of the longest shortest path to any node reachable from it) in the specified layer — 1-mode or 2-mode (pseudo-projected, traversed node-hyperedge-node directly without materializing a 1-mode projection) — storing the result as an int node attribute (default name 'eccentricity'). Unlike diameter(), eccentricity is computed for every node individually, not restricted to the largest weakly connected component: a node in a smaller component gets its own component's eccentricity, and an isolated node gets 0. For directed (1-mode) layers, 'traversal' controls which direction BFS follows. The per-node BFS passes run in parallel across up to 'maxthreads' threads (see 'setting()').";

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
            string attrName = command.GetArgumentParseString("attrname", "");
            string traversalStr = command.GetArgumentParseString("traversal", "out");
            EdgeTraversal traversal = traversalStr.ToLowerInvariant() switch
            {
                "in" => EdgeTraversal.In,
                "both" => EdgeTraversal.Both,
                _ => EdgeTraversal.Out
            };
            return CommandResult.FromOperationResult(Analyses.Eccentricity(network, layerName, attrName.Length > 0 ? attrName : null, traversal));
        }
    }
}