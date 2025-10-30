using Threadle.Core.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Utilities
{
    public class StructureResult(IStructure mainStructure, Dictionary<string, IStructure>? additionalStructures = null)
    {
        public IStructure MainStructure { get; } = mainStructure;
        public Dictionary<string, IStructure> AdditionalStructures { get; } = additionalStructures ?? [];
    }
}
