using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public interface IHttpAsyncHost
    {
        List<string> Prefixes { get; }

        void Run(params string[] uriPrefixes);
    }
}
