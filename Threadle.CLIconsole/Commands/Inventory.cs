using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'i' (inventory) CLI command.
    /// </summary>
    public class Inventory : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[str] = i()";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Provides an inventory of the currently stored data objects. Note that the brackets can be ignored. (This command is in honor of all 1970's text adventure games, where 'i' was used to check what you were carrying).";

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
            if (!context.Variables.Any())
                return CommandResult.Ok("No structures stored in the current session.");
            Dictionary<string, object> inventory = context.VariablesMetadata();
            return CommandResult.Ok(
                message: $"Inventory contains {inventory.Count} structure(s)",
                payload: inventory
                );
        }
    }
}
