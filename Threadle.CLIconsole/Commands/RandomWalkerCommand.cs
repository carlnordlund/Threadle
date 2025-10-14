using PopnetEngine.CLIconsole.CLIUtilities;
using PopnetEngine.Core.Analysis;
using PopnetEngine.Core.Model;
using PopnetEngine.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.CLIconsole.Commands
{
    public class RandomWalkerCommand : ICommand
    {
        public string Usage => "[var:matrix] = walker(network=[var:network], attribute=[string], restarts=[int], maxsteps=[int], *checkprob=[float])";
        public string Description => "Initializes and starts an attribute-aware random walker. Walks on all layers, keeping track of the specified attribute (which should be categorical/discrete). Storing average distances in the assigned matrix, and also stores matrices with standard deviations (*_stdev), median (*_median), max values (*_max), min values (*_min) and number of samples per pair (*_nbr_datapoints).";
        public void Execute(Command command, CommandContext context)
        {
            command.CheckAssignment(true);
            string networkName = command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0");
            Network network = context.GetVariable<Network>(networkName)
                ?? throw new Exception($"!Error: No Network '{networkName}' found.");
            string attributeName = command.GetArgumentThrowExceptionIfMissingOrNull("attribute", "arg2");
            int restarts = command.GetArgumentParseInt("restarts", 10);
            int maxStepsPerWalk = command.GetArgumentParseInt("maxsteps", 20);
            int bufferSize = command.GetArgumentParseInt("buffersize", 10);
            float checkProb = command.GetArgumentParseFloat("checkprob", 1f);

            string assignedVariable = command.AssignedVariable!;

            //AttributeDistanceWalker walker = new AttributeDistanceWalker
            //{
            //    Network = network,
            //    AttributeName = attributeName,
            //    Restarts = restarts,
            //    MaxStepsPerWalk = maxStepsPerWalk,
            //    CheckProb = checkProb
            //};
            //walker.Run(msg => ConsoleOutput.WriteLine(msg));

            //if (walker.StructureResult != null)
            //{
            //    context.SetVariable(assignedVariable, walker.StructureResult.MainStructure);
            //    if (walker.StructureResult.AdditionalStructures.Count > 0)
            //        foreach (var kvp in walker.StructureResult.AdditionalStructures)
            //        {
            //            context.SetVariable(assignedVariable + "_" + kvp.Key, kvp.Value);
            //            ConsoleOutput.WriteLine($"Also loaded {kvp.Key} '{kvp.Value.Name}'.");
            //        }
            //}
        }
    }
}
