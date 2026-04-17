using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Analysis
{
    internal struct CategoryNodeset
    {
        public Nodeset Nodeset { get; init; }
        public Dictionary<string, uint> LabelToNodeId { get; init; }
        public string[] Labels { get; init; }
        public NodeAttributeType AttrType { get; init; }
        public byte AttrIndex { get; init; }
    }
}
