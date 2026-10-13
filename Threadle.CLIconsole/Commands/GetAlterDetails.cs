using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
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
        public string Description => "Returns a table with one row per ego, alter, layer and shared affiliation, for all ego nodes in 'nodes' (separated by semicolons) in the Network with the variable name [var:network]. The table has the columns 'ego' (the queried node), 'alter' (the alter node), 'layer' (the layer name), 'hyperedge' (the shared affiliation in a 2-mode layer; null for 1-mode layers) and 'hypersize' (the number of nodes in that affiliation; 0 for 1-mode layers). In JSON output, the payload has one array per column. Alters are taken from the layers in 'layernames' (separated by semicolons), or from all layers if not specified. Nothing is deduplicated: an alter appears once per ego, layer and shared affiliation. In 2-mode layers, ego is excluded from its own alters, and 'direction' does not apply. In 1-mode layers, alters are obtained as in getnodealters(): only outbound edges are included by default, which can be adjusted with 'direction', but packed (CSR) layers only provide outbound edges. Optionally, alters can be restricted to those fulfilling a condition on an attribute, with the same semantics as filter(): 'attrname' is the attribute, 'cond' the condition (required if 'attrname' is set), and 'attrvalue' the reference value (ignored for 'isnull' and 'notnull'). The condition applies to alters only, not to egos.";

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
            if (Misc.SplitStringToUintArray(command.GetArgumentThrowExceptionIfMissingOrNull("nodes", "arg1")) is not uint[] egoIds)
                return CommandResult.Fail("ParseError", "Could not parse semicolon-separated list with nodes.");
            string layerNames = command.GetArgumentParseString("layernames", "");
            string[]? layers = (layerNames.Length > 0) ? layerNames.Split(';') : null;
            EdgeTraversal edgeTraversal = command.GetArgumentParseEnum<EdgeTraversal>("direction", EdgeTraversal.Out);

            // Optional condition on alters, parsed as in filter()
            string? attrName = command.GetArgument("attrname");
            ConditionType? condition = null;
            string attrValue = "";
            if (attrName != null)
            {
                condition = command.GetArgumentParseEnumThrowExceptionIfMissingOrNull<ConditionType>("cond", "cond");
                if (condition != ConditionType.isnull && condition != ConditionType.notnull)
                    attrValue = command.GetArgumentThrowExceptionIfMissingOrNull("attrvalue");
            }

            OperationResult<AlterDetails> result = NetworkProcessor.GetAlterDetails(network, egoIds, layers, edgeTraversal, attrName, condition, attrValue);
            if (!result.Success)
                return CommandResult.FromOperationResult(result);
            AlterDetails table = result.Value!;
            return CommandResult.FromOperationResult(result, new TablePayload()
                .AddColumn("ego", table.Ego)
                .AddColumn("alter", table.Alter)
                .AddColumn("layer", table.Layer)
                .AddColumn("hyperedge", table.Hyperedge)
                .AddColumn("hypersize", table.Hypersize));
        }
    }
}
