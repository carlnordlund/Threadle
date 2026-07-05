using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'assortativity' CLI command.
    /// </summary>
    public class Assortativity : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "assortativity(network=[var:network], layername=[str], attrname=[str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Computes attribute assortativity (homophily): the tendency of connected nodes to share similar values of a node attribute. For numeric attributes (Int, Float) uses Pearson correlation; for categorical attributes (Char, String, Bool) uses Newman's nominal mixing formula. Returns a value in [-1, 1]: positive = nodes prefer similar others, negative = nodes prefer dissimilar others. Nodes with missing attribute values are skipped. Reference: Newman (2003) doi:10.1103/PhysRevE.67.026126.";

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
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            string attrName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg2");
            var result = Analyses.Assortativity(network, layerName, attrName);
            return CommandResult.FromOperationResult(result, result.Value);
        }
    }
}
