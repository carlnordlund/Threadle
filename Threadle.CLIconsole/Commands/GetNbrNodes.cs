using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class GetNbrNodes : ICommand
    {
        public string Usage => "[uint] = getnbrnodes(structure = [var:structure])";
        public string Description => "Get the number of nodes in this structure (which can either be a Nodeset or a Network structure). If a network is provided, it uses the Nodeset that this network is pointing to";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            Nodeset nodeset = context.GetNodesetFromIStructure(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));

            //string structureName = command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0");
            //Nodeset nodeset = context.GetNodesetFromIStructure(structureName);
            ConsoleOutput.WriteLine(nodeset.Count.ToString(), true);
        }
    }
}
