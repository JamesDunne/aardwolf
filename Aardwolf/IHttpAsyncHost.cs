using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aardwolf
{
    public interface IHttpAsyncHost
    {
        /// <summary>
        /// Gets the set of server bindings.
        /// </summary>
        List<string> Prefixes { get; }

        /// <summary>
        /// Run the server host and block the current thread.
        /// </summary>
        /// <param name="uriPrefixes"></param>
        void Run(params string[] uriPrefixes);
    }
}
