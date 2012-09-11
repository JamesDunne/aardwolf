using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0
{
    public sealed class ContentResponse : StatusResponse
    {
        readonly string response;

        public ContentResponse(int statusCode, string statusDescription, string response)
            : base(statusCode, statusDescription)
        {
            this.response = response;
        }

        public async Task Execute(IHttpRequestResponseContext context)
        {
            SetStatus(context);
            context.Response.ContentLength64 = response.Length;
            context.Response.SendChunked = false;
            context.Response.ContentType = "text/html";

            using (context.Response.OutputStream)
            using (var tw = new System.IO.StreamWriter(context.Response.OutputStream, UTF8.WithoutBOM, 65536, true))
                await tw.WriteAsync(response);
        }
    }
}
