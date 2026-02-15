using System.Text.Json;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Results
{
    /// <summary>
    /// Provides functionality to render command results and exceptions as JSON for console output.
    /// </summary>
    /// <remarks>This class implements the ICommandResultRenderer interface to serialize command results and
    /// exceptions into JSON format. The output includes additional details when CLISettings.Verbose is enabled,
    /// allowing for more comprehensive diagnostic information. This renderer is intended for use in command-line
    /// applications that require structured, machine-readable output.</remarks>
    public sealed class JsonCommandResultRenderer : ICommandResultRenderer
    {
        #region Fields
        private static readonly JsonSerializerOptions Options =
            new() { WriteIndented = false };
        #endregion

        #region Methods (public)
        /// <summary>
        /// Renders the specified command result to the console in JSON format.
        /// </summary>
        /// <remarks>If verbose output is enabled, the entire command result is serialized; otherwise,
        /// only the success status, code, payload, and assigned values are included in the output.</remarks>
        /// <param name="result">The command result to be rendered, containing information about the success status, code, payload, and any
        /// assigned values.</param>
        public void Render(CommandResult result)
        {
            object toSerialize = CLISettings.Verbose
                ? result
                : new
                {
                    result.Success,
                    result.Code,
                    result.Payload,
                    result.Assigned
                };

            ConsoleOutput.WriteLine(JsonSerializer.Serialize(toSerialize, Options), true);
        }

        /// <summary>
        /// Outputs details of an unhandled exception to the console in a JSON-formatted structure for diagnostic
        /// purposes.
        /// </summary>
        /// <remarks>The exception information is serialized as JSON and written to the console to
        /// facilitate easier debugging and error tracking. This method is intended for use in scenarios where
        /// exceptions are not otherwise handled.</remarks>
        /// <param name="ex">The exception to be rendered. Must not be null.</param>
        public void RenderException(Exception ex)
        {
            var error = CommandResult.Fail(
                "UnhandledException",
                ex.Message
            );

            ConsoleOutput.WriteLine(JsonSerializer.Serialize(error, Options), true);

            //Console.WriteLine(
            //    JsonSerializer.Serialize(error, Options)
            //);
        }
        #endregion
    }
}
