using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Processing;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'symmetrictodirected' CLI command.
    /// </summary>
    public class SymmetricToDirected : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "symmetrictodirected(network = [var:network], layername = [str], *newlayername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Converts a symmetric (undirected) 1-mode layer into a directed layer, turning each undirected tie into two independent directed arcs (one in each direction) carrying the same value. The resulting layer keeps the original's edge value type (binary stays binary, valued stays valued). The original layer remains unchanged. The new layer will be named as the provided layer, with the addition '-directed', but the name can also be specified with the 'newlayername' argument.";

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
            string newLayerName = network.GetNextAvailableLayerName(command.GetArgumentParseString("newlayername", layerName + "-directed"));
            return CommandResult.FromOperationResult(NetworkProcessor.SymmetricToDirectedLayer(network, layerName, newLayerName));
        }
    }
}
