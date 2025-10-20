using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.DTO
{
    public sealed record LayerTwoModeMetadata
    (
        string Layername
    ) : LayerMetadataBase(Layername, 2);
}
