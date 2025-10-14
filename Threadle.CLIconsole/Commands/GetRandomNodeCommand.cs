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
    public class GetRandomNodeCommand : ICommand
    {
        public string Usage => "[uint] = getrandom(nodeset = [var:nodeset])";
        public string Description => "Gets the nodeId of a randomly selected node from the specified nodeset. In silent (-p) mode, only the nodeId value is returned.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            string nodesetName = command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0");
            //Nodeset nodeset = context.GetVariable<Nodeset>(nodesetName)
            //    ?? throw new Exception($"!Error: Nodeset '{nodesetName}' not found.");
            //Node node = nodeset.GetRandomNode();
            //ConsoleOutput.Write($"Random nodeId: ");
            //ConsoleOutput.WriteLine($"{node.Id}", true);
        }
    }
}
