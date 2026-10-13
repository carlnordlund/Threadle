using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'setattrs' CLI command.
    /// </summary>
    public class SetAttrs : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "setattrs(structure = [var:structure], *nodes = [semicolon-separated uints], attrname = [str], attrvalue = [str] | attrvalues = [semicolon-separated strs])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Sets the value of the attribute 'attrname' for the nodes in 'nodes' (separated by semicolons) in the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. Either sets all of them to one common value with 'attrvalue', or sets one value per node with 'attrvalues' (separated by semicolons, same length and order as 'nodes'). Exactly one of 'attrvalue' and 'attrvalues' must be provided. If 'nodes' is omitted, 'attrvalue' is set for all nodes in the nodeset (e.g. to reset an attribute between simulation runs); 'attrvalues' always requires 'nodes'. If a node appears more than once in 'nodes', its last value is used. Values in 'attrvalues' cannot contain semicolons: use setattr() for such values. As with setattr(), the attribute must first have been defined, and the values must be in accordance with its data type. All or nothing: all nodes and values are checked before anything is set, so any error leaves all attribute values unchanged. Returns a message with the number of nodes updated.";

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
            if (CommandHelpers.TryGetNodesetFromIStructure(context, command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"), out var nodeset) is CommandResult commandResult)
                return commandResult;
            uint[]? nodeIds = null;
            if (command.GetArgument("nodes") is string nodesString && (nodeIds = Misc.SplitStringToUintArray(nodesString)) == null)
                return CommandResult.Fail("ParseError", "Could not parse semicolon-separated list with nodes.");
            string attrName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname");
            string? attrValue = command.GetArgument("attrvalue");
            string[]? attrValues = command.GetArgument("attrvalues")?.Split(';');
            return CommandResult.FromOperationResult(nodeset!.SetNodeAttributes(nodeIds, attrName, attrValue, attrValues));
        }
    }
}
