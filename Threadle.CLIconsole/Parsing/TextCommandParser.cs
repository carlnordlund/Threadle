using System.Text.RegularExpressions;

namespace Threadle.CLIconsole.Parsing
{
    /// <summary>
    /// Provides functionality to parse text-based command input into structured command packages, extracting command
    /// names, optional variable assignments, and key-value arguments.
    /// </summary>
    /// <remarks>This parser is designed for command-line style input where commands may optionally assign
    /// their result to a variable and may include arguments in parentheses. The parser returns a CommandPackage
    /// representing the parsed command, or null if the input does not conform to the expected format. This class is not
    /// thread-safe.</remarks>
    public sealed class TextCommandParser : ICommandParser
    {
        #region Fields
        /// <summary>
        /// Pre-compiled regular expression for parsing a command, its string of arguments, and potential variable assignment.
        /// </summary>
        private static readonly Regex commandRegex = new(@"^(?:([a-zA-Z]\w*)\s*=)?\s*([a-zA-Z]\w*)(?:\s*\(\s*(.*?)\s*\))?\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Pre-compiled regular expression for parsing a string of arguments into separate key-value argument pairs.
        /// </summary>
        private static readonly Regex argRegex = new(@"(?:([a-zA-Z0-9]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^,\s]+)))|(?:""([^""]*)""|'([^']*)'|([^=,\s]+))", RegexOptions.Compiled);
        #endregion

        #region Methods (public)
        /// <summary>
        /// Parses a command-line input string and returns a corresponding CommandPackage object containing the command
        /// name, assigned variable, and named arguments.
        /// </summary>
        /// <remarks>The input string must conform to the expected command-line format for successful
        /// parsing. The method extracts the command name, an optional assigned variable, and any named or positional
        /// arguments from the input. If the input cannot be parsed, the method returns null.</remarks>
        /// <param name="input">The input string representing a command and its arguments to be parsed.</param>
        /// <returns>A CommandPackage object that represents the parsed command and its arguments, or null if the input does not
        /// match the expected command format.</returns>
        public CommandPackage? Parse(string input)
        {
            // existing CLI parsing logic
            var match = commandRegex.Match(input);
            if (!match.Success)
                return null;
            string? assignedVar = match.Groups[1].Value;
            string cmdName = match.Groups[2].Value.ToLowerInvariant();
            string argString = match.Groups[3].Value;

            var cmd = new CommandPackage();
            cmd.CommandName = cmdName;
            if (!string.IsNullOrWhiteSpace(assignedVar))
                cmd.AssignedVariable = assignedVar;
            var argMatches = argRegex.Matches(argString);
            foreach (Match argMatch in argMatches)
            {
                if (argMatch.Groups[1].Success)
                {
                    string key = argMatch.Groups[1].Value.Trim().ToLowerInvariant();
                    string val = argMatch.Groups[2].Success ? argMatch.Groups[2].Value :
                        argMatch.Groups[3].Success ? argMatch.Groups[3].Value :
                        argMatch.Groups[4].Value;
                    cmd.NamedArgs[key] = val.Trim();
                }
                else
                {
                    string val = argMatch.Groups[5].Success ? argMatch.Groups[5].Value :
                        argMatch.Groups[6].Success ? argMatch.Groups[6].Value :
                        argMatch.Groups[7].Value;
                    string key = $"arg{cmd.NamedArgs.Count}";
                    cmd.NamedArgs[key] = val.Trim();
                }
            }
            return cmd;
        }
        #endregion
    }
}
