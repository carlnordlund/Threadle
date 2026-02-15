namespace Threadle.Core.Utilities
{
    /// <summary>
    /// Represents the outcome of several methods in Threadle, indicating success or failure.
    /// Provides a consistent way to return success/fail info, messages and potential error
    /// codes instead of using exceptions. If a method can also return a value, use the
    /// <see cref="OperationResult{T}"/> class for generic types.
    /// </summary>
    public class OperationResult
    {
        #region Properties
        /// <summary>
        /// A boolean indicating if the operation was a success (true) or not (false).
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// A status string code.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// A message, typically describing what went wrong.
        /// Useful for displaying at the frontend level.
        /// </summary>
        public string Message { get; }
        #endregion


        #region Constructor (protected)
        /// <summary>
        /// The class-internal protected constructor for creating an OperationResult object.
        /// Used by the public factory methods below.
        /// </summary>
        /// <param name="success">Boolean indicating whether the operation was successful (true) or failed (false).</param>
        /// <param name="code">A short code string for the operation status.</param>
        /// <param name="message">A message describing how the operation went, including what might have gone wrong.</param>
        protected OperationResult(bool success, string code, string message)
        {
            Success = success;
            Code = code;
            Message = message;
        }
        #endregion


        #region Methods (factories)
        /// <summary>
        /// Factory method to create an OperationResult object for successful operations.
        /// </summary>
        /// <param name="message">A message about the successful operation (defaults to empty string).</param>
        /// <returns>An <see cref="OperationResult"/> object indicating success.</returns>
        public static OperationResult Ok(string message = "") => new(true, "OK", message);

        /// <summary>
        /// Factory method to create an OperationResult object for failed operations.
        /// </summary>
        /// <param name="code">A short code string for the type of failure that happened.</param>
        /// <param name="message">A message describing in what way the operation went wrong.</param>
        /// <returns>An <see cref="OperationResult"/> object indicating failure.</returns>
        public static OperationResult Fail(string code, string message) => new(false, code, message);

        /// <summary>
        /// Overrides the ToString() methods, showing the status code and message.
        /// </summary>
        /// <returns>A string with the status code and the message</returns>
        public override string ToString() => $"{Code}: {Message}";
        #endregion
    }
}
