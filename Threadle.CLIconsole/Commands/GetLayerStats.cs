using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'getlayerstats' CLI command.
    /// </summary>
    public class GetLayerStats : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[str] = getlayerstats(network = [var:network], layername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates and returns summary statistics (Mean, Median, StdDev, Min, Max, Q1, Q3, EdgeCount) over the edge values of a valued 1-mode layer, using the same statistics machinery as getattrsummary(). Requires a valued layer — binary layers have no varying values to summarize, and 2-mode layers are not supported. For symmetric (undirected) layers, each edge is counted once, not once per endpoint.";

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
            var statsResult = Analyses.GetLayerStats(network, layerName);
            return CommandResult.FromOperationResult(statsResult, statsResult.Value);
        }
    }
}