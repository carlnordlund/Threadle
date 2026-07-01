using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'pagerank' CLI command.
    /// </summary>
    public class PageRank : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "pagerank(network=[var:network], *layernames=[semicolon-separated], *attrname=[str], *dampingfactor=[double], *maxiterations=[int], *tolerance=[double])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates PageRank scores for each node and stores the result as a node attribute. Uses directed out-edges (projected adjacency for 2-mode layers). Dangling nodes (no out-edges) redistribute their mass uniformly. Default damping factor 0.85. Iterates until convergence within tolerance or maxiterations is reached. See Page, Brin, Motwani, Winograd (1999).";

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
            double dampingFactor = command.GetArgumentParseDouble("dampingfactor", 0.85);
            int maxIterations = command.GetArgumentParseInt("maxiterations", 100);
            double tolerance = command.GetArgumentParseDouble("tolerance", 1e-8);
            string[]? layers = layerNames.Length > 0 ? layerNames.Split(';') : null;
            string? attr = attrName.Length > 0 ? attrName : null;
            return CommandResult.FromOperationResult(Analyses.PageRank(network, layers, attr, dampingFactor, maxIterations, tolerance));
        }
    }
}
