using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'getedge' CLI command.
    /// </summary>
    public class GetEdge : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "[float] = getedge(network = [var:network], layername = [str], node1id = [uint], node2id = [uint])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Returns the edge value between node1id (from) and node2id (to) in the specified layer 'layername', which can be either 1-mode or 2-mode. If the layer is 1-mode directional, node1id is the source and node2id is the destination. If no edge is found, returns zero. For 2-mode layers, the value represents the number of affiliations that the two nodes share in this particular layer, i.e. the value that typically emerge when using the classical matrix-multiplcation-approach for projecting 2-mode data to 1-mode.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="Command"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public CommandResult Execute(Command command, CommandContext context)
        {
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            uint node1id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node1id", "arg2");
            uint node2id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node2id", "arg3");
            OperationResult<float> result = network.GetEdge(layerName, node1id, node2id);
            //if (result.Success)
            //    ConsoleOutput.WriteLine(result.Value.ToString(), true);
            if (!result.Success)
                return CommandResult.Fail(result.Code, result.Message);
            return CommandResult.Ok(result.Message, result.Value);
            //else
            //    ConsoleOutput.WriteLine(result.ToString());
        }
    }
}
