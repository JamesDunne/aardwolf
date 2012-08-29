using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public sealed class HttpRequestState
    {
        public IHttpAsyncHostHandlerContext Context { get; private set; }

        public HttpListenerRequest Request { get; private set; }
        public IPrincipal User { get; private set; }

        public HttpRequestState(HttpListenerRequest request, IPrincipal user)
        {
            Request = request;
            User = user;
        }
    }
}
