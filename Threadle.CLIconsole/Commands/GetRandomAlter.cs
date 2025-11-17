using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
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
        public string Description => "Get the node id of a random alter to the specified node. By default, the pick is randomly picked among all available layers, or the specified layer. By default, both in- and outbound ties are considered, but this can be adjusted.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="Command"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console varioable memory.</param>
        public void Execute(Command command, CommandContext context)
        {
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            uint nodeid = command.GetArgumentParseUintThrowExceptionIfMissingOrNull("nodeid", "arg1");
            string layerName = command.GetArgumentParseString("layername", "");
            EdgeTraversal edgeTraversal = command.GetArgumentParseEnum<EdgeTraversal>("direction", EdgeTraversal.Both);
            bool balanced = command.GetArgumentParseBool("balanced", false);
            OperationResult<uint> result = Analyses.GetRandomAlter(network, nodeid, layerName, edgeTraversal, balanced);
            if (result.Success)
                ConsoleOutput.WriteLine(result.Value.ToString(), true);
            else
                ConsoleOutput.WriteLine(result.ToString());
        }
    }
}
