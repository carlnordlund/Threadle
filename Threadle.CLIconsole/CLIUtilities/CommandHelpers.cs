using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.CLIUtilities
{
    internal static class CommandHelpers
    {
        public static CommandResult? TryGetVariable<T>(CommandContext context, string name, out T value) where T : class
        {
            if (!context.TryGetVariable<T>(name, out value))
            {
                return CommandResult.Fail(
                    "VariableNotFound",
                    $"No {typeof(T).Name} named '{name}' found."
                );
            }
            return null;
        }
    }
}
