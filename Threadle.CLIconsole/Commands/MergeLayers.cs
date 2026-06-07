using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'mergelayers' CLI command.
    /// </summary>
    public class MergeLayers : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "mergelayers(network = [var:network], layer1 = [str], layer2 = [str], newlayername = [str], *aggregation = ['sum'(default),'max','min','avg'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Merges two existing layers in a network into a new layer, keeping the original layers intact. Both layers must be of the same type: same mode (1-mode or 2-mode), and for 1-mode layers also the same directionality (directed/undirected) and value type (binary/valued). The merged layer's selfties property is true if either source layer permits selfties. For valued 1-mode layers, values for edges present in both layers are aggregated using 'aggregation': 'sum' (default), 'max', 'min', or 'avg'. The aggregation parameter is ignored for binary and 2-mode layers. For 2-mode layers, hyperedges sharing the same name have their affiliated node sets union-merged. Returns an error if 'newlayername' already exists in the network.";

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
            string layer1Name = command.GetArgumentThrowExceptionIfMissingOrNull("layer1", "arg1");
            string layer2Name = command.GetArgumentThrowExceptionIfMissingOrNull("layer2", "arg2");
            string newLayerName = command.GetArgumentThrowExceptionIfMissingOrNull("newlayername", "arg3");
            string aggregation = command.GetArgumentParseString("aggregation", "sum").ToLower();

            if (aggregation is not ("sum" or "max" or "min" or "avg"))
                return CommandResult.Fail("InvalidParameter", $"Aggregation '{aggregation}' is not supported. Use 'sum', 'max', 'min', or 'avg'.");
            if (layer1Name == layer2Name)
                return CommandResult.Fail("InvalidParameter", "layer1 and layer2 must be different layers.");
            if (network.Layers.ContainsKey(newLayerName))
                return CommandResult.Fail("LayerAlreadyExists", $"A layer named '{newLayerName}' already exists in network '{network.Name}'.");

            var r1 = network.GetLayer(layer1Name);
            if (!r1.Success) return CommandResult.Fail(r1.Code, r1.Message);
            var r2 = network.GetLayer(layer2Name);
            if (!r2.Success) return CommandResult.Fail(r2.Code, r2.Message);

            ILayer layer1 = r1.Value!;
            ILayer layer2 = r2.Value!;

            if ((layer1 is ILayerOneMode) != (layer2 is ILayerOneMode))
                return CommandResult.Fail("IncompatibleLayerTypes", $"Layers '{layer1Name}' and '{layer2Name}' must be of the same mode (both 1-mode or both 2-mode).");

            if (layer1 is ILayerOneMode om1 && layer2 is ILayerOneMode om2)
            {
                if (om1.IsDirectional != om2.IsDirectional)
                    return CommandResult.Fail("IncompatibleLayerTypes", $"Layers '{layer1Name}' and '{layer2Name}' must have the same directionality (both directed or both undirected).");
                if (om1.IsValued != om2.IsValued)
                    return CommandResult.Fail("IncompatibleLayerTypes", $"Layers '{layer1Name}' and '{layer2Name}' must have the same value type (both binary or both valued).");

                var merged = new LayerOneMode(newLayerName, om1.Directionality, om1.EdgeValueType, om1.Selfties || om2.Selfties);

                if (om1.IsValued)
                {
                    var edgeDict = new Dictionary<(uint from, uint to), float>();
                    foreach (var (egoId, alters, values) in om1.GetAllEgoData())
                    {
                        ReadOnlySpan<uint> a = alters.Span;
                        ReadOnlySpan<float> v = values.Span;
                        for (int i = 0; i < a.Length; i++)
                            edgeDict[(egoId, a[i])] = v[i];
                    }
                    foreach (var (egoId, alters, values) in om2.GetAllEgoData())
                    {
                        ReadOnlySpan<uint> a = alters.Span;
                        ReadOnlySpan<float> v = values.Span;
                        for (int i = 0; i < a.Length; i++)
                        {
                            var key = (egoId, a[i]);
                            float v2 = v[i];
                            edgeDict[key] = edgeDict.TryGetValue(key, out float v1)
                                ? aggregation switch
                                {
                                    "max" => Math.Max(v1, v2),
                                    "min" => Math.Min(v1, v2),
                                    "avg" => (v1 + v2) / 2f,
                                    _     => v1 + v2
                                }
                                : v2;
                        }
                    }
                    foreach (var ((from, to), val) in edgeDict)
                        merged._addEdge(from, to, val);
                }
                else
                {
                    var edgeSet = new HashSet<(uint, uint)>();
                    foreach (var (egoId, alters, _) in om1.GetAllEgoData())
                    {
                        ReadOnlySpan<uint> a = alters.Span;
                        for (int i = 0; i < a.Length; i++)
                            edgeSet.Add((egoId, a[i]));
                    }
                    foreach (var (egoId, alters, _) in om2.GetAllEgoData())
                    {
                        ReadOnlySpan<uint> a = alters.Span;
                        for (int i = 0; i < a.Length; i++)
                            edgeSet.Add((egoId, a[i]));
                    }
                    foreach (var (from, to) in edgeSet)
                        merged._addEdge(from, to);
                }

                network.Layers.Add(newLayerName, merged);
                return CommandResult.Ok($"Merged 1-mode layers '{layer1Name}' and '{layer2Name}' into new layer '{newLayerName}' ({merged.NbrEdges} edges).");
            }
            else
            {
                var tm1 = (ILayerTwoMode)layer1;
                var tm2 = (ILayerTwoMode)layer2;
                var merged = new LayerTwoMode(newLayerName);

                var hyperDict = new Dictionary<string, HashSet<uint>>();
                foreach (var (hypername, nodeIds) in tm1.GetAllHyperedgeData())
                    hyperDict[hypername] = new HashSet<uint>(nodeIds);
                foreach (var (hypername, nodeIds) in tm2.GetAllHyperedgeData())
                {
                    if (hyperDict.TryGetValue(hypername, out var existing))
                        foreach (uint nodeId in nodeIds) existing.Add(nodeId);
                    else
                        hyperDict[hypername] = new HashSet<uint>(nodeIds);
                }
                foreach (var (hypername, nodeIds) in hyperDict)
                    merged._addHyperedge(hypername, [.. nodeIds]);

                network.Layers.Add(newLayerName, merged);
                return CommandResult.Ok($"Merged 2-mode layers '{layer1Name}' and '{layer2Name}' into new layer '{newLayerName}' ({merged.NbrHyperedges} hyperedges).");
            }
        }
    }
}
