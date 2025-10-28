using Threadle.CLIconsole.CLIUtilities;
using Threadle.Core.Model;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Commands
{
    public class Filter : ICommand
    {
        public string Usage => "[var:nodeset1] = filter(nodeset = [var:nodeset2], attrname = [str], cond=['eq','ne','gt','lt','ge','le','isnull','notnull'], +attrvalue = [str])";
        public string Description => "Creates and stores a new nodeset [var:nodeset1] containing all the nodes in [var:nodeset2] that fulfills the specified condition 'cond' concerning the specified attribute 'attrname' and the reference value 'attrvalue'. The new nodeset and its nodes and attributes constitute a partial deep copy of the inbound nodeset, making them completely independent from each other. Note: when checking for 'isnull' or 'notnull', the 'attrvalue' argument can be ignored.";

        public void Execute(Command command, CommandContext context)
        {
            //command.CheckAssignment(true);
            string variableName = command.CheckAndGetAssignmentVariableName();
            Nodeset nodeset = context.GetVariableThrowExceptionIfMissing<Core.Model.Nodeset>(command.GetArgumentThrowExceptionIfMissingOrNull("nodeset", "arg0"));
            string attributeName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg1");
            ConditionType conditionType = command.GetArgumentParseEnumThrowExceptionIfMissingOrNull<ConditionType>("cond", "arg2");
            string attributeValue = (conditionType == ConditionType.isnull || conditionType == ConditionType.notnull) ? "" : command.GetArgumentThrowExceptionIfMissingOrNull("attrvalue", "arg3");
            OperationResult<Core.Model.Nodeset> result = NodesetProcessor.Filter(nodeset, attributeName, conditionType, attributeValue);
            if (result.Success)
                context.SetVariable(variableName, result.Value!);
            ConsoleOutput.WriteLine(result.ToString());
        }
    }
}
