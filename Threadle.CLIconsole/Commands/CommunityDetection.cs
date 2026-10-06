using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'communitydetection' CLI command.
    /// </summary>
    public class CommunityDetection : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "communitydetection(network=[var:network], *layernames=[semicolon-separated], *attrname=[str], *type=['louvain'(default),'leiden','lp','lpam'], +resolution=[double], +randomness=[double], +maxiterations=[int], *numrestarts=[int(default:1)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Detects communities for the specified 1-mode layer(s), storing the community index as an integer node attribute. Directed layers are symmetrized (arcs in both directions accumulate into an undirected weight) since community detection is inherently undirected; multiple layers combine via summed edge weight. Only 1-mode layers are accepted — project 2-mode layers first with projecttwomodetoonemode(). Four methods are available via 'type': 'louvain' (default) — modularity optimization; resolution (default 1.0) scales the null-model term, values above 1 favor more/smaller communities, values below 1 favor fewer/larger ones. 'leiden' — like 'louvain' but with a randomized refinement step before each aggregation that guarantees every resulting community is internally connected, fixing a known Louvain flaw; also takes resolution, plus randomness (default 0.01) controlling how much refinement deviates from a purely greedy choice (near 0 is effectively greedy, larger values explore more). 'lp' — plain (Raghavan-Albert-Kumara) label propagation: each node repeatedly adopts the most common label among its neighbors; maxiterations (default 100) caps the number of passes, since unlike Louvain/Leiden this method has no guaranteed convergence — prone to collapsing well-connected networks into one giant community. 'lpam' — modularity-constrained label propagation (Barber & Clark): same neighbor-label mechanism as 'lp', but a node only adopts a new label if doing so increases modularity, which avoids the monster-community collapse; runs a single level (no aggregation), so it typically finds more, smaller communities than Louvain/Leiden. Arguments marked with (+) only apply to the corresponding type and are ignored otherwise. 'numrestarts' (default 1, applies to every type) runs that many independent restarts — each with its own random node order/tie-breaks/refinement draws — in parallel across up to 'maxthreads' threads (see 'setting()') and keeps the highest-modularity result; a way to escape the local optima a single run can get stuck in (for 'lp', modularity is a secondary selection criterion since that method doesn't target it). Returns NbrCommunities, CommunitySizes (descending), the achieved Modularity score (for 'lp' this is reported, not optimized), and NumRestarts. References: Blondel, Guillaume, Lambiotte & Lefebvre (2008) doi:10.1088/1742-5468/2008/10/P10008; Traag, Waltman & van Eck (2019) doi:10.1038/s41598-019-41695-z; Raghavan, Albert & Kumara (2007) doi:10.1103/PhysRevE.76.036106; Barber & Clark (2009) doi:10.1103/PhysRevE.80.026129.";

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
            string type = command.GetArgumentParseString("type", "louvain").ToLowerInvariant();
            string[]? layers = layerNames.Length > 0 ? layerNames.Split(';') : null;
            string? attr = attrName.Length > 0 ? attrName : null;
            int numRestarts = command.GetArgumentParseInt("numrestarts", 1);

            switch (type)
            {
                case "louvain":
                    double resolution = command.GetArgumentParseDouble("resolution", 1.0);
                    var louvainResult = Analyses.CommunityDetectionLouvain(network, layers, attr, resolution, numRestarts);
                    return CommandResult.FromOperationResult(louvainResult, louvainResult.Value);
                case "leiden":
                    double leidenResolution = command.GetArgumentParseDouble("resolution", 1.0);
                    double randomness = command.GetArgumentParseDouble("randomness", 0.01);
                    var leidenResult = Analyses.CommunityDetectionLeiden(network, layers, attr, leidenResolution, randomness, numRestarts);
                    return CommandResult.FromOperationResult(leidenResult, leidenResult.Value);
                case "lp":
                    int maxIterations = command.GetArgumentParseInt("maxiterations", 100);
                    var lpaResult = Analyses.CommunityDetectionLabelPropagation(network, layers, attr, maxIterations, numRestarts);
                    return CommandResult.FromOperationResult(lpaResult, lpaResult.Value);
                case "lpam":
                    var lpamResult = Analyses.CommunityDetectionLPAm(network, layers, attr, numRestarts);
                    return CommandResult.FromOperationResult(lpamResult, lpamResult.Value);
                default:
                    return CommandResult.Fail("CommunityMethodNotFound", $"Community detection method '{type}' not recognized.");
            }
        }
    }
}