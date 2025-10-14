using Threadle.Core.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Utilities
{
    public class StructureResult
    {
        public IStructure MainStructure { get; }
        public Dictionary<string, IStructure> AdditionalStructures { get; }

        public StructureResult(IStructure mainStructure, Dictionary<string, IStructure> additionalStructures = null)
        {
            MainStructure = mainStructure;
            AdditionalStructures = additionalStructures ?? new();
        }
    }
}
