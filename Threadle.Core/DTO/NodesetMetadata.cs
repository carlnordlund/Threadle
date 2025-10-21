using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.DTO
{
    public record NodesetMetadata
    (
        string Name,
        string Filepath,
        int NumberOfNodes,
        IReadOnlyList<NodeAttributeMetadata> NodeAttributes
    ) : StructureMetadata(Name, Filepath)
    {
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Nodeset: {Name}");
            sb.AppendLine($"  Filepath: {Filepath}");
            sb.AppendLine($"  Nbr nodes: {NumberOfNodes}");
            sb.AppendLine($"  Node attributes:");
            foreach (var attribute in NodeAttributes)
                sb.AppendLine($"    - {attribute.Name} ({attribute.Type})");
            return sb.ToString();
        }

    }
}
