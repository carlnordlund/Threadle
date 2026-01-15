using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Parsing
{
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
    }
}
