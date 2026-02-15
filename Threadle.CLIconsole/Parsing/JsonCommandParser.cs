using System.Text.Json;

namespace Threadle.CLIconsole.Parsing
{
    /// <summary>
    /// Provides functionality to parse JSON-formatted command strings into command package objects for further
    /// processing.
    /// </summary>
    /// <remarks>This class is designed to interpret user input in JSON format, extracting command names,
    /// assigned variables, and named arguments in a case-insensitive manner. It implements the ICommandParser interface
    /// to support integration with command-line or scripting environments that utilize structured command
    /// representations.</remarks>
    public sealed class JsonCommandParser : ICommandParser
    {
        #region Fields
        /// <summary>
        /// Provides JSON serializer options configured to ignore case when matching property names during serialization
        /// and deserialization.
        /// </summary>
        /// <remarks>These options ensure that property names are treated case-insensitively, allowing
        /// JSON data with varying property name casing to be correctly processed.</remarks>
        private static readonly JsonSerializerOptions _jsonOptions =
            new() { PropertyNameCaseInsensitive = true };
        #endregion

        #region Methods (public)
        /// <summary>
        /// Parses a JSON-encoded command string and returns a corresponding CommandPackage instance containing the
        /// command name and its arguments.
        /// </summary>
        /// <remarks>If the input is null, empty, or does not contain a valid command, the method returns
        /// null. Any exceptions encountered during parsing are handled internally, and the method will return null in
        /// such cases.</remarks>
        /// <param name="input">The JSON string representation of the command to parse. The string must contain a valid command structure.</param>
        /// <returns>A CommandPackage object representing the parsed command and its arguments, or null if the input is invalid
        /// or cannot be parsed.</returns>
        public CommandPackage? Parse(string input)
        {
            try
            {
                var dto = JsonSerializer.Deserialize<JsonCommandDto>(input, _jsonOptions);
                if (dto == null || string.IsNullOrWhiteSpace(dto.Command))
                    return null;
                CommandPackage package = new CommandPackage
                {
                    AssignedVariable = dto.Assign?.Trim(),
                    CommandName = dto.Command.Trim().ToLowerInvariant(),
                    NamedArgs = dto.Args ?? new Dictionary<string, string>()
                };

                return package;
            }
            catch
            {
                return null;
            }
        }
        #endregion
    }
}
