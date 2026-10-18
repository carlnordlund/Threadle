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
        public string Syntax => "[double] = density(network = [var:network], layername = [str], *samplesize = [int(default:0)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates and returns the density of the layer 'layername' of the specified network. Treats all existing ties as binary ties. Works for both 1-mode and 2-mode networks. 1-mode is always exact. 2-mode defaults (samplesize 0) to exact too - summing every node's projected degree, same technique dyadcensus() uses for its exact Mutual count - which can take a bit longer than 1-mode on a very large layer; set samplesize to a positive value below the node count to instead estimate it from that many randomly sampled nodes.";

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
            var densityResult = Analyses.Density(network, layerName, sampleSize);
            return CommandResult.FromOperationResult(densityResult, densityResult.Value);
        }
    }
}
