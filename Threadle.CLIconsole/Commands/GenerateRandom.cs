using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;

namespace Threadle.CLIconsole.Commands
{
    public class GenerateRandom : ICommand
    {
        public string Usage => "[var:network] = generate(type = ['er'], size = [int], p = [double], *directed = ['true'(default),'false'], *selfties = ['true','false'(default)], *newname = [str])";
        public string Description => "Creates a random network of the specified type (only Erdös-Renyi implemented so far) of specified size and tie probability (also density). The network is by default directed without selfties but that can be adjusted. Is automatically named but can be given a name with the optional parameter.";

        public bool ToAssign => true;

        public void Execute(Command command, CommandContext context)
        {
            string variableName = command.CheckAndGetAssignmentVariableName();
            string type = command.GetArgumentThrowExceptionIfMissingOrNull("type", "arg0");
            int size = command.GetArgumentParseIntThrowExceptionIfMissingOrNull("size", "arg1");
            double p = command.GetArgumentParseDoubleThrowExceptionIfMissingOrNull("p", "arg2");
            EdgeDirectionality directionality = command.GetArgumentParseBool("directed", true) ? EdgeDirectionality.Directed : EdgeDirectionality.Undirected;
            bool selfties = command.GetArgumentParseBool("selfties", false);
            if (!(command.GetArgument("newname") is string newName))
                newName = context.GetNextIncrementalName($"{type}_s{size}_p{p}");
            Network network = NetworkGenerators.ErdosRenyi(size, p, directionality, selfties);
            context.SetVariable(variableName, network);
            ConsoleOutput.WriteLine($"Network '{newName}' generated and stored in variable '{variableName}'.");
            

        }
    }
}
