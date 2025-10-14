using PopnetEngine.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.CLIconsole.Commands
{
    public class InfoCommand : ICommand
    {
        public string Usage => "info(object=[var:object])";
        public string Description => "Displays basic information about the object with the variable name [var:object] such as size, types etc. For viewing actual content, use 'view()'.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            string objName = command.GetArgumentThrowExceptionIfMissingOrNull("object", "arg0");
            IStructure structure = context.GetVariable(objName)
                ?? throw new Exception($"!Error: No object variable '{objName}' found.");
            ConsoleOutput.WriteLine(structure.Infotext, true);
        }
    }
}
