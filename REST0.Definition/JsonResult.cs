using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public struct JsonResult
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
        {
            this.statusCode = statusCode;
            success = false;
            message = failureMessage;
            errors = null;
            results = null;
            meta = null;
        }

        public JsonResult(int statusCode, string failureMessage, object errorData)
        {
            this.statusCode = statusCode;
            success = false;
            message = failureMessage;
            errors = errorData;
            results = null;
            meta = null;
        }

        public JsonResult(object successfulResults)
        {
            statusCode = 200;
            success = true;
            message = null;
            errors = null;
            results = successfulResults;
            meta = null;
        }

        public JsonResult(object successfulResults, object metaData)
        {
            statusCode = 200;
            success = true;
            message = null;
            errors = null;
            results = successfulResults;
            meta = metaData;
        }
    }
}
