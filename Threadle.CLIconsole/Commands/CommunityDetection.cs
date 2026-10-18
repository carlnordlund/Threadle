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
        public string Syntax => "communitydetection(network=[var:network], *layernames=[semicolon-separated], *attrname=[str], *type=['louvain'(default),'leiden','lp','lpam','gn'], +resolution=[double], +randomness=[double], +maxiterations=[int], *numrestarts=[int(default:1)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Detects communities for the specified layer(s), storing the community index as an integer node attribute. Directed layers are symmetrized (arcs in both directions accumulate into an undirected weight) since community detection is inherently undirected; multiple layers combine via summed edge weight. 'louvain', 'leiden', 'lp' and 'lpam' also accept a single 2-mode layer on its own (not combined with other layers), pseudo-projected: traversed node-hyperedge-node directly, without materializing a 1-mode projection. Tie strength on a 2-mode layer is always count-weighted (matching eigenvectorcentrality()'s convention, same as getedge()'s default projection) — there is no binary option, since deduplicating a co-affiliate pair across hyperedges would need the same global pairwise structure this feature exists to avoid. For 'leiden' on a 2-mode layer, only the very first (hyperedge-direct) level skips the usual connectivity-guaranteeing refinement step; refinement applies normally from the first aggregation onward. 'gn' has no 2-mode support (edge betweenness has no hyperedge analogue) — project a 2-mode layer first with projecttwomodetoonemode() to use it there. Five methods are available via 'type': 'louvain' (default) — modularity optimization; resolution (default 1.0) scales the null-model term, values above 1 favor more/smaller communities, values below 1 favor fewer/larger ones. 'leiden' — like 'louvain' but with a randomized refinement step before each aggregation that guarantees every resulting community is internally connected, fixing a known Louvain flaw; also takes resolution, plus randomness (default 0.01) controlling how much refinement deviates from a purely greedy choice (near 0 is effectively greedy, larger values explore more). 'lp' — plain (Raghavan-Albert-Kumara) label propagation: each node repeatedly adopts the most common label among its neighbors; maxiterations (default 100) caps the number of passes, since unlike Louvain/Leiden this method has no guaranteed convergence — prone to collapsing well-connected networks into one giant community. 'lpam' — modularity-constrained label propagation (Barber & Clark): same neighbor-label mechanism as 'lp', but a node only adopts a new label if doing so increases modularity, which avoids the monster-community collapse; runs a single level (no aggregation), so it typically finds more, smaller communities than Louvain/Leiden. 'gn' — Girvan-Newman: divisive rather than agglomerative, repeatedly removing the currently-highest-edge-betweenness edge and keeping whichever resulting partition scores highest on modularity (against the original graph); also takes resolution. Deterministic, and substantially more expensive than the other methods (a full edge-betweenness recomputation after every single edge removal, O(V*E^2) in the worst case) since it does not stop early, so best suited to smaller networks; numrestarts does not apply (it is always reported as 1). Arguments marked with (+) only apply to the corresponding type and are ignored otherwise. 'numrestarts' (default 1, applies to every type except 'gn') runs that many independent restarts — each with its own random node order/tie-breaks/refinement draws — in parallel across up to 'maxthreads' threads (see 'setting()') and keeps the highest-modularity result; a way to escape the local optima a single run can get stuck in (for 'lp', modularity is a secondary selection criterion since that method doesn't target it). Exact reproducibility via 'randomseed()' across restarts is only guaranteed when maxthreads is set to 1. Returns NbrCommunities, CommunitySizes (descending), the achieved Modularity score (for 'lp' this is reported, not optimized), and NumRestarts. References: Blondel, Guillaume, Lambiotte & Lefebvre (2008) doi:10.1088/1742-5468/2008/10/P10008; Traag, Waltman & van Eck (2019) doi:10.1038/s41598-019-41695-z; Raghavan, Albert & Kumara (2007) doi:10.1103/PhysRevE.76.036106; Barber & Clark (2009) doi:10.1103/PhysRevE.80.026129; Girvan & Newman (2002) doi:10.1073/pnas.122653799.";

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
                case "gn":
                    double gnResolution = command.GetArgumentParseDouble("resolution", 1.0);
                    var gnResult = Analyses.CommunityDetectionGirvanNewman(network, layers, attr, gnResolution);
                    return CommandResult.FromOperationResult(gnResult, gnResult.Value);
                default:
                    return CommandResult.Fail("CommunityMethodNotFound", $"Community detection method '{type}' not recognized.");
            }
        }
    }
}