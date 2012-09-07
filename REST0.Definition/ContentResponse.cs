using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public sealed class ContentResponse : IHttpResponseAction
    {
        readonly string response;

        public ContentResponse(string response)
        {
            this.response = response;
        }

        public async Task Execute(IHttpRequestResponseContext context)
        {
            context.Response.StatusCode = 200;
            context.Response.StatusDescription = "OK";
            context.Response.ContentLength64 = response.Length;
            context.Response.SendChunked = false;
            context.Response.ContentType = "text/html";

            using (context.Response.OutputStream)
            using (var tw = new System.IO.StreamWriter(context.Response.OutputStream, UTF8.WithoutBOM, 65536, true))
                await tw.WriteAsync(response);
        }
    }
}
