using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Runtime
{
    /// <summary>
    /// Class for taking care of the execution of scripts from a script file.
    /// Reads from a text file and parses it (as containing text-based, non-json commands).
    /// Returns a CommandResult with Payload and Assigned when applicable
    /// </summary>
    internal static class ScriptExecutor
    {
        /// <summary>
        /// Reads and executes a script file, working with the provided CommandContext. Creates
        /// its own TextCommandParser for reading and parsing commands. Will provide any payloads and
        /// assignments from respective command to joint Payload and Assignment objects in the final
        /// CommandResult
        /// </summary>
        /// <param name="filePath">The filepath to the script file.</param>
        /// <param name="context">The Command context with the variable memory </param>
        /// <returns>A CommandResult informing how it went, including possible Payload and Assignments objects.</returns>
        public static CommandResult LoadAndExecuteScript(string filePath, CommandContext context)
        {
            var result = FileManager.LoadTextfile(filePath);
            if (!result.Success)
                return CommandResult.FromOperationResult(result);

            var lines = result.Value;

            if (lines == null || lines.Length == 0)
                return CommandResult.Fail("FileEmptyOrNull", $"File '{filePath}' is null or empty.");

            var parser = new TextCommandParser();
            Dictionary<string, object> payloads = [];
            Dictionary<string, string> assigned = [];

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var pkt = parser.Parse(line);
                if (pkt == null)
                    return CommandResult.Fail("InvalidSyntax",$"Invalid command syntax at line {i + 1}.");

                var lineResult = CommandDispatcher.Dispatch(pkt, context);
                if (!lineResult.Success)
                    return CommandResult.Fail("InvalidSyntax", $"Error at line {i + 1}: {lineResult.Message}");
                if (lineResult.Payload != null)
                    payloads[$"[{i + 1}] {pkt.CommandName}"] = lineResult.Payload;
                if (lineResult.Assigned != null)
                    foreach (var kv in lineResult.Assigned)
                        assigned[$"{kv.Key}:"] = kv.Value;
            }
            return CommandResult.Ok($"Script '{filePath}' executed successfully", payload: payloads, assignments: assigned);
        }
    }
}
