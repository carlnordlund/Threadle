using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'density' CLI command.
    /// </summary>
    public class Density : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[double] = density(network = [var:network], layername = [str], *samplesize = [int(default:200)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates and returns the density of the layer 'layername' of the specified network. Treats all existing ties as binary ties. Works for both 1-mode and 2-mode networks. 1-mode is always exact. 2-mode defaults to estimating from a 200-node sample: exact 2-mode density costs O(sum of hyperedge sizes squared) - the same order as materializing a full projection, since it needs every node's true deduplicated degree - so unlike most other samplesize-bearing commands, it is not the default here. Set samplesize=0 to get the exact value anyway, or a positive value to change the sample size.";

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
            int sampleSize = command.GetArgumentParseInt("samplesize", 200);
            var densityResult = Analyses.Density(network, layerName, sampleSize);
            return CommandResult.FromOperationResult(densityResult, densityResult.Value);
        }
    }
}
