using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'getattr' CLI command.
    /// </summary>
    public class GetAttr : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[str] = getattr(structure = [var:structure], nodeid = [uint], attrname = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Gets the value of the attribute 'attrname' for node 'nodeid' in the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. Note that the node attribute must first have been defined. Returns an empty string if the node has no value for this attribute.";

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
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg1");
            string attrName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg2");
            OperationResult<NodeAttributeValue> result = nodeset!.GetNodeAttribute(nodeId, attrName);
            if (!result.Success)
                return CommandResult.Fail(result.Code, result.Message);
            return CommandResult.Ok(result.Message, result.Value.GetValue());
        }
    }
}
