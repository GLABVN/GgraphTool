using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Glab.C_Graph
{
    public interface IConnectable
    {
        Dictionary<string, object> Attributes { get; set; }
    }
}
