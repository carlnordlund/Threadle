using Threadle.CLIconsole.CLIUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'setting' CLI command.
    /// </summary>
    public class Setting : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "setting(name = [str], value = ['true','false'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Changes the setting 'name' to either 'true' or 'false', i.e. either activating or deactivating it. Available settings are 'nodecache' (use node cache, lazy initialized), 'blockmultiedges' (prohibits the creation of multiple edges with identical connections and directions), 'onlyoutboundedges' (only stores outbound edges, i.e. no inbound edges, all to save memory for walker-only applications).";

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
            string param =
                command.GetArgumentThrowExceptionIfMissingOrNull("name", "arg0")
                       .ToLowerInvariant();

            bool value =
                command.GetArgumentParseBoolThrowExceptionIfMissingOrNull("value", "arg1");

            if (param == "verbose")
            {
                ConsoleOutput.Verbose = value;
                return CommandResult.Ok(
                    $"Setting 'verbose' set to {value}.",
                    payload: new { Setting = "verbose", Value = value }
                );
            }

            if (param == "endmarker")
            {
                ConsoleOutput.EndMarker = value;
                return CommandResult.Ok(
                    $"Setting 'endmarker' set to {value}.",
                    payload: new { Setting = "endmarker", Value = value }
                );
            }

            // Delegate to shared settings manager
            OperationResult result = UserSettings.Set(param, value);
            return CommandResult.FromOperationResult(
                result,
                payload: new { Setting = param, Value = value }
            );
        }
    }
}
