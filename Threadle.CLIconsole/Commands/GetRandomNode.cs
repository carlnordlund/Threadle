using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Analysis;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    public class GetRandomNode : ICommand
    {
        public string Usage => "[uint] = getrandomnode(structure = [var:structure])";
        public string Description => "Get a random node id from the specified nodeset (or the nodeset that is used by this network).";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            Nodeset nodeset = context.GetNodesetFromIStructure(command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"));

            OperationResult<uint> result = Analyses.GetRandomNode(nodeset);
            if (result.Success)
                ConsoleOutput.WriteLine(result.Value.ToString(), true);
            else
                ConsoleOutput.WriteLine(result.ToString());
        }
    }
}
