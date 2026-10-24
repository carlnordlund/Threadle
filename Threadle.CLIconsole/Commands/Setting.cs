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
        public string Description => "Changes the setting 'name' to 'value'. Boolean settings are 'nodecache' (use node cache, lazy initialized), 'blockmultiedges' (prohibits the creation of multiple edges with identical connections and directions), 'verbose', and 'executiontime' (append each command's execution time, in milliseconds, as a sentence in its response message, e.g. 'Execution time: 12.34 ms.'; off by default). The integer setting 'maxthreads' caps the number of threads parallelized methods (e.g. betweennesscentrality) may use concurrently; -1 is unconstrained, i.e. .NET decides based on available cores. It cannot exceed the number of available processor cores, nor the limit set by 'maxthreadslimit' in the optional appconfig.json file next to the Threadle executable (where -1 then means that limit). Its value at startup is -1, unless set by 'defaultmaxthreads' in appconfig.json (see 'systeminfo()').";

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
            if (param == "executiontime")
            {
                if (!bool.TryParse(valueString, out bool executionTimeValue))
                    return CommandResult.Fail("InvalidValue", $"Setting 'executiontime' requires a boolean value ('true'/'false'), got '{valueString}'.");
                CLISettings.ExecutionTime = executionTimeValue;
                return CommandResult.Ok($"Setting 'executiontime' set to {executionTimeValue}.");
            }
            // Delegate to shared settings manager
            OperationResult result = UserSettings.Set(param, valueString);
            return CommandResult.FromOperationResult(
                result,
                // 'maxthreads' may be adjusted (-1 becomes the ceiling from appconfig.json), so report the value actually set.
                payload: new { Setting = param, Value = (param == "maxthreads" && result.Success) ? UserSettings.MaxDegreeOfParallelism.ToString() : valueString }
            );
        }
    }
}