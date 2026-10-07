using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'rename' CLI command.
    /// </summary>
    public class Rename : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "rename(structure = [var:structure], type = ['layer','attribute'], from = [str], to = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Renames either a layer or a node attribute, without affecting anything else about it. When type is 'layer', 'structure' must be a Network variable, and 'from'/'to' are the layer's current and new name. When type is 'attribute', 'structure' is a Nodeset (or the Nodeset of the provided Network), and 'from'/'to' are the node attribute's current and new name: the attribute's internal index and all currently set values are unaffected, only the name it is looked up by changes. Renaming to a name already in use, or renaming a layer/attribute that does not exist, fails without changing anything.";

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
            string structureName = command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0");
            string type = command.GetArgumentThrowExceptionIfMissingOrNull("type", "arg1");
            string from = command.GetArgumentThrowExceptionIfMissingOrNull("from", "arg2");
            string to = command.GetArgumentThrowExceptionIfMissingOrNull("to", "arg3");

            switch (type.ToLowerInvariant())
            {
                case "layer":
                    if (CommandHelpers.TryGetVariable<Network>(context, structureName, out var network) is CommandResult networkFail)
                        return networkFail;
                    return CommandResult.FromOperationResult(network.RenameLayer(from, to));
                case "attribute":
                    if (CommandHelpers.TryGetNodesetFromIStructure(context, structureName, out var nodeset) is CommandResult nodesetFail)
                        return nodesetFail;
                    return CommandResult.FromOperationResult(nodeset!.RenameNodeAttribute(from, to));
                default:
                    return CommandResult.Fail("InvalidArgument", $"Unknown rename type '{type}': expected 'layer' or 'attribute'.");
            }
        }
    }
}
