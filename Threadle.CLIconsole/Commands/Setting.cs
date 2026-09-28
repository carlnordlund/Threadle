using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'setting' CLI command.
    /// </summary>
    public class Setting : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "setting(name = [str], value = ['true','false'] or [int])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Changes the setting 'name' to 'value'. Boolean settings are 'nodecache' (use node cache, lazy initialized), 'blockmultiedges' (prohibits the creation of multiple edges with identical connections and directions), 'onlyoutboundedges' (only stores outbound edges, i.e. no inbound edges, all to save memory for walker-only applications), and 'verbose'. The integer setting 'maxthreads' caps the number of threads parallelized methods (e.g. betweennesscentrality) may use concurrently; -1 (the default) is unconstrained, i.e. .NET decides based on available cores, and it cannot exceed the number of available processor cores (see 'system()').";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console variable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            string param = command.GetArgumentThrowExceptionIfMissingOrNull("name", "arg0").ToLowerInvariant();
            string valueString = command.GetArgumentThrowExceptionIfMissingOrNull("value", "arg1");
            if (param == "verbose")
            {
                if (!bool.TryParse(valueString, out bool verboseValue))
                    return CommandResult.Fail("InvalidValue", $"Setting 'verbose' requires a boolean value ('true'/'false'), got '{valueString}'.");
                CLISettings.Verbose = verboseValue;
                return CommandResult.Ok($"Setting 'verbose' set to {verboseValue}.");
            }
            // Delegate to shared settings manager
            OperationResult result = UserSettings.Set(param, valueString);
            return CommandResult.FromOperationResult(
                result,
                payload: new { Setting = param, Value = valueString }
            );
        }
    }
}
