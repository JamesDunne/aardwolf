using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace REST0.APIService.Services
{
    class ParameterTypeDescriptor
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("length", NullValueHandling = NullValueHandling.Ignore)]
        public int? Length { get; set; }
        [JsonProperty("scale", NullValueHandling = NullValueHandling.Ignore)]
        public int? Scale { get; set; }
    }
}
