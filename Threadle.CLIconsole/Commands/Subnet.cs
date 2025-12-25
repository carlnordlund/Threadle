using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    public class Subnet : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[var:network] = subnet(network = [var:network2], nodeset = [var:nodeset])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates and stores a new network [var:network] that based on the provided network [var:network2] but only including the nodes in the provided nodeset [var:nodeset]. For instance, if a subset of a Nodeset has first been created using 'filter()', one can then use this command to create a subset of a network that is using the original Nodeset.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => true;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="Command"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(Command command, CommandContext context)
        {
            string variableName = command.GetAssignmentVariableNameThrowExceptionIfNull();
            //string networkName = command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0");
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResultNetwork)
                return commandResultNetwork;

            //Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));

            string nodesetName = command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg1");
            if (CommandHelpers.TryGetVariable<Nodeset>(context, nodesetName, out var nodeset) is CommandResult commandResultNodeset)
                return commandResultNodeset;

            //Nodeset nodeset = context.GetVariableThrowExceptionIfMissing<Nodeset>(command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg1"));

            OperationResult<Network> result = NetworkProcessor.Subnet(network, nodeset);
            if (!result.Success)
                return CommandResult.Fail(result.Code, result.Message);
            var subnet = result.Value!;
            subnet.Name = context.GetNextIncrementalName(network.Name + "_subnet");
            context.SetVariable(variableName, result.Value!);
            return CommandResult.Ok(
                message: result.Message,
                assignments: CommandResult.Assigning(variableName, typeof(Network))
                );
        }
    }
}
