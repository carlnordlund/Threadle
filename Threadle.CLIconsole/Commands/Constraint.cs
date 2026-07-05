using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'constraint' CLI command.
    /// </summary>
    public class Constraint : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "constraint(network=[var:network], *layernames=[semicolon-separated], *attrname=[str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Computes Burt's constraint for each node in the specified 1-mode layer(s) and stores the result as a float node attribute. Constraint C(i) = Σ_j (p_ij + Σ_q p_iq p_qj)² where p_ij is i's proportion of interaction with j. Ranges from ~0 (broker, many structural holes) to ~1 (fully embedded in dense clique). Weights are symmetrized across layers. Only 1-mode layers are accepted — project 2-mode layers first. Reference: Burt (1992) Structural Holes; Burt (2004) doi:10.1086/421787.";

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
            string layerNames = command.GetArgumentParseString("layernames", "");
            string? attrName = command.GetArgumentParseString("attrname", "");
            string[]? layers = layerNames.Length > 0 ? layerNames.Split(';') : null;
            return CommandResult.FromOperationResult(Analyses.Constraint(network!, layers, attrName));
        }
    }
}