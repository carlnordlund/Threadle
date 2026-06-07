using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'convertlayer' CLI command.
    /// </summary>
    public class ConvertLayer : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "convertlayer(network = [var:network], layername = [str], *valuetype = ['keep'(default),'valued'], *directionality = ['keep'(default),'directed'], *selfties = ['keep'(default),'true','false'], *newlayername = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates a converted copy of an existing 1-mode layer with modified structural properties, leaving the original layer intact. 'valuetype=valued' promotes a binary layer to valued (all edge weights become 1.0); use dichotomize() for the reverse. 'directionality=directed' promotes an undirected layer to directed (each undirected edge becomes two directed edges with the same weight); use symmetrize() for the reverse. 'selfties' sets whether self-ties are permitted in the new layer ('keep' preserves the source layer's setting). At least one of valuetype, directionality, or selfties must differ from 'keep'. The new layer is named using 'newlayername', defaulting to '<layername>-converted'.";

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
            string valueTypeArg      = command.GetArgumentParseString("valuetype",      "keep").ToLower();
            string directionalityArg = command.GetArgumentParseString("directionality", "keep").ToLower();
            string selftiesArg       = command.GetArgumentParseString("selfties",       "keep").ToLower();

            if (valueTypeArg is not ("keep" or "valued"))
                return CommandResult.Fail("InvalidParameter", $"valuetype '{valueTypeArg}' is not valid. Use 'keep' or 'valued' (use dichotomize() to convert valued→binary).");
            if (directionalityArg is not ("keep" or "directed"))
                return CommandResult.Fail("InvalidParameter", $"directionality '{directionalityArg}' is not valid. Use 'keep' or 'directed' (use symmetrize() to convert directed→undirected).");
            if (selftiesArg is not ("keep" or "true" or "false"))
                return CommandResult.Fail("InvalidParameter", $"selfties '{selftiesArg}' is not valid. Use 'keep', 'true', or 'false'.");

            var layerResult = network.GetLayer(layerName);
            if (!layerResult.Success)
                return CommandResult.Fail(layerResult.Code, layerResult.Message);

            ILayer layer = layerResult.Value!;
            if (layer is not ILayerOneMode om)
                return CommandResult.Fail("InvalidLayerType", $"Layer '{layerName}' is not a 1-mode layer. convertlayer() only applies to 1-mode layers.");

            if (valueTypeArg == "valued" && om.IsValued)
                return CommandResult.Fail("NoConversionNeeded", $"Layer '{layerName}' is already valued. Use dichotomize() to convert to binary.");
            if (directionalityArg == "directed" && om.IsDirectional)
                return CommandResult.Fail("NoConversionNeeded", $"Layer '{layerName}' is already directed. Use symmetrize() to convert to undirected.");

            bool selftiesKeep = selftiesArg == "keep";
            if (valueTypeArg == "keep" && directionalityArg == "keep" && selftiesKeep)
                return CommandResult.Fail("InvalidParameter", "At least one of 'valuetype', 'directionality', or 'selfties' must differ from 'keep'.");

            bool toValued   = valueTypeArg == "valued";
            bool toDirected = directionalityArg == "directed";
            bool newSelfties = selftiesArg switch
            {
                "true"  => true,
                "false" => false,
                _       => om.Selfties
            };

            EdgeType           newEdgeType       = toValued   ? EdgeType.Valued            : om.EdgeValueType;
            EdgeDirectionality newDirectionality = toDirected ? EdgeDirectionality.Directed : om.Directionality;
            string newLayerName = network.GetNextAvailableLayerName(
                command.GetArgumentParseString("newlayername", layerName + "-converted"));

            var converted = new LayerOneMode(newLayerName, newDirectionality, newEdgeType, newSelfties);

            foreach (var (egoId, alters, values) in om.GetAllEgoData())
            {
                ReadOnlySpan<uint>  a        = alters.Span;
                ReadOnlySpan<float> v        = values.Span;
                bool                hasValues = !values.IsEmpty;
                for (int i = 0; i < a.Length; i++)
                {
                    float val = hasValues ? v[i] : 1f;
                    converted._addEdge(egoId, a[i], val);
                    if (toDirected && egoId != a[i])    // add reverse edge; self-edges need no doubling
                        converted._addEdge(a[i], egoId, val);
                }
            }

            network.Layers.Add(newLayerName, converted);
            return CommandResult.Ok($"Converted layer '{layerName}' into new layer '{newLayerName}' ({converted.NbrEdges} edges).");
        }
    }
}
