using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public sealed class JsonResponse : IHttpResponseAction
    {
        JsonValue _value;

        public JsonResponse(JsonValue value)
        {
            _value = value;
        }

        public async Task Execute(IHttpRequestResponseContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentEncoding = UTF8Encoding.WithoutBOM;
            using (var tw = new StreamWriter(context.Response.OutputStream, UTF8Encoding.WithoutBOM))
                _value.Save(tw);
        }
    }
}
