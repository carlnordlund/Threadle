using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.CLIUtilities
{
    public sealed class CommandResult
    {
        /// <summary>
        /// Indicating whether a command was successful or not
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Response code: "Ok" for successful, otherwise an error "code", like "NodeNotFound" etc.
        /// </summary>
        public string Code { get; init; } = "";

        /// <summary>
        /// Human-readable message describing what happened (success as well as fail; takes message from OperationResult)
        /// </summary>
        public string Message { get; init; } = "";

        /// <summary>
        /// Optional payload: what should be displayed (e.g. density, info stuff, attribute values etc)
        /// </summary>
        public object? Payload { get; init; }

        /// <summary>
        /// Details about internal variables that were assigned and their types
        /// </summary>
        public IReadOnlyDictionary<string, string>? Assigned { get; init; }



        /// <summary>
        /// Factory method to create a CommandResult object for successful commands.
        /// </summary>
        /// <param name="message">A message about the successful operation.</param>
        /// <param name="payload">Optional data that is to be displayed (e.g. output from 'getattr()').</param>
        /// <param name="assignments">A dictionary of assigned variables and their data types.</param>
        /// <returns>A <see cref="CommandResult"/> object indicating a successful command.</returns>
        public static CommandResult Ok(string message, object? payload = null, IDictionary<string,string>? assignments = null)
            => new()
            {
                Success = true,
                Code = "OK",
                Message = message,
                Payload = payload,
                Assigned = assignments is null ? null : new Dictionary<string, string>(assignments)

            };

        /// <summary>
        /// Factory method to create a CommandResult object for commands that failed.
        /// </summary>
        /// <param name="code">The error code of the failure (like "NodeNotFound").</param>
        /// <param name="message">A human-readable message describing the failure.</param>
        /// <returns>A <see cref="CommandResult"/> object indicating a failed command.</returns>
        public static CommandResult Fail(string code, string message)
            => new()
            {
                Success = false,
                Code = code,
                Message = message,
            };
    }
}
