using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.CLIUtilities
{
    public sealed class JsonCommandResultRenderer : ICommandResultRenderer
    {
        private static readonly JsonSerializerOptions Options =
            new() { WriteIndented = false };

        public void Render(CommandResult result)
        {
            Console.WriteLine(
                JsonSerializer.Serialize(result, Options)
            );
        }

        public void RenderException(Exception ex)
        {
            var error = CommandResult.Fail(
                "UnhandledException",
                ex.Message
            );

            Console.WriteLine(
                JsonSerializer.Serialize(error, Options)
            );
        }
    }
}
