using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Threadle.Core.DTO
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(LayerOneModeMetadata))]
    [JsonDerivedType(typeof(LayerTwoModeMetadata))]
    public abstract record LayerMetadataBase
    (
        string LayerName,
        int Mode
    );
}
