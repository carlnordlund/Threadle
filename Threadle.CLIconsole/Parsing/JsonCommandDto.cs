using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.CLIconsole.Parsing
{
    internal sealed class JsonCommandDto
    {
        public string? Assign { get; set; }
        public string? Command { get; set; }
        public Dictionary<string, string>? Args { get; set; }
    }
}
