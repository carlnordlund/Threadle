using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.DTO
{
    public abstract record StructureMetadata
    (
        string Name,
        string Filepath
    );
}
