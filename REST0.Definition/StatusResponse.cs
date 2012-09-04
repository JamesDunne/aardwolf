using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public sealed class StatusResponse : IHttpResponseAction
    {
        readonly int statusCode;
        readonly string statusMessage;

        public StatusResponse(int statusCode, string statusMessage)
        {
            this.statusCode = statusCode;
            this.statusMessage = statusMessage;
        }

        public async Task Execute(IHttpRequestResponseContext context)
        {
            context.Response.StatusCode = statusCode;
            context.Response.StatusDescription = statusMessage;
            context.Response.OutputStream.Dispose();
        }
    }
}
