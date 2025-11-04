using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    public class Dichotomize : ICommand
    {
        public string Usage => "dichotomize(network = [var:network], layername = [str], *cond = ['eq','ne','gt','lt','ge'(default),'le','isnull'(invalid),'notnull'(invalid)], *threshold = [float(default:1)], *truevalue = [float(default:1)|'keep'], *falsevalue = [float(default:0)|'keep'], *newlayername = [str])";

        public string Description => "Takes a valued 1-mode layer (which it is only applicable for) and dichotomizes it, creating and storing this as a new layer. By default, all values equal to or greater to 1 will be converted to a binary tie and all other values represent a missing tie. Both the condition, the threshold value and the values resulting from a true vs. a false evaluation can be modified. If truevalue and falsevalue are kept at their default values, i.e. either containing 1 or 0, the resulting layer will be a binary network, otherwise the resulting layer will be valued. The directionality of the new layer will remain the same as the original layer. The new layer will be named as the provided layer, with the addition '-dichotomized', but the name of the new layer can also be specified with the 'newlayername' argument.";

        public bool ToAssign => false;

        public void Execute(Command command, CommandContext context)
        {
            Network network = context.GetVariableThrowExceptionIfMissing<Network>(command.GetArgumentThrowExceptionIfMissingOrNull("network", "arg0"));
            string layerName = command.GetArgumentThrowExceptionIfMissingOrNull("layername", "arg1").ToLowerInvariant();
            ConditionType conditionType = command.GetArgumentParseEnum<ConditionType>("cond", ConditionType.ge);
            float threshold = command.GetArgumentParseFloat("threshold", 1);
            float trueValue = command.GetArgument("truevalue") == "keep" ? float.NaN : command.GetArgumentParseFloat("truevalue", 1);
            float falseValue = command.GetArgument("falsevalue") == "keep" ? float.NaN : command.GetArgumentParseFloat("falsevalue", 0);
            string newLayerName = network.GetnextAvailableLayerName(command.GetArgumentParseString("newlayername", layerName + "-dichotomized").ToLowerInvariant());
            OperationResult result = NetworkProcessor.DichotomizeLayer(network, layerName, conditionType, threshold, trueValue, falseValue, newLayerName);
            ConsoleOutput.WriteLine(result.ToString());
        }
    }
}
