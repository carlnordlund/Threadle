using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the '[...]' CLI command.
    /// </summary>
    public class DegreeAssortativity : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "degreeassortativity(network=[var:network], layername=[str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Computes degree assortativity: the Pearson correlation between the degrees of connected node pairs. Positive values indicate that high-degree nodes tend to connect to other high-degree nodes (assortative mixing); negative values indicate hub-to-peripheral connections (disassortative mixing). For directed layers uses out-degree of the source and in-degree of the target; for undirected uses degree of both endpoints. Reference: Newman (2002) doi:10.1103/PhysRevLett.89.208701.";

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
            var result = Analyses.DegreeAssortativity(network, layerName);
            return CommandResult.FromOperationResult(result, result.Value);
        }
    }
}
