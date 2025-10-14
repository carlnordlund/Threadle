using PopnetEngine.Core.Model;
using PopnetEngine.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PopnetEngine.Core.Utilities;
using System.Net;
using PopnetEngine.CLIconsole.CLIUtilities;

namespace PopnetEngine.CLIconsole.Commands
{
    public class GenerateNetworkCommand : ICommand
    {
        public string Usage => "[var:network] = generate(type=['er'], (n=[int]|nodeset=[name]), p=[double], *directed=[bool], *selfties=[bool], *name=[name],*boolattr=[bool],*boolattrprob=[float],*catattrs=[int])";
        public string Description => "Generates a random network with the name [name], either generating a new Nodeset with the size specified by the argument 'n' or using the nodeset specified with the variable provided by the argument 'nodeset'. The network will be stored in the variable [var:network]. If a new Nodeset is generated, that will be stored with the name '[var:network]_nodeset'. The type of network is currently only 'er' (Erdös-Renyi). The probability of a tie is given by the 'p' parameter. By default, the network is symmetric and is not allowing self-ties, both with can be adjusted with the 'directed' and 'selfties' boolean arguments. If the optional 'boolattr' is set to true, a random boolean attribute 'bool0' will also be generated, with the probability of being true as given by the 'boolattrprob' argument. If catattrs integer is given, a new node attribute of type int is generated with a uniform distribution of values from 0 to the specified 'catattrs' value.";


        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(true);
            string assignmentName = command.AssignedVariable!;
            string assignmentName_nodeset = assignmentName + "_nodeset";
            string type = command.GetArgumentThrowExceptionIfMissingOrNull("type", "arg0");
            bool generateRandomBoolAttr = command.GetArgumentParseBool("boolattr", false);
            int catAttrs = command.GetArgumentParseInt("catattrs", 0);
            float boolattrprob = command.GetArgumentParseFloat("boolattrprob", 0f);
            Nodeset nodeset;
            bool storeNodeset = false;
            if (command.GetArgument("nodeset") is string nodesetName)
                nodeset = context.GetVariable<Nodeset>(nodesetName)
                    ?? throw new Exception($"!Error: Nodeset {nodesetName} not found.");                
            else
            {
                string nStr = command.GetArgument("n", "arg1")
                    ?? throw new Exception("!Error: Either 'nodeset' or 'n' must be specified.");
                if (Int32.TryParse(nStr, out int n) && n <= 1)
                    throw new Exception("!Error: Number of nodes (n) must be greater than 1.");
                nodeset = new Nodeset(assignmentName_nodeset, n, true);
                storeNodeset = true;

            }
            string name = command.GetArgument("name") ?? assignmentName;
            Network network;

            switch (type.ToLower())
            {
                case "er":
                    double p = command.GetArgumentParseDoubleThrowExceptionIfMissingOrNull("p", "arg2");
                    if (p < 0 || p > 1)
                        throw new Exception($"!Error: Proportion of ties (p) must be between 0 and 1.");
                    bool directed = command.GetArgumentParseBool("directed", false);
                    bool selfties = command.GetArgumentParseBool("selfties", false);
                    network = NetworkGenerators.GenerateErdosRenyi3(nodeset, p, directed, selfties);
                    network.Name = name;
                    break;
                default:
                    ConsoleOutput.WriteLine($"!Error: Random network type '{type}' not implemented.");
                    return;
            }

            if (generateRandomBoolAttr)
            {
                NetworkGenerators.GenerateRandomBooleanAttribute(network, "bool0", boolattrprob);
            }
            if (catAttrs > 0)
            {
                NetworkGenerators.GenerateRandomIntAttribute(network, "int0", catAttrs);
            }
            

            if (storeNodeset)
            {
                context.SetVariable(assignmentName_nodeset, nodeset);
                ConsoleOutput.WriteLine($"Nodeset '{nodeset.Name}' created and stored in variable '{assignmentName_nodeset}'");
            }
            context.SetVariable(assignmentName, network);
            ConsoleOutput.WriteLine($"Network '{network.Name}' created and stored in variable '{assignmentName}'");
        }
    }
}
