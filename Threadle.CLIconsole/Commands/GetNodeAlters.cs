using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'getnodealters' CLI command.
    /// </summary>
    public class GetNodeAlters : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[array:uint] = getnodealters(network = [var:network], layername = [str], nodeid = [uint], *direction = ['both'(default),'in','out'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Get the id of the alters to a specific node in a specific layer in the Network with the variable name [var:network]. Output is in standard JSON array format. By default, both in- and outbound ties are included in the set of alters, but this can be adjusted with the optional direction argument.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="Command"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(Command command, CommandContext context)
        {
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            uint nodeid = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg2");
            EdgeTraversal edgeTraversal = command.GetArgumentParseEnum<EdgeTraversal>("direction", EdgeTraversal.Both);
            OperationResult<uint[]> result = network.GetNodeAlters(layerName, nodeid, edgeTraversal);
            if (!result.Success)
                return CommandResult.Fail(result.Code, result.Message);
            return CommandResult.Ok(result.Message, result.Value);
        }
    }
}
