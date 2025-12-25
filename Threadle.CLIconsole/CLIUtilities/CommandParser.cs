using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.CLIUtilities
{
    /// <summary>
    /// Static class for parsing CLI input into a <see cref="CommandPackage"/> object for further processing.
    /// </summary>
    public static class CommandParser
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


        #region Methods (internal)
        /// <summary>
        /// Parses a provided CLI string into a Command object - or null if it could not be parsed.
        /// If an argument value is provided with a name, the name is used as the key. If only a value is provided,
        /// the key for this argument is 'argN' where N is the index of the argument in the list of arguments.
        /// </summary>
        /// <param name="input">The CLI string.</param>
        /// <returns>A <see cref="CommandPackage"/> object corresponding to a parsed command, or null if unsuccessfully parsed.</returns>
        internal static CommandPackage? Parse(string input)
        {
            var match = commandRegex.Match(input);
            if (!match.Success)
                return null;
            string? assignedVar = match.Groups[1].Value;
            string cmdName = match.Groups[2].Value;
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
                    string key = argMatch.Groups[1].Value.Trim();
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
