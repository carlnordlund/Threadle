using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'clusteringcoefficient' CLI command.
    /// </summary>
    public class ClusteringCoefficient : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "clusteringcoefficient(network=[var:network], *layernames=[semicolon-separated], *attrname=[str], *method=['auto'(default),'wattsstrogatz','fagiolo','barrat','onnela'], *samplesize=[int])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates the local clustering coefficient for each node in one or more 1-mode layers, storing results as a float node attribute. Formula is auto-selected based on layer type (Watts-Strogatz for binary undirected, Fagiolo for binary directed, Barrat for valued undirected) unless overridden. Accepts multiple 1-mode layers simultaneously (union adjacency). 2-mode layers are not supported — project them first with projecttwomodetoonemode(). Optional samplesize limits computation to a random subset of nodes (same pattern as betweennesscentrality). References: Watts & Strogatz (1998) doi:10.1038/30918; Fagiolo (2007) doi:10.1103/PhysRevE.76.026107; Barrat et al. (2004) doi:10.1073/pnas.0400087101; Onnela et al. (2005) doi:10.1103/PhysRevE.71.065103.";

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
            int sampleSize = command.GetArgumentParseInt("samplesize", 0);
            var methodStr = command.GetArgumentParseString("method", "auto");
            var method = methodStr?.ToLowerInvariant() switch
            {
                "wattsstrogatz" => ClusteringMethod.WattsStrogatz,
                "fagiolo" => ClusteringMethod.Fagiolo,
                "barrat" => ClusteringMethod.Barrat,
                "onnela" => ClusteringMethod.Onnela,
                _ => ClusteringMethod.Auto
            };
            string[]? layers = layerNames.Length > 0 ? layerNames.Split(';') : null;

            return CommandResult.FromOperationResult(Analyses.ClusteringCoefficient(network, layers, attrName, method, sampleSize));
        }
    }
}
