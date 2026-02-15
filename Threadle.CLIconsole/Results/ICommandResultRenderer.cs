namespace Threadle.CLIconsole.Results
{
    /// <summary>
    /// Defines methods for rendering the results of command executions and handling exceptions that occur during
    /// command processing.
    /// </summary>
    /// <remarks>Implementations of this interface should provide specific logic for displaying command
    /// results and presenting exception information. This enables flexible output formatting and error reporting
    /// tailored to different application contexts, such as console, GUI, or logging environments.</remarks>
    public interface ICommandResultRenderer
    {
        /// <summary>
        /// Renders the output based on the provided command result.
        /// </summary>
        /// <remarks>This method is responsible for displaying the results of a command execution. It may
        /// throw exceptions if the result is invalid or cannot be processed.</remarks>
        /// <param name="result">The command result to be rendered, which contains the output data and status information.</param>
        void Render(CommandResult result);

        /// <summary>
        /// Handles the specified exception by rendering it for display or logging purposes.
        /// </summary>
        /// <remarks>This method is typically used to present error details to the user or log them for
        /// further analysis. It may format the exception message and stack trace for better readability.</remarks>
        /// <param name="ex">The exception to be rendered, which contains information about the error that occurred.</param>
        void RenderException(Exception ex);
    }
}
