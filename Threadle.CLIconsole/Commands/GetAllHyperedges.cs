using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'getallhyperedges' CLI command.
    /// </summary>
    public class GetAllHyperedges : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[array:str] = getallhyperedges(network = [var:network], layername = [str], *offset = [int(default:0)], *limit = [int(default:1000])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Returns a JSON-ready array/vector of the hyperedge names that exist in the specified 2-mode layer and network. Will return maximum 1000 by default, but this can be changed with the 'limit' argument. Starts with the first one (index zero) by default, but this can be adjusted with 'offset'. Thus, if there are more than 1000 hyperedges, all can be obtained either by using 'offset' for pagination and/or increasing the 'limit'.";

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
            int offset = command.GetArgumentParseInt("offset", 0);
            int limit = command.GetArgumentParseInt("limit", 1000);
            OperationResult<string[]> result = network.GetAllHyperedges(layerName, offset, limit);
            return CommandResult.FromOperationResult(result, result.Value);
        }
    }
}
