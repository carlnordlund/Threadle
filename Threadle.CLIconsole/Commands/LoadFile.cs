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
    public class LoadFile : ICommand
    {
        public string Usage => "[var:structure] = loadfile(file = \"[str]\", type = ['nodeset', 'network'])";
        public string Description => "Loads a structure from file 'file', using the internal text-based file format. The type of structure is given by the 'type' argument, which can be either 'nodeset' or 'network'. if the filepath has the ending .tsv, it is saved in the standard internal text-based format, if the .tsv.gz is used, it is subsequently gzipped and saved as such.";

        public bool ToAssign => true;

        public void Execute(Command command, CommandContext context)
        {
            string variableName = command.CheckAndGetAssignmentVariableName();
            string filepath = command.GetArgumentThrowExceptionIfMissingOrNull("file", "arg0");
            string typeString = command.GetArgumentThrowExceptionIfMissingOrNull("type", "arg1");
            //string assignedVariable = command.AssignedVariable!;

            StructureResult structures = FileManager.Load(filepath, typeString, FileFormat.TsvGzip);
            context.SetVariable(variableName, structures.MainStructure);
            ConsoleOutput.WriteLine($"Structure '{structures.MainStructure.Name}' loaded and stored in variable '{variableName}'");
            if (structures.AdditionalStructures.Count > 0)
                foreach (var kvp in structures.AdditionalStructures)
                {
                    string additionalAssignedVariable = variableName + "_" + kvp.Key;
                    context.SetVariable(additionalAssignedVariable, kvp.Value);
                    ConsoleOutput.WriteLine($"Structure '{kvp.Value.Name}' created and stored in variable '{additionalAssignedVariable}'.");
                }
        }
    }
}
