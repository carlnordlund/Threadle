using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Parsing
{
    public sealed class JsonCommandParser : ICommandParser
    {
        private static readonly JsonSerializerOptions _jsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true
            };

        public CommandPackage? Parse(string input)
        {
            try
            {
                var dto = JsonSerializer.Deserialize<JsonCommandDto>(input, _jsonOptions);
                if (dto == null || string.IsNullOrWhiteSpace(dto.Command))
                    return null;
                CommandPackage package = new CommandPackage
                {
                    AssignedVariable = dto.Assign?.Trim().ToLowerInvariant(),
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
    }
}
