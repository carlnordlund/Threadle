using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'lastexecutiontime' CLI command.
    /// </summary>
    public class LastExecutionTime : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "lastexecutiontime()";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Returns the wall-clock execution time (in milliseconds) of the most recently dispatched command, along with that command's name, as both a human-readable message and a structured payload (ExecutionTimeMs, Command). Only measured while the 'executiontime' setting is enabled (see 'setting()'); if it's off, or no command has been dispatched yet while it was on, returns a message saying so instead, with no payload.";

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
            if (CommandDispatcher.LastExecutionTimeMs is not double ms || CommandDispatcher.LastCommand is not string lastCommand)
                return CommandResult.Ok("No execution time has been measured yet - the 'executiontime' setting may be off (see 'setting()').");
            if (!CLISettings.ExecutionTime)
                return CommandResult.Ok("Execution time setting is turned off");

            return CommandResult.Ok(
                $"Last execution time: {ms:F2} ms ({lastCommand}).",
                payload: new { ExecutionTimeMs = ms, Command = lastCommand }
            );
        }
    }
}