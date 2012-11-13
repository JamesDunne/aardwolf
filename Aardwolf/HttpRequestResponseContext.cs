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
    public sealed class HttpRequestResponseContext : IHttpRequestResponseContext
    {
        public IHttpAsyncHostHandlerContext HostContext { get; private set; }

        public HttpListenerRequest Request { get; private set; }
        public IPrincipal User { get; private set; }

        public HttpListenerResponse Response { get; private set; }

        public Stream OutputStream { get { return Response.OutputStream; } }

        public HttpRequestResponseContext(IHttpRequestContext requestState, HttpListenerResponse response)
        {
            HostContext = requestState.HostContext;
            Request = requestState.Request;
            User = requestState.User;

            Response = response;
        }
    }
}
