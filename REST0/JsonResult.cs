using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable 1998

namespace REST0
{
    public class JsonResult : StatusResponse, IHttpResponseAction
    {
        // NOTE(jsd): Fields are serialized to JSON in lexical definition order.
        public readonly bool success;

        // NOTE(jsd): This is here primarily for JSONP compatibility.
        public readonly int statusCode;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public readonly string message;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public readonly object errors;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public readonly object meta;

        // NOTE(jsd): `results` must be last.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public readonly object results;

        public JsonResult(int statusCode, string failureMessage)
            : base(statusCode, failureMessage)
        {
            this.statusCode = statusCode;
            success = false;
            message = failureMessage;
            errors = null;
            results = null;
            meta = null;
        }

        public JsonResult(int statusCode, string failureMessage, object errorData)
            : base(statusCode, failureMessage)
        {
            this.statusCode = statusCode;
            success = false;
            message = failureMessage;
            errors = errorData;
            results = null;
            meta = null;
        }

        public JsonResult(object successfulResults)
            : base(200, "OK")
        {
            statusCode = 200;
            success = true;
            message = null;
            errors = null;
            results = successfulResults;
            meta = null;
        }

        public JsonResult(object successfulResults, object metaData)
            : base(200, "OK")
        {
            statusCode = 200;
            success = true;
            message = null;
            errors = null;
            results = successfulResults;
            meta = metaData;
        }

        public override async Task Execute(IHttpRequestResponseContext context)
        {
            SetStatus(context);
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentEncoding = UTF8.WithoutBOM;

            using (context.Response.OutputStream)
            using (var tw = new StreamWriter(context.Response.OutputStream, UTF8.WithoutBOM))
                Json.Serializer.Serialize(tw, this);
        }
    }
}
