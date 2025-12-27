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
    internal static class ScriptExecutor
    {
        public static CommandResult LoadAndExecuteScript(string filePath, CommandContext context)
        {
            var result = FileManager.LoadTextfile(filePath);
            if (!result.Success)
                return CommandResult.FromOperationResult(result);

            var lines = result.Value;

            if (lines == null || lines.Length == 0)
                return CommandResult.Fail("FileEmptyOrNull", $"File '{filePath}' is null or empty.");

            var parser = new TextCommandParser();

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


            }
            return CommandResult.Ok($"Script '{filePath}' executed successfully");
        }
    }
}
