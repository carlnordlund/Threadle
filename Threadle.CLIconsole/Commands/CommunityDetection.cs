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
        public string Syntax => "communitydetection(network=[var:network], *layernames=[semicolon-separated], *attrname=[str], *type=['louvain'(default),'leiden','lp','lpam','infomap'], +resolution=[double], +randomness=[double], +maxiterations=[int])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Detects communities for the specified layer(s), storing the community index as an integer node attribute. Directed 1-mode layers are symmetrized (arcs in both directions accumulate into an undirected weight) since community detection is inherently undirected; multiple layers combine via summed edge weight. Four of the five methods require 1-mode layers — project 2-mode layers first with projecttwomodetoonemode(); 'infomap' is the exception and accepts 2-mode layers directly (see below). Five methods are available via 'type': 'louvain' (default) — modularity optimization; resolution (default 1.0) scales the null-model term, values above 1 favor more/smaller communities, values below 1 favor fewer/larger ones. 'leiden' — like 'louvain' but with a randomized refinement step before each aggregation that guarantees every resulting community is internally connected, fixing a known Louvain flaw; also takes resolution, plus randomness (default 0.01) controlling how much refinement deviates from a purely greedy choice (near 0 is effectively greedy, larger values explore more). 'lp' — plain (Raghavan-Albert-Kumara) label propagation: each node repeatedly adopts the most common label among its neighbors; maxiterations (default 100) caps the number of passes, since unlike Louvain/Leiden this method has no guaranteed convergence — prone to collapsing well-connected networks into one giant community. 'lpam' — modularity-constrained label propagation (Barber & Clark): same neighbor-label mechanism as 'lp', but a node only adopts a new label if doing so increases modularity, which avoids the monster-community collapse; runs a single level (no aggregation), so it typically finds more, smaller communities than Louvain/Leiden. 'infomap' (Rosvall & Bergstrom) — a fundamentally different, flow-based method: instead of comparing edge density to a null model, it minimizes the expected bits per step to describe a random walker's trajectory (the 'map equation'), i.e. it finds where flow gets trapped rather than where ties are unusually dense; takes neither resolution nor randomness. Unlike the other four types, 'infomap' accepts 2-mode layers directly — a random walk step is well-defined on a hyperedge (pick one weighted by its size, then a member other than the ego) without ever materializing a projected layer, unlike modularity's null model. That said, computing the exact co-membership weight between every pair of a hyperedge's members still costs O(hyperedge size squared) for that hyperedge — the same cost projecting would have, just not stored as a layer — so a 2-mode layer with very large hyperedges (e.g. thousands of people sharing one affiliation) can still be expensive even though nothing gets materialized. Arguments marked with (+) only apply to the corresponding type and are ignored otherwise. Returns NbrCommunities, CommunitySizes (descending), and the achieved Modularity score (for 'lp' and 'infomap' this is reported for comparability, not optimized); 'infomap' additionally returns Codelength in bits, its actual objective — unlike Modularity, lower Codelength is better. References: Blondel, Guillaume, Lambiotte & Lefebvre (2008) doi:10.1088/1742-5468/2008/10/P10008; Traag, Waltman & van Eck (2019) doi:10.1038/s41598-019-41695-z; Raghavan, Albert & Kumara (2007) doi:10.1103/PhysRevE.76.036106; Barber & Clark (2009) doi:10.1103/PhysRevE.80.026129; Rosvall & Bergstrom (2008) doi:10.1073/pnas.0706851105.";

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

            switch (type)
            {
                case "louvain":
                    double resolution = command.GetArgumentParseDouble("resolution", 1.0);
                    var louvainResult = Analyses.CommunityDetectionLouvain(network, layers, attr, resolution);
                    return CommandResult.FromOperationResult(louvainResult, louvainResult.Value);
                case "leiden":
                    double leidenResolution = command.GetArgumentParseDouble("resolution", 1.0);
                    double randomness = command.GetArgumentParseDouble("randomness", 0.01);
                    var leidenResult = Analyses.CommunityDetectionLeiden(network, layers, attr, leidenResolution, randomness);
                    return CommandResult.FromOperationResult(leidenResult, leidenResult.Value);
                case "lp":
                    int maxIterations = command.GetArgumentParseInt("maxiterations", 100);
                    var lpaResult = Analyses.CommunityDetectionLabelPropagation(network, layers, attr, maxIterations);
                    return CommandResult.FromOperationResult(lpaResult, lpaResult.Value);
                case "lpam":
                    var lpamResult = Analyses.CommunityDetectionLPAm(network, layers, attr);
                    return CommandResult.FromOperationResult(lpamResult, lpamResult.Value);
                case "infomap":
                    var infomapResult = Analyses.CommunityDetectionInfomap(network, layers, attr);
                    return CommandResult.FromOperationResult(infomapResult, infomapResult.Value);
                default:
                    return CommandResult.Fail("CommunityMethodNotFound", $"Community detection method '{type}' not recognized.");
            }
        }
    }
}
