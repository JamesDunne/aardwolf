using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace REST0.APIService.Services
{
    class ServiceDescriptor
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonIgnore()]
        public ServiceDescriptor BaseService { get; set; }
        [JsonProperty("$")]
        public IDictionary<string, string> Tokens { get; set; }
        [JsonProperty("connection")]
        public ConnectionDescriptor Connection { get; set; }
        [JsonProperty("parameterTypes")]
        public IDictionary<string, ParameterTypeDescriptor> ParameterTypes { get; set; }
        [JsonProperty("methods")]
        public IDictionary<string, MethodDescriptor> Methods { get; set; }
    }

    class ServiceDescriptorSerialized
    {
        [JsonIgnore]
        readonly ServiceDescriptor desc;

        internal ServiceDescriptorSerialized(ServiceDescriptor desc)
        {
            this.desc = desc;
        }

        [JsonProperty("base", NullValueHandling = NullValueHandling.Ignore)]
        public string BaseService { get { return desc.BaseService == null ? null : desc.BaseService.Name; } }
        [JsonProperty("$", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, string> Tokens { get { return desc.Tokens; } }
        [JsonProperty("connection", NullValueHandling = NullValueHandling.Ignore)]
        public string Connection { get { return desc.Connection.ConnectionString; } }
        [JsonProperty("parameterTypes", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, ParameterTypeDescriptor> ParameterTypes
        {
            get
            {
                // TODO(jsd): Make the parser do copy-on-write instead of copy-on-inherit so that this will work.
                //if (desc.BaseService == null) return desc.ParameterTypes;
                //if (desc.ParameterTypes == desc.BaseService.ParameterTypes) return null;
                return desc.ParameterTypes;
            }
        }
        [JsonProperty("methods", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, MethodDescriptorSerialized> Methods { get { return desc.Methods.ToDictionary(m => m.Key, m => new MethodDescriptorSerialized(m.Value), StringComparer.OrdinalIgnoreCase); } }
    }
}
