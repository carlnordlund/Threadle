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

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the '[...]' CLI command.
    /// </summary>
    public class Components : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "components(network = [var:network], layername = [str], *attrname = [str])";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Calculates the number of components for the specified layer and network, and storing the result as an integer node attribute representing which component it is part of. The node attribute is automatically named to the layername and 'components', but this can be overridden with the attrName parameter. To explore the number of components, use the getattrsummary() on this node attribute: the max value represents the number of components minus one. To calculate the size of a component: use the filter() command to create new nodesets for each value of this attribute and then use getnbrnodes() to get the size of the filtered nodesets. Not the smoothest right now, but it works!";

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
            if (CommandHelpers.TryGetVariable<Network>(context, command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"), out var network) is CommandResult commandResult)
                return commandResult;
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            string? attrName = command.GetArgument("attrname");
            var componentsResult = Analyses.ConnectedComponents(network, layerName, attrName);
            return CommandResult.FromOperationResult(componentsResult, componentsResult.Value);
        }
    }
}
