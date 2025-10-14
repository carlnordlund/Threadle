using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.Core.Model
{
    /// <summary>
    /// Defines a particular node attribute (as an immutable record) with a
    /// particular name and <see cref="AttributeType">
    /// </summary>
    /// <param name="Name">The name of the node attribute.</param>
    /// <param name="Type">The type (<see cref="AttributeType"/>) of the node attribute.</param>
    public record NodeAttributeDefinition(string Name, AttributeType Type);
}
