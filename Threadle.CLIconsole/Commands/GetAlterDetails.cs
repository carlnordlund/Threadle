using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing.Enums;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'getalterdetails' CLI command.
    /// </summary>
    public class GetAlterDetails : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[table] = getalterdetails(network = [var:network], nodes = [semicolon-separated uints], *layernames = [semicolon-separated layer names], *direction = ['both','in','out'(default)], *attrname = [str], *cond = ['eq','ne','gt','lt','ge','le','isnull','notnull'], *attrvalue = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Gets detailed information about the alters of all the ego nodes in the semicolon-separated list 'nodes', in the Network with the variable name [var:network]. Alters are collected from the specified layers (separated by semicolons in 'layernames') or from all layers. The result is a column-oriented table, i.e. a dictionary where each of the keys 'ego', 'alter', 'layer', 'hyperedge' and 'hypersize' holds an array with one value per row. There is one row per ego, alter, layer and shared affiliation: alters are thus not deduplicated, and an alter appears once for each ego, layer and (in 2-mode layers) hyperedge that it shares with an ego. For 2-mode layers, 'hyperedge' is the name of the shared hyperedge and 'hypersize' is its number of members; for 1-mode layers, 'hyperedge' is null and 'hypersize' is 0. The ego node itself is never included as an alter in 2-mode layers. For directional 1-mode layers, only outbound edges are included by default, but this can be adjusted with the 'direction' argument (for packed (CSR) layers, only outbound can be provided). Optionally, only alters that fulfill the condition 'cond' for the node attribute 'attrname' and the reference value 'attrvalue' are included, with the same semantics as the 'filter' command (when checking for 'isnull' or 'notnull', 'attrvalue' can be ignored).";

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
            string nodesString = command.GetArgumentThrowExceptionIfMissingOrNull("nodes", "arg1");
            if (Misc.SplitStringToUintArray(nodesString) is not uint[] nodeIds || nodeIds.Length == 0)
                return CommandResult.Fail("ParseError", "Could not parse semicolon-separated list with nodes.");
            string layerNames = command.GetArgumentParseString("layernames", "");
            string[]? layers = (layerNames.Length > 0) ? layerNames.Split(';') : null;
            EdgeTraversal edgeTraversal = command.GetArgumentParseEnum<EdgeTraversal>("direction", EdgeTraversal.Out);

            Func<uint, bool>? alterCondition = null;
            string attrName = command.GetArgumentParseString("attrname", "");
            if (attrName.Length > 0)
            {
                ConditionType conditionType = command.GetArgumentParseEnumThrowExceptionIfMissingOrNull<ConditionType>("cond", "cond");
                string attrValue = command.GetArgumentParseString("attrvalue", "");
                OperationResult<Func<uint, bool>> conditionResult = network.Nodeset.CreateNodeAttributeCondition(attrName, conditionType, attrValue);
                if (!conditionResult.Success)
                    return CommandResult.FromOperationResult(conditionResult);
                alterCondition = conditionResult.Value;
            }
            else if (command.GetArgumentParseString("cond", "").Length > 0)
                return CommandResult.Fail("InvalidArguments", "'cond' requires that 'attrname' is provided.");

            OperationResult<Dictionary<string, object>> result = network.GetAlterDetails(nodeIds, layers, edgeTraversal, alterCondition);
            return CommandResult.FromOperationResult(result, result.Success ? new ColumnTable(result.Value!) : null);
        }
    }
}
