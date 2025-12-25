using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'info' CLI command.
    /// </summary>
    internal class Info : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[str] = info(structure = [var:structure])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Displays metadata about the structure, which is either a Nodeset or a Network. Name and Filepath are included for both structures. Network structures also provide data on existing relational layers, their properties and number of edges. Nodeset structures also provide data on number of nodes, and existing node attributes and their types.";

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
            //string structureName = command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0");
            if (CommandHelpers.TryGetVariable<IStructure>(context, command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"), out var structure) is CommandResult commandResult)
                return commandResult;

            //IStructure structure = context.GetVariableThrowExceptionIfMissing<IStructure>(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));

            Dictionary<string, object> infoMetadata = structure.Info;
            return CommandResult.Ok(
                message: $"Returning metadata about '{structure.Name}'",
                payload: infoMetadata
                );
        }
    }
}
