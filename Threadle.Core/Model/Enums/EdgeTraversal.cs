using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model.Enums
{
    /// <summary>
    /// Represents a direction of edges, e.g. whether in- or outdegree (or both) should be
    /// calculated.
    /// </summary>
    public enum EdgeTraversal
    {
        Out,
        In,
        Both
    }
}
