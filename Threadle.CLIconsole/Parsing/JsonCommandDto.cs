namespace Threadle.CLIconsole.Parsing
{
    /// <summary>
    /// Represents a data transfer object for serializing a command and its associated arguments in JSON format.
    /// </summary>
    /// <remarks>This class is typically used to encapsulate a command, its optional assignment target, and a
    /// set of named arguments for structured communication between components or services. All properties are optional
    /// and may be null if not specified.</remarks>
    internal sealed class JsonCommandDto
    {
        /// <summary>
        /// Gets or sets the assignment identifier for the current context.
        /// </summary>
        /// <remarks>This property can be null, indicating that no assignment has been made. Ensure to
        /// check for null before using the value.</remarks>
        public string? Assign { get; set; }

        /// <summary>
        /// Gets or sets the command string that specifies the operation to be performed.
        /// </summary>
        /// <remarks>The command string can be null, indicating that no command is currently set. Ensure
        /// that the command is valid for the intended operation before execution.</remarks>
        public string? Command { get; set; }

        /// <summary>
        /// Gets or sets a collection of additional arguments as key-value pairs.
        /// </summary>
        /// <remarks>Use this property to provide optional parameters that customize behavior or supply
        /// extra context. The dictionary may be null if no additional arguments are specified.</remarks>
        public Dictionary<string, string>? Args { get; set; }
    }
}
