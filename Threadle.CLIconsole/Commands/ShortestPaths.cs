using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'shortestpaths' CLI command.
    /// </summary>
    public class ShortestPaths : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[list] = shortestpaths(network = [var:network], pairs = [semicolon-separated 'from:to' pairs], *layernames = [semicolon-separated], *returnpaths = ['false'(default),'true'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates exact shortest paths (bidirectional BFS) for a batch of node pairs, e.g. pairs='1:2;3:4;5:6' (colon between the two node ids of a pair, semicolon between pairs — not commas, which are the CLI's own argument separator). Runs in parallel across up to 'maxthreads' threads (see 'setting()' and 'system()'). Uses all layers unless specific layers are specified as a semicolon-separated list. Returns a list of {node1id, node2id, distance, [path]} for each pair, in the same order as given. Distance is -1 if no path exists. Set returnpaths=true to also return the sequence of node ids along each shortest path. For aggregating shortest paths by node attribute category instead, see 'shortestpathsattributes()'.";

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

            string pairsString = command.GetArgumentThrowExceptionIfMissingOrNull("pairs", "arg1");
            List<(uint, uint)>? pairs = Misc.ParseUintPairList(pairsString);
            if (pairs == null)
                return CommandResult.Fail("InvalidArgument", "Argument 'pairs' is malformed; expected semicolon-separated 'from:to' pairs, e.g. '1:2;3:4'.");

            string layerNames = command.GetArgumentParseString("layernames", "");
            string[]? layers = (layerNames.Length > 0) ? layerNames.Split(';') : null;
            bool returnPaths = command.GetArgumentParseBool("returnpaths", false);

            var result = Analyses.ShortestPaths(network, layers, pairs, returnPaths);
            if (!result.Success)
                return CommandResult.FromOperationResult(result);

            var payload = new List<Dictionary<string, object>>(result.Value!.Count);
            foreach (var (from, to, sp) in result.Value!)
            {
                var entry = new Dictionary<string, object>
                {
                    ["node1id"] = from,
                    ["node2id"] = to,
                    ["distance"] = sp.Distance
                };
                if (returnPaths && sp.Path != null)
                    entry["path"] = string.Join(";", sp.Path);
                payload.Add(entry);
            }

            return CommandResult.Ok($"Computed shortest paths for {pairs.Count} node pair(s).", payload);
        }
    }
}