using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Threadle.Core.Model
{
    public interface IStructure
    {
        string Name { get; set; }
        
        string Filepath { get; set; }

        bool IsModified { get; set; }

        List<string> Content { get; }

        Dictionary<string,object> Info { get; }
    }
}
