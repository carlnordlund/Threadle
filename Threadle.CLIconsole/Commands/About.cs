using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'about' CLI command.
    /// </summary>
    public class About : ICommand
    {
        public string Syntax => "about()";

        public string Description => "Displays version information and metadata about the running Threadle instance, including CLI and Core version numbers, author, project website, and license.";

        public bool ToAssign => false;

        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            var cliAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            var cliVersion = cliAssembly.GetName().Version?.ToString(3) ?? "unknown";

            var coreAssembly = typeof(Core.Model.Network).Assembly;
            var coreVersion = coreAssembly.GetName().Version?.ToString(3) ?? "unknown";

            var info = new Dictionary<string, object>
            {
                ["CliVersion"] = cliVersion,
                ["CoreVersion"] = coreVersion,
                ["Author"] = "Carl Nordlund",
                ["Institute"] = "Institute for Analytical Sociology (IAS), Linköping University",
                ["Website"] = "https://threadle.dev",
                ["Funding"] = "Swedish Research Council (Vetenskapsrådet), Grant 2024-01861",
                ["License"] = "MIT"
            };

            return CommandResult.Ok($"Threadle CLI v{cliVersion} / Core v{coreVersion}", info);
        }
    }
}