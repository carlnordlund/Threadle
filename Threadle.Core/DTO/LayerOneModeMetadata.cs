using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.DTO
{
    public sealed record LayerOneModeMetadata
    (
        string Layername,
        EdgeType EdgeType,
        EdgeDirectionality EdgeDirectionality,
        bool AllowsSelfties
    ) : LayerMetadataBase(Layername, 1);
}
