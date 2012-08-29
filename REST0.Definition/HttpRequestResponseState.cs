using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public sealed class HttpRequestResponseState
    {
        public HttpListenerRequest Request { get; private set; }
        public IPrincipal User { get; private set; }
        public HttpListenerResponse Response { get; private set; }

        public HttpRequestResponseState(HttpListenerRequest request, IPrincipal user, HttpListenerResponse response)
        {
            Request = request;
            User = user;
            Response = response;
        }
    }
}
