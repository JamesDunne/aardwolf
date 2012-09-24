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
    public class JsonResult : IHttpResponseAction
    {
        // NOTE(jsd): Fields are serialized to JSON in lexical definition order.
        public readonly bool success;

        // NOTE(jsd): This is here primarily for JSONP compatibility.
        public readonly int statusCode;

        [JsonIgnore]
        readonly string statusDescription;

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
        {
            this.statusCode = statusCode;
            //statusDescription = failureMessage;
            success = false;
            message = failureMessage;
            errors = null;
            results = null;
            meta = null;
        }

        public JsonResult(int statusCode, string failureMessage, object errorData)
        {
            this.statusCode = statusCode;
            //statusDescription = failureMessage;
            success = false;
            message = failureMessage;
            errors = errorData;
            results = null;
            meta = null;
        }

        public JsonResult(object successfulResults)
        {
            statusCode = 200;
            statusDescription = "OK";
            success = true;
            message = null;
            errors = null;
            results = successfulResults;
            meta = null;
        }

        public JsonResult(object successfulResults, object metaData)
        {
            statusCode = 200;
            statusDescription = "OK";
            success = true;
            message = null;
            errors = null;
            results = successfulResults;
            meta = metaData;
        }

        public async Task Execute(IHttpRequestResponseContext context)
        {
            context.Response.StatusCode = statusCode;
            if (statusDescription != null)
                context.Response.StatusDescription = statusDescription;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentEncoding = UTF8.WithoutBOM;

            using (context.Response.OutputStream)
            using (var tw = new StreamWriter(context.Response.OutputStream, UTF8.WithoutBOM))
                Json.Serializer.Serialize(tw, this);
        }
    }
}
