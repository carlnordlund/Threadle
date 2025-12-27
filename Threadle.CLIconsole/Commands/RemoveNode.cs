using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'removenode' CLI command.
    /// </summary>
    public class RemoveNode : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "removenode(structure = [var:structure], nodeid = [uint])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Removes the specified node from the the Nodeset (or the nodeset of the provided Network) that has the variable name [var:structure]. This CLI command will also iterate through all stored Network structures, removing related edges for the networks that use this Nodeset.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            if (CommandHelpers.TryGetNodesetFromIStructure(context, command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"), out var nodeset) is CommandResult commandResult)
                return commandResult;

            //Nodeset nodeset = context.GetNodesetFromIStructure(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));
            uint nodeId = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg1");
            OperationResult result = nodeset!.RemoveNode(nodeId);
            //ConsoleOutput.WriteLines(result.ToString());
            if (!result.Success)
                return CommandResult.Fail(result.Code, result.Message);

            List<string> affectedNetworks = [];
            foreach (Network network in context.GetNetworksUsingNodeset(nodeset))
            {
                network.RemoveNodeEdges(nodeId);
                affectedNetworks.Add(network.Name);
                //ConsoleOutput.WriteLines($"Also removing edges involving node '{nodeId}' from network '{network.Name}'.");
            }

            return CommandResult.Ok(
                message: $"Node {nodeId} removed from nodeset '{nodeset.Name}'.",
                payload: new
                {
                    RemovedNodeId = nodeId,
                    Nodeset = nodeset.Name,
                    AffectedNetworks = affectedNetworks
                }
                );
        }
    }
}
