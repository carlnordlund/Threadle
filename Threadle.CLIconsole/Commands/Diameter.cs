using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'diameter' CLI command.
    /// </summary>
    public class Diameter : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "diameter(network=[var:network], layername=[str], *samplesize=[int(default:0)], *numsweeps=[int(default:4)], *traversal=['out'(default),'in','both'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Computes network diameter (the longest shortest path) and average path length for a single 1-mode layer, restricted to the largest weakly connected component — ComponentSize and TotalNodes are both reported for transparency. When samplesize is 0 (default), both are computed exactly via a full BFS from every node in that component, run in parallel across up to 'maxthreads' threads (see 'setting()'); the result carries Diameter, AvgDistance, PairsConsidered and Method = 'Exact'. When samplesize > 0, AvgDistance is instead estimated from that many random sources, with a StandardError and 95% confidence interval (normal approximation of a mean over each sampled source's own mean distance to its reachable nodes). Diameter cannot be estimated the same way, so instead a DiameterLowerBound is reported from 'numsweeps' parallel double-sweep trials (pick a random node, BFS to its farthest node, then BFS again from there — the largest distance seen across trials is a lower bound on the true diameter, often exact in practice but not guaranteed) — a lower bound rather than a confidence interval, since it is an extreme-value statistic, not a mean. For directed layers, 'traversal' controls which edge direction BFS follows; unreachable pairs are simply excluded from both statistics.";

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
            int sampleSize = command.GetArgumentParseInt("samplesize", 0);
            int numSweeps = command.GetArgumentParseInt("numsweeps", 4);
            string traversalStr = command.GetArgumentParseString("traversal", "out");
            EdgeTraversal traversal = traversalStr.ToLowerInvariant() switch
            {
                "in" => EdgeTraversal.In,
                "both" => EdgeTraversal.Both,
                _ => EdgeTraversal.Out
            };
            var result = Analyses.Diameter(network, layerName, sampleSize, numSweeps, traversal);
            return CommandResult.FromOperationResult(result, result.Value);
        }
    }
}