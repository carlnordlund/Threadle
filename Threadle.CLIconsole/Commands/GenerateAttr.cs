using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.CLIconsole.Parsing;
using Threadle.CLIconsole.Results;
using Threadle.CLIconsole.Runtime;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Processing;
using Threadle.Core.Utilities;

namespace Threadle.CLIconsole.Commands
{
    /// <summary>
    /// Class representing the 'generateattr' CLI command.
    /// </summary>
    public class GenerateAttr : ICommand
    {
        /// <summary>
        /// Gets the command syntax definition as shown in help and usage output.
        /// </summary>
        public string Syntax => "generateattr(structure = [var:structure], attrname = [str], *attrtype = ['int'(default),'float','bool','char'], *min = [int(default:0)|float(default:0.0)], *max = [int(default:100)|float(default:1.0)], +p = [double(default:0.5)], +chars = [str](default:\"m;f;o\")";

        /// <summary>
        /// Gets a human-readable description of what the command does.
        /// </summary>
        public string Description => "Creates a new node attribute with the given name and type for the specific nodeset, and sets a random attribute value to each node in this nodeset according to the provided parameters. The 'min' and 'max' arguments apply to the int and float variable types: they are optional, where the default 'min' value is 0 and the default 'max' value is, respectively, 100 for int types and 1 for float types. The 'p' argument applies to the bool variable type: this is the probability of the value being 'true'. The 'chars' argument applies to the char variable type: this consists of a string of semi-colon-separated characters, e.g. \"a;c;f;g;z\" from which the values will be uniformly picked from.";

        /// <summary>
        /// Gets a value indicating whether this command produces output that must be assigned to a variable.
        /// </summary>
        public bool ToAssign => false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The parsed <see cref="CommandPackage"/> to be executed.</param>
        /// <param name="context">The <see cref="CommandContext"/> providing shared console variable memory.</param>
        public CommandResult Execute(CommandPackage command, CommandContext context)
        {
            if (CommandHelpers.TryGetNodesetFromIStructure(context, command.GetArgumentThrowExceptionIfMissingOrNull("structure", "arg0"), out var nodeset) is CommandResult commandResult)
                return commandResult;
            string attrName = command.GetArgumentThrowExceptionIfMissingOrNull("attrname", "arg1");
            NodeAttributeType attrType = command.GetArgumentParseEnum<NodeAttributeType>("attrtype",NodeAttributeType.Int);
            switch (attrType)
            {
                case NodeAttributeType.Int:
                    int minInt = command.GetArgumentParseInt("min", 0);
                    int maxInt = command.GetArgumentParseInt("max", 100);
                    return CommandResult.FromOperationResult(NetworkGenerators.GenerateIntAttr(nodeset!, attrName, minInt, maxInt));
                case NodeAttributeType.Float:
                    float minFloat = command.GetArgumentParseFloat("min", 0);
                    float maxFloat = command.GetArgumentParseFloat("max", 1);
                    return CommandResult.FromOperationResult(NetworkGenerators.GenerateFloatAttr(nodeset!, attrName, minFloat, maxFloat));
                case NodeAttributeType.Bool:
                    double p = command.GetArgumentParseDouble("p", 0.5);
                    return CommandResult.FromOperationResult(NetworkGenerators.GenerateBoolAttr(nodeset!, attrName, p));
                case NodeAttributeType.Char:
                    string charstring = command.GetArgumentParseString("chars", "m;f;o");
                    return CommandResult.FromOperationResult(NetworkGenerators.GenerateCharAttr(nodeset!, attrName, charstring));
                default:
                    return CommandResult.Fail("AttributeTypeNotFound", $"Node attribute type '{attrType}' not implemented.");
            }
        }
    }
}
