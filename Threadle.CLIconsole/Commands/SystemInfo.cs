using System.Runtime.InteropServices;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'system' CLI command.
    /// </summary>
    public class SystemInfo : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "systeminfo()";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Displays details about the system Threadle is running on, including the number of available processor cores, OS and process architecture, .NET runtime version, and the current 'maxthreads' setting. Useful for deciding what value to give the 'maxthreads' setting (see 'setting()').";

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
            var info = new Dictionary<string, object>
            {
                ["ProcessorCount"] = Environment.ProcessorCount,
                ["MaxThreadsSetting"] = UserSettings.MaxDegreeOfParallelism,
                ["OSDescription"] = RuntimeInformation.OSDescription,
                ["OSArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
                ["ProcessArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                ["Is64BitProcess"] = Environment.Is64BitProcess,
                ["FrameworkDescription"] = RuntimeInformation.FrameworkDescription,
                ["ServerGC"] = System.Runtime.GCSettings.IsServerGC
            };

            return CommandResult.Ok(
                message: $"System has {Environment.ProcessorCount} logical processor core(s) available. Current 'maxthreads' setting: {(UserSettings.MaxDegreeOfParallelism == -1 ? "unconstrained" : UserSettings.MaxDegreeOfParallelism.ToString())}.",
                payload: info
                );
        }
    }
}