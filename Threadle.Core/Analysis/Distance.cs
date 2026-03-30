using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;
using Threadle.Core.Utilities;

namespace Threadle.Core.Analysis
{
    public static class Distance
    {

        public static OperationResult<StructureResult> RandomWalkNodeAttributeDistances(Network network, string attrName, int maxSteps, string[]? layers, float walkfactor = 1.0f, bool balanced = false, bool weighted = false, bool backtrack = false, bool savesteps = false)
        {
            Nodeset nodeset = network.Nodeset;
            if (!nodeset.NodeAttributeDefinitionManager.TryGetAttributeIndex(attrName, out byte attrIndex))
                return OperationResult<StructureResult>.Fail("AttributeUnknown", $"Attribute '{attrName}' not found in nodeset '{nodeset.Name}'.");

            NodeAttributeType attrType = nodeset.NodeAttributeDefinitionManager.IndexToType[attrIndex];

            // Continue later when I have implemented string node attributes!




            return OperationResult<StructureResult>.Fail("NotYetImplemented", $"RandomWalkNodeAttributeDistances not yet implemented");

        }
    }
}
