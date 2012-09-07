using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace REST0.APIService.Services
{
    class ServiceDescriptor
    {
        public string Name { get; set; }
        public ServiceDescriptor BaseService { get; set; }
        public ConnectionDescriptor Connection { get; set; }
        public IDictionary<string, ParameterTypeDescriptor> ParameterTypes { get; set; }
        public IDictionary<string, MethodDescriptor> Methods { get; set; }
        public IDictionary<string, string> Tokens { get; set; }
    }
}
