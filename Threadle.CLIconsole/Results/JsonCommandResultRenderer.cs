using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Results
{
    public sealed class JsonCommandResultRenderer : ICommandResultRenderer
    {
        private static readonly JsonSerializerOptions Options =
            new() { WriteIndented = false };

        public void Render(CommandResult result)
        {
            object toSerialize = CLISettings.Verbose
                ? result
                : new
                {
                    result.Success,
                    result.Code,
                    result.Payload,
                    result.Assigned
                };

            ConsoleOutput.WriteLine(JsonSerializer.Serialize(toSerialize, Options), true);
        }

        public void RenderException(Exception ex)
        {
            var error = CommandResult.Fail(
                "UnhandledException",
                ex.Message
            );

            ConsoleOutput.WriteLine(JsonSerializer.Serialize(error, Options), true);

            //Console.WriteLine(
            //    JsonSerializer.Serialize(error, Options)
            //);
        }
    }
}
