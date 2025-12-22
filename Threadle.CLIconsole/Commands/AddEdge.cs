using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'addedge' CLI command.
    /// </summary>
    public class AddEdge : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "addedge(network = [var:network], layername = [str], node1id = [uint], node2id = [uint], *value = [float(default:1)], *addmissingnodes = ['true'(default), 'false'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates and adds an edge between node1id and node2id in the specified layer 'layername' (which must be 1-mode) in network 'network'. If the layer is directional, node1id is the source (from) and node2id is the destination (to). The edge value 'value' is optional, defaulting to 1. If the specified node id's do not exist in the Nodeset, they are by default created and added, but by setting 'addmissingnodes' to 'false' prevents this.";

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
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1").ToLowerInvariant();
            uint node1id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node1id", "arg2");
            uint node2id = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("node2id", "arg3");
            float value = command.GetArgumentParseFloat("value", 1);
            bool addMissingNodes = command.GetArgumentParseBool("addmissingnodes", true);

            var opResult = network.AddEdge(layerName, node1id, node2id, value, addMissingNodes);
            return CommandResult.FromOperationResult(opResult);

            //return opResult.Success
            //    ? CommandResult.Ok(opResult.Message)
            //    : CommandResult.Fail(opResult.Code, opResult.Message);


            //ConsoleOutput.WriteLine(network.AddEdge(layerName, node1id, node2id, value, addMissingNodes).ToString());
        }
    }
}
