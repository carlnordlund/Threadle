using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Utilities;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'createnetwork' CLI command.
    /// </summary>
    public class CreateNetwork : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[var:network] = createnetwork(nodeset = [var:nodeset], *name = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates a network using the provided [var:nodeset], giving the network the optional name 'name' and assigning it to the variable [var:network].";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => true;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console variable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            string variableName = command.GetAssignmentVariableNameThrowExceptionIfNull();
            if (CommandHelpers.TryGetVariable<Nodeset>(context, command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0"), out var nodeset) is CommandResult commandResult)
                return commandResult;
            string nameNetwork = command.GetArgumentParseString("name", variableName);
            Network network = new Network(nameNetwork, nodeset);
            context.SetVariable(variableName, network);

            return CommandResult.Ok(
                message: $"Network '{nameNetwork}' created.",
                assignments: CommandResult.Assigning(variableName, typeof(Network))
                );
        }
    }
}
