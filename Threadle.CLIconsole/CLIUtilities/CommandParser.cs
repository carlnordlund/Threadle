using Threadle.CLIconsole.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.CLIUtilities
{
    public static class CommandParser
    {
        private static readonly Regex commandRegex = new(@"^(?:([a-zA-Z]\w*)\s*=)?\s*([a-zA-Z]\w*)(?:\s*\(\s*(.*?)\s*\))?\s*$", RegexOptions.Compiled);
        //"^(?:([a-zA-Z]\w*)\s*=)?\s*([a-zA-Z]\w*)\s*\(\s*(.*?)\s*\)\s*$"
        //@"(?:([a-zA-Z]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^,\s]+)))|(?:""([^""]*)""|'([^']*)'|([^=,\s]+))"
        //@"(?:([a-zA-Z]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^,\s]+)))|([^=,\s]+)"

        private static readonly Regex argRegex = new(@"(?:([a-zA-Z]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^,\s]+)))|(?:""([^""]*)""|'([^']*)'|([^=,\s]+))", RegexOptions.Compiled);

        public static Command? Parse(string input)
        {
            var match = commandRegex.Match(input);
            if (!match.Success)
                return null;

            string? assignedVar = match.Groups[1].Value;
            string cmdName = match.Groups[2].Value;
            string argString = match.Groups[3].Value;

            var cmd = new Command();
            cmd.CommandName = cmdName;
            if (!string.IsNullOrWhiteSpace(assignedVar))
                cmd.AssignedVariable = assignedVar;
            var argMatches = argRegex.Matches(argString);
            foreach (Match argMatch in argMatches)
            {
                if (argMatch.Groups[1].Success) // Named argument
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
                //else if (argMatch.Groups[5].Success)
                //{
                //    string key = $"arg{cmd.NamedArgs.Count}";
                //    cmd.NamedArgs[key] = argMatch.Groups[5].Value.Trim();
                //}
            }
            return cmd;
        }
    }
}
