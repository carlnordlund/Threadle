using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the '[...]' CLI command.
    /// </summary>
    public class RandomWalkerDistances : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[var:network] = rwdistances(network = [var:network], nodeattr = [str], steps = [uint],*layernames = [semicolon-separated], *walks = [float(default=1.0)], *balanced = ['false'(default),'true'], *weighted = ['false'(default),'true'], *backtrack = ['false'(default),'true'], *stdev = ['false'(default),'true'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Initializes and executes a distance-measuring random walker (experimental).";

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
            return CommandResult.Fail("NotImplemented", "This command is not yet implemented");
        }
    }
}
