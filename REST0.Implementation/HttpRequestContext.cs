using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public sealed class HttpRequestContext : IHttpRequestContext
    {
        public IHttpAsyncHostHandlerContext HostContext { get; private set; }

        public HttpListenerRequest Request { get; private set; }
        public IPrincipal User { get; private set; }

        public HttpRequestContext(IHttpAsyncHostHandlerContext hostContext, HttpListenerRequest request, IPrincipal user)
        {
            HostContext = hostContext;
            Request = request;
            User = user;
        }
    }
}
