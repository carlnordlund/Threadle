using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the '[...]' CLI command.
    /// </summary>
    public class Export : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "export(network = [var:network], format = ['gexf'], file = [str], +layername = [str], *nodeattrs = [semicolon-separated attribute names])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Export a network info different external file formats. So far, only 'gexf' (Gephi) is implemented, which still has to be specified with the 'format' argument. For gephi, the specific layer to export is specified with the 'layername' argument, and the node attributes to export are given by a semicolon-separated list of attribute names.";

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
