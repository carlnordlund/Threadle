namespace Threadle.CLIconsole.Parsing
{
    /// <summary>
    /// Defines a contract for parsing command input into a structured CommandPackage.
    /// </summary>
    /// <remarks>Implementations of this interface should handle the parsing logic specific to the command
    /// format being used. The Parse method may return null if the input cannot be parsed into a valid
    /// CommandPackage.</remarks>
    public interface ICommandParser
    {
        /// <summary>
        /// Parses the specified input string and returns a corresponding CommandPackage instance if the input is valid.
        /// </summary>
        /// <remarks>The input string must conform to the expected syntax for command packages. If the
        /// input does not match the required format, this method returns null instead of throwing an
        /// exception.</remarks>
        /// <param name="input">The input string to parse. The string must be in a recognized format that can be converted to a
        /// CommandPackage.</param>
        /// <returns>A CommandPackage object that represents the parsed input, or null if the input is invalid or cannot be
        /// parsed.</returns>
        CommandPackage? Parse(string input);
    }
}
