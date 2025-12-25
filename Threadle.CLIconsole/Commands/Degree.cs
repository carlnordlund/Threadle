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
    /// Class representing the 'degree' CLI command.
    /// </summary>
    public class Degree : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "degree(network = [var:network], layername = [str], *attrname = [str], *direction=['in'(default),'out','both'])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates the degree centrality for the specified layer and network, and storing the result as a node attribute. The optional direction parameter decides whether the inbound (default) or the outbound ties should be counted, or - for layers with directional relations - if both in- and outbound ties should be counted. The node attribute is automatically named to the layername and the direction, but this can be overridden with the attrname parameter. For 2-mode layers, the direction argument is moot.";

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
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            string? attrname = command.GetArgument("attrname");
            EdgeTraversal direction = command.GetArgumentParseEnum<EdgeTraversal>("direction", EdgeTraversal.In);
            return CommandResult.FromOperationResult(Analyses.DegreeCentralities(network, layerName, attrname, direction));
        }
    }
}