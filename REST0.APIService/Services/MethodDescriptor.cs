using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace REST0.APIService.Services
{
    class MethodDescriptor
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("deprecated", NullValueHandling = NullValueHandling.Ignore)]
        public string DeprecatedMessage { get; set; }
        [JsonProperty("parameters")]
        public IDictionary<string, ParameterDescriptor> Parameters { get; set; }
        [JsonProperty("connection")]
        public ConnectionDescriptor Connection { get; set; }
        [JsonProperty("query")]
        public QueryDescriptor Query { get; set; }

        internal MethodDescriptor Clone()
        {
            return new MethodDescriptor()
            {
                Name = this.Name,
                Parameters = new Dictionary<string, ParameterDescriptor>(this.Parameters, StringComparer.OrdinalIgnoreCase),
                Connection = this.Connection,
                Query = this.Query
            };
        }
    }
}
