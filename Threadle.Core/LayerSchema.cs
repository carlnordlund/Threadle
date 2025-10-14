using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.Core
{
    public class LayerSchema
    {
        private Dictionary<byte, LayerDefinition> _layers = new();

        public void DefineLayer(byte index, LayerDefinition layerDefinition)
        {
            _layers[index] = layerDefinition;
        }

        public LayerDefinition? GetLayer(byte layerId)
        {
            return (_layers.ContainsKey(layerId)) ? _layers[layerId] : null;
        }

        public bool IsDirected(byte layerId)
        {
            //return (_layers.ContainsKey(layerId) && _layers[layerId].Directionality == EdgeDirectionality.Directed);
            return true;
        }

        public EdgeValueType GetEdgeValueType(byte layerId)
        {
            return EdgeValueType.Valued;
            //return _layers.TryGetValue(layerId, out LayerDefinition? def) ? def.ValueType : default(EdgeValueType);
        }

        public bool HasSelfties(byte layerid)
        {
            return true;
            //return _layers.TryGetValue(layerid, out LayerDefinition? def) ? def.SelfTies : false;
        }

    }
}
