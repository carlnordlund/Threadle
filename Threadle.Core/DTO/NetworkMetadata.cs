using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model;

namespace Threadle.Core.DTO
{
    public sealed record NetworkMetadata(
        string Name,
        string Filepath,
        string NodesetName,
        IReadOnlyList<LayerMetadataBase> Layers
    );
}
