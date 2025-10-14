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
    public class SaveFileCommand : ICommand
    {
        public string Usage => "savefile(name = [var:structure], file = \"[str]\", *nodesetfile = \"[filepath]\")";
        public string Description => "Saves the structure [var:structure] to file 'file'. If the structure is a Network and if the Nodeset is also to be stored, the filepath where to save the Nodeset is given by 'nodesetfile'. A reference to this Nodeset file will then also be added in the Network data file.";

        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(false);
            string objName = command.GetArgumentThrowExceptionIfMissingOrNull("name", "arg0");
            IStructure structure = context.GetVariable(objName)
                ?? throw new Exception($"!Error: No object variable '{objName}' found.");
            string filepath = command.GetArgumentThrowExceptionIfMissingOrNull("file", "arg1");

            if (structure is Network && command.GetArgument("nodesetfile") is string nodesetFilepath)
            {
                FileManager.Save(structure, filepath, FileFormat.TsvGzip, nodesetFilepath);
                ConsoleOutput.WriteLine($"Saved network '{structure.Name}' to file: {filepath}");
                ConsoleOutput.WriteLine($"Also saved nodeset to '{nodesetFilepath}' and added reference in network.");
            }
            else
            {
                FileManager.Save(structure, filepath, FileFormat.TsvGzip);
                ConsoleOutput.WriteLine($"Saved structure '{structure.Name}' to file: {filepath}");
            }
        }
    }
}
