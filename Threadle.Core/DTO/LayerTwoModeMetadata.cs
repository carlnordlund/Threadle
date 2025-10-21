using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.DTO
{
    public record LayerTwoModeMetadata
    (
        string Layername
    ) : LayerMetadataBase(Layername, 2)
    {
        public override string ToString()
        {
            return $"{Layername} (2-mode)";
        }
    }
}
