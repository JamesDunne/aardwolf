using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace REST0.APIService.Services
{
    class ParameterDescriptor
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("sqlName")]
        public string SqlName { get; set; }
        [JsonProperty("type")]
        public ParameterTypeDescriptor Type { get; set; }
        [JsonProperty("optional")]
        public bool IsOptional { get; set; }
    }
}
