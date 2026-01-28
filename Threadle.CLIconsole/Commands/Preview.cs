using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'view' CLI command.
    /// </summary>
    public class Preview : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[str] = preview(structure = [var:structure])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Previews the content of the structure with the variable name [var:structure]. Caps the number of outputted lines: max 10 nodes (with attributes) for nodesets, max 10 edges per 1-mode layer, and max 10 node-hyperedge affiliations per 2-mode layer.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            string structureName = command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0");
            if (CommandHelpers.TryGetVariable<IStructure>(context, structureName, out var structure) is CommandResult commandResult)
                return commandResult;
            return CommandResult.Ok(
                message: $"Preview of {nameof(IStructure)} '{structure.Name}'",
                payload: structure.Preview
                );
        }
    }
}
