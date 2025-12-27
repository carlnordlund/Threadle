using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'getrandomalter' CLI command.
    /// </summary>
    public class GetRandomAlter : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[uint] = getrandomalter(network = [var:network], nodeid = [uint], *layername = [str], *direction = ['both'(default),'in','out'], *balanced = ['true','false'(default)])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Get the node id of a random alter to the specified node. By default, both in- and outbound ties are considered, but this can be adjusted. By default, the pick is randomly picked among a specific layer as given by the 'layername' argument, or all available layers can be used by omitting this argument. If all layers are included, the 'balanced' argument specifies how the pick should be done. If balanced is set to 'true', a uniformly random pick between layer takes place first, followed by a random pick of an alter in the specific layer that was picked. If set to 'false', alters in all layers are first pooled together (with the possibility of an alter appearing multiple times) and a random pick is then done among this complete set of alters across layers.";

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
            //string networkName = command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0");
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;

            //Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            uint nodeid = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg1");
            string layerName = command.GetArgumentParseString("layername", "");
            EdgeTraversal edgeTraversal = command.GetArgumentParseEnum<EdgeTraversal>("direction", EdgeTraversal.Both);
            bool balanced = command.GetArgumentParseBool("balanced", false);
            OperationResult<uint> result = Analyses.GetRandomAlter(network, nodeid, layerName, edgeTraversal, balanced);
            return CommandResult.FromOperationResult(
                opResult: result,
                payload: result.Value
                );
            //if (!result.Success)
            //    return CommandResult.Fail(result.Code,result.Message);
            //return CommandResult.Ok(result.Message, result.Value);
        }
    }
}
