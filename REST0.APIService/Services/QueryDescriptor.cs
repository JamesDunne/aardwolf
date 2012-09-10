using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace REST0.APIService.Services
{
    class QueryDescriptor
    {
        [JsonIgnore]
        public Dictionary<string, string> XMLNamespaces { get; set; }
        [JsonIgnore]
        public string CTEidentifier { get; set; }
        [JsonIgnore]
        public string CTEexpression { get; set; }
        [JsonIgnore]
        public string From { get; set; }
        [JsonIgnore]
        public string Where { get; set; }
        [JsonIgnore]
        public string Select { get; set; }
        [JsonIgnore]
        public string GroupBy { get; set; }
        [JsonIgnore]
        public string Having { get; set; }
        [JsonIgnore]
        public string OrderBy { get; set; }

        /// <summary>
        /// The final composed SQL query.
        /// </summary>
        [JsonProperty("sql")]
        public string SQL { get; internal set; }
        /// <summary>
        /// Any errors encountered while parsing the query.
        /// </summary>
        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Errors { get; internal set; }
    }
}
