using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable 1998

namespace REST0.Definition
{
    public sealed class JsonResponse : IHttpResponseAction
    {
        JsonObject _value;

        public JsonResponse(JsonObject value)
        {
            _value = value;
        }

        public async Task Execute(IHttpRequestResponseContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentEncoding = UTF8Encoding.WithoutBOM;
            _value.Save(context.Output);
        }
    }
}
