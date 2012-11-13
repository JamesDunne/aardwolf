using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Aardwolf
{
    public interface IHttpRequestResponseContext
    {
        IHttpAsyncHostHandlerContext HostContext { get; }

        HttpListenerRequest Request { get; }
        IPrincipal User { get; }

        HttpListenerResponse Response { get; }

        Stream OutputStream { get; }
    }
}
