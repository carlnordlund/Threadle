using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the '[...]' CLI command.
    /// </summary>
    public class DyadCensus : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "dyadcensus(network=[var:network], layername=[str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Computes the MAN dyad census and reciprocity measures for a single 1-mode layer. Returns counts of Mutual (both arcs present), Asymmetric (one arc only) and Null (no arc) dyad pairs, plus ArcReciprocity (proportion of arcs that are reciprocated) and DyadicReciprocity (proportion of non-null dyads that are mutual). For undirected layers all non-null dyads are trivially Mutual. Reference: Holland & Leinhardt (1970) doi:10.1086/224727.";

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
            var result = Analyses.DyadCensus(network, layerName);
            return CommandResult.FromOperationResult(result, result.Value);
        }
    }
}
