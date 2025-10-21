using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.DTO
{
    public record LayerOneModeMetadata
    (
        string Layername,
        EdgeType EdgeType,
        EdgeDirectionality EdgeDirectionality,
        bool AllowsSelfties
    ) : LayerMetadataBase(Layername, 1)
    {
        public override string ToString()
        {
            return $"{Layername} (1-mode): {EdgeType}, {EdgeDirectionality},{(!AllowsSelfties ? "No selfties" : "")}";
        }
    }
}
