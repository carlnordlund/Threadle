using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'triadiccensus' CLI command.
    /// </summary>
    public class TriadicCensus : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "triadiccensus(network=[var:network], layername=[str], *samplesize=[int])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Computes the Holland-Leinhardt triadic census for a single 1-mode layer: counts of the 16 isomorphism classes of directed triads (003, 012, 102, 021D, 021U, 021C, 111D, 111U, 030T, 030C, 201, 120D, 120U, 120C, 210, 300), plus the total number of triples and a Method field ('Exact' or 'Sampled'). For undirected layers every present dyad is mutual, so only 003/102/201/300 can be non-zero. When samplesize is 0 (default), computes the exact census. When samplesize > 0, classifies that many random node triples instead and extrapolates, adding SampleSize plus per-type StandardErrors, ConfidenceIntervalLower and ConfidenceIntervalUpper (95% Wilson score interval, which stays non-degenerate even when a rare type is observed zero times in the sample) — useful when the network is too large for exact computation. Reference: Holland & Leinhardt (1970) doi:10.1086/224727.";

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
            var result = Analyses.TriadicCensus(network, layerName, sampleSize);
            return CommandResult.FromOperationResult(result, result.Value);
        }
    }
}
