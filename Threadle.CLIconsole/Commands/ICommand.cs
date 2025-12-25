using Threadle.CLIconsole.CLIUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Defines the interface that is shared by all CLI commands.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        string Syntax { get; }

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        bool ToAssign { get; }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        /// <returns>A <see cref="CommandResult"/> object with info on how command execution went.</returns>
        CommandResult Execute(CommandPackage command, CommandContext context);
    }
}
