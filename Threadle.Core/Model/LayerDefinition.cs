using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.Core.Model
{
    // Holds information about a particular relational layer in a network
    // For instance: kinship networks, friendships, a traded commodity
    // Contains name and info about the layer: whether binary/valued/signed,
    // directional or undirectional
    // Note that layers may have same names: but layerId must be different
    public struct LayerDefinition
    {
        public string Name;
        public EdgeDirectionality Directionality;
        public EdgeValueType ValueType;
        public bool Selfties;

        public LayerDefinition(string name, EdgeDirectionality directionality, EdgeValueType valueType, bool selfties)
        {
            Name = name;
            Directionality = directionality;
            ValueType = valueType;
            Selfties = selfties;
        }
    }
}
