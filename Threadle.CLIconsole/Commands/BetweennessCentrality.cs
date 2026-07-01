using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'betweennesscentrality' CLI command.
    /// </summary>
    public class BetweennessCentrality : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "betweennesscentrality(network=[var:network], *layernames=[semicolon-separated], *attrname=[str], *samplesize=[int], *directed=['true'(default),'false'], *traversal=['out'(default),'in','both'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates betweenness centrality for each node using Brandes' algorithm and stores the result as a node attribute. Normalized by (n-1)(n-2) for directed networks, halved for undirected. When samplesize > 0, a random subset of source nodes is used and scores are scaled accordingly. See Brandes (2001); sampling: Brandes & Pich (2007).";

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
            int sampleSize = command.GetArgumentParseInt("samplesize", 0);
            bool directed = command.GetArgumentParseBool("directed", true);
            string traversalStr = command.GetArgumentParseString("traversal", "out");
            EdgeTraversal traversal = traversalStr.ToLowerInvariant() switch
            {
                "in" => EdgeTraversal.In,
                "both" => EdgeTraversal.Both,
                _ => EdgeTraversal.Out
            };
            string[]? layers = layerNames.Length > 0 ? layerNames.Split(';') : null;
            string? attr = attrName.Length > 0 ? attrName : null;
            return CommandResult.FromOperationResult(Analyses.BetweennessCentrality(network, layers, attr, sampleSize, directed, traversal));
        }
    }
}
