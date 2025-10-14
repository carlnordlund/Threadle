using PopnetEngine.CLIconsole;
using PopnetEngine.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.CLIconsole.Commands
{
    public class DisplayNetworkCommand : ICommand
    {
        public string Usage => "display(object=[var:object])";
        public string Description => "Displays detailed information about the object with the variable name [var:object]. E.g. display(mynet) will display detailed information about the network with the variable 'mynet'";


        //public string Description => "Displays information about a network. Usage: displaynetwork(net=mytestnet)";

        public void Execute(Command command, CommandContext context)
        {
            //command.CheckAssignment(false);
            //string networkName = command.GetArgumentThrowExceptionIfMissingOrNull("object", "arg0");
            //// Modify so it takes IStructure instead
            ////NetworkModel network = context.GetNetworkThrowExceptionIfMissing(networkName);
            //NetworkModel network = context.GetVariableThrowExceptionIfMissing<NetworkModel>(networkName);
            //Console.WriteLine(network.ToString());
        }

    }
}
