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
    public class DegreeCommand : ICommand
    {
        public string Usage => "degree(network = [var:network], layername = [str], *attrname = [str], *direction=('in'(default),'out','both'))";

        public string Description => "Calculates the degree centrality for the specified layer and network, and storing the result as a node attribute. The optional direction parameter decides whether the inbound (default) or the outbound ties should be counted, or - for layers with directional relations - if both in- and outbound ties should be counted. The node attribute is automatically named to the layername and the direction, but this can be overridden with the attrname parameter.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1");
            string? attrname = command.GetArgument("attrname");
            EdgeTraversal direction = Misc.ParseEnumOrNull<EdgeTraversal>(command.GetArgument("direction"), EdgeTraversal.In);
            OperationResult result = Analyses.DegreeCentrality(network, layerName, attrname, direction);
            ConsoleOutput.WriteLine(result.ToString());
        }
    }
}
