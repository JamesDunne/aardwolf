using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace REST0.APIService.Services
{
    class ServiceDescriptor
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonIgnore()]
        public ServiceDescriptor BaseService { get; set; }
        [JsonIgnore()]
        public IDictionary<string, string> Tokens { get; set; }
        [JsonProperty("connection")]
        public ConnectionDescriptor Connection { get; set; }
        [JsonProperty("parameterTypes")]
        public IDictionary<string, ParameterTypeDescriptor> ParameterTypes { get; set; }
        [JsonProperty("methods")]
        public IDictionary<string, MethodDescriptor> Methods { get; set; }
    }
}
