using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Results
{
    /// <summary>
    /// Represents the outcome of a command execution, including success status, response code, descriptive message,
    /// optional payload data, and details about assigned variables.
    /// </summary>
    /// <remarks>Use this class to encapsulate the result of a command, providing both a high-level success
    /// indicator and additional context such as error codes, messages, and any structured data returned by the command.
    /// The Assigned property can be used to track variables set during command execution, which may assist with
    /// debugging or logging. Instances of this class are typically created using the static Ok or Fail factory
    /// methods.</remarks>
    public sealed class CommandResult
    {
        #region Properties
        /// <summary>
        /// Indicating whether a command was successful or not
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Response code: "Ok" for successful, otherwise an error "code", like "NodeNotFound" etc.
        /// </summary>
        public string Code { get; init; } = "";

        /// <summary>
        /// Human-readable message describing what happened (success or failure)
        /// </summary>
        public string Message { get; init; } = "";

        /// <summary>
        /// Optional structured payload to be rendered (tables, info, statistics etc)
        /// </summary>
        public object? Payload { get; init; }

        /// <summary>
        /// Details about internal variables that were assigned and their type names
        /// </summary>
        public IReadOnlyDictionary<string, string>? Assigned { get; init; }
        #endregion

        #region Methods (public)
        /// <summary>
        /// Creates a new CommandResult based on the outcome of the specified OperationResult, optionally including a
        /// payload and additional assignments.
        /// </summary>
        /// <remarks>If the operation is successful, the returned CommandResult includes the provided
        /// payload and assignments. If the operation fails, the CommandResult contains the error code and message from
        /// the OperationResult, and the payload and assignments are not included.</remarks>
        /// <param name="opResult">The OperationResult that indicates whether the operation succeeded or failed. The success or failure of this
        /// result determines the type of CommandResult returned.</param>
        /// <param name="payload">An optional object containing additional data to include in the CommandResult if the operation was
        /// successful. This parameter is ignored if the operation failed.</param>
        /// <param name="assignments">An optional dictionary of key-value pairs that provide additional context or metadata to include in the
        /// CommandResult if the operation was successful.</param>
        /// <returns>A CommandResult representing the outcome of the operation. Returns a successful result with the specified
        /// payload and assignments if opResult indicates success; otherwise, returns a failure result with the error
        /// code and message from opResult.</returns>
        public static CommandResult FromOperationResult(OperationResult opResult, object? payload = null, IDictionary<string, string>? assignments = null)
        {
            if (opResult.Success)
            {
                return Ok(
                    message: opResult.Message,
                    payload: payload,
                    assignments: assignments
                    );
            }
            return Fail(
                code: opResult.Code,
                message: opResult.Message
                );
        }

        /// <summary>
        /// Factory method to create a CommandResult object for successful commands.
        /// </summary>
        /// <param name="message">A message about the successful operation.</param>
        /// <param name="payload">Optional data that is to be displayed (e.g. output from 'getattr()').</param>
        /// <param name="assignments">A dictionary of assigned variables and their data types.</param>
        /// <returns>A <see cref="CommandResult"/> object indicating a successful command.</returns>
        public static CommandResult Ok(string message, object? payload = null, IDictionary<string, string>? assignments = null)
            => new()
            {
                Success = true,
                Code = "Ok",
                Message = message,
                Payload = payload,
                Assigned = assignments is null ? null : new Dictionary<string, string>(assignments)

            };

        public static CommandResult Ok(string message)
            => Ok(message, null, null);

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

        public static IDictionary<string, string> Assigning(string name, Type type)
            => new Dictionary<string, string> { [name] = type.Name };
        #endregion
    }
}
