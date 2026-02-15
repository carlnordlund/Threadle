using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'gethyperedgenodes' CLI command.
    /// </summary>
    public class GetHyperedgeNodes : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[array:uint] = gethyperedgenodes(network = [var:network], layername = [str], hypername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Returns a JSON-ready array/vector of the node ids that are affiliated to the hyperedge with name 'hypername' in the specified layer and network. Note that the layer must be 2-mode.";

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
            string hyperName = command.GetArgumentThrowExceptionIfMissingOrNull("hypername", "arg2");
            OperationResult<uint[]> result = network.GetHyperedgeNodes(layerName, hyperName);
            return CommandResult.FromOperationResult(result, result.Value);
        }
    }
}
