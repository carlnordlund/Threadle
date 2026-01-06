using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Processing.Enums
{
    /// <summary>
    /// Enum to handle various comparisons that can be done
    /// Used by the filter command
    /// </summary>
    public enum ConditionType
    {
        eq,
        ne,
        gt,
        lt,
        ge,
        le,
        isnull,
        notnull
    }
}
