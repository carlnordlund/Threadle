using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'eigenvectorcentrality' CLI command.
    /// </summary>
    public class EigenvectorCentrality : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "eigenvectorcentrality(network=[var:network], *layernames=[semicolon-separated], *attrname=[str], *traversal=['both'(default),'out','in'], *maxiterations=[int], *tolerance=[double])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates eigenvector centrality via power iteration and stores the result as a node attribute. Scores are L2-normalized. For 2-mode layers uses efficient per-hyperedge summation. Typically applied with traversal=both for undirected or symmetric networks. Iterates until convergence within tolerance or maxiterations is reached. See Bonacich (1987).";

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
            string traversalStr = command.GetArgumentParseString("traversal", "both");
            int maxIterations = command.GetArgumentParseInt("maxiterations", 100);
            double tolerance = command.GetArgumentParseDouble("tolerance", 1e-8);
            EdgeTraversal traversal = traversalStr.ToLowerInvariant() switch
            {
                "out" => EdgeTraversal.Out,
                "in" => EdgeTraversal.In,
                _ => EdgeTraversal.Both
            };
            string[]? layers = layerNames.Length > 0 ? layerNames.Split(';') : null;
            string? attr = attrName.Length > 0 ? attrName : null;
            return CommandResult.FromOperationResult(Analyses.EigenvectorCentrality(network, layers, attr, traversal, maxIterations, tolerance));
        }
    }
}
