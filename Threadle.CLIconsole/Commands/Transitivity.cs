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
    public class Transitivity : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "transitivity(network=[var:network], *layernames=[semicolon-separated])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates the global clustering coefficient (transitivity) of the specified 1-mode layer(s): ratio of closed triangles to all connected triples, treating edges as undirected. Equals 3T / Σ C(k,2) where T is the number of distinct triangles. Returns a single scalar value. Only 1-mode layers are accepted — project 2-mode layers first. Reference: Watts & Strogatz (1998) doi:10.1038/30918..";

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
            string[]? layers = layerNames.Length > 0 ? layerNames.Split(';') : null;
            //var densityResult = Analyses.Density(network, layerName, sampleSize);

            var transitivityResult = Analyses.Transitivity(network, layers);
            return CommandResult.FromOperationResult(transitivityResult, transitivityResult.Value);

            //return CommandResult.FromOperationResult(Analyses.Transitivity(network, layers));
        }
    }
}
