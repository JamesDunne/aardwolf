using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Aardwolf
{
    public interface IHttpRequestContext
    {
        IHttpAsyncHostHandlerContext HostContext { get; }

        HttpListenerRequest Request { get; }
        IPrincipal User { get; }
    }
}
