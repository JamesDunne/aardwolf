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

    class ParameterDescriptorSerialized
    {
        [JsonIgnore()]
        readonly ParameterDescriptor desc;

        internal ParameterDescriptorSerialized(ParameterDescriptor desc)
        {
            this.desc = desc;
        }

        [JsonProperty("sqlName")]
        public string SqlName { get { return desc.SqlName; } }
        [JsonProperty("type")]
        public string Type { get { return desc.Type.Name; } }
        [JsonProperty("optional", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsOptional { get { return desc.IsOptional ? true : (bool?)null; } }
    }
}
