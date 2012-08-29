using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public interface IHttpRequestResponseContext
    {
        IHttpAsyncHostHandlerContext HostContext { get; }

        HttpListenerRequest Request { get; }
        IPrincipal User { get; }

        HttpListenerResponse Response { get; }
    }
}
