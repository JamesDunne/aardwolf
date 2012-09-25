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
    public class JsonRootResponse : IHttpResponseAction
    {
        // NOTE(jsd): Fields are serialized to JSON in lexical definition order.

        // NOTE(jsd): This is here primarily for JSONP compatibility.
        public readonly int statusCode;

        public readonly bool success;

        [JsonIgnore]
        readonly string statusDescription;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public readonly string message;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public readonly object meta;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public readonly object errors;

        // NOTE(jsd): `results` must be last.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public readonly object results;

        public JsonRootResponse(int statusCode = 200, string statusDescription = null, string message = null, object meta = null, object[] errors = null, object results = null)
        {
            this.statusCode = statusCode;
            this.statusDescription = statusDescription;
            this.success = errors == null;
            this.message = message;
            this.meta = meta;
            this.errors = errors;
            this.results = results;
        }

#if false
        public JsonRootResult(int statusCode, string failureMessage)
        {
            this.statusCode = statusCode;
            //statusDescription = failureMessage;
            success = false;
            message = failureMessage;
            errors = null;
            results = null;
            meta = null;
        }

        public JsonRootResult(int statusCode, string failureMessage, object errorData)
        {
            this.statusCode = statusCode;
            //statusDescription = failureMessage;
            success = false;
            message = failureMessage;
            errors = errorData;
            results = null;
            meta = null;
        }

        public JsonRootResult(object successfulResults)
        {
            statusCode = 200;
            statusDescription = "OK";
            success = true;
            message = null;
            errors = null;
            results = successfulResults;
            meta = null;
        }

        public JsonRootResult(object successfulResults, object metaData)
        {
            statusCode = 200;
            statusDescription = "OK";
            success = true;
            message = null;
            errors = null;
            results = successfulResults;
            meta = metaData;
        }
#endif

        public async Task Execute(IHttpRequestResponseContext context)
        {
            context.Response.StatusCode = statusCode;
            if (statusDescription != null)
                context.Response.StatusDescription = statusDescription;
            context.Response.ContentType = "application/json; charset=utf-8";
            // NOTE(jsd): Not concerned at all about setting ContentLength64.

            // NOTE(jsd): This seems to be a hot line re: performance.
            //context.Response.ContentEncoding = UTF8.WithoutBOM;

#if true
            var rsp = context.Response.OutputStream;
            using (rsp)
            {
                var tw = new StreamWriter(rsp, UTF8.WithoutBOM);
                Json.Serializer.Serialize(tw, this);
                tw.Close();
            }
#else
            // NOTE(jsd): Just testing out some stuff here...

            var sb = new StringBuilder(1024);
            using (var sw = new StringWriter(sb))
            {
                Json.Serializer.Serialize(sw, this);
            }

            // Char count or byte count?
            context.Response.ContentLength64 = sb.Length;
            var rsp = context.Response.OutputStream;
            var enc = UTF8.WithoutBOM;

            // NOTE: bad buffering here; number of bytes != number of chars
            // Doesn't seem to speed anything up either.
            byte[] buf = new byte[4096];
            for (int i = 0; i < sb.Length; i += 4096)
            {
                string tmp = sb.ToString(i, Math.Min(4096, sb.Length - i));
                int count = enc.GetBytes(tmp, 0, tmp.Length, buf, 0);
                await rsp.WriteAsync(buf, 0, count);
            }
#endif
        }
    }
}
