using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace REST0.APIService.Services
{
    class MethodDescriptor
    {
        public string Name { get; set; }
        public string DeprecatedMessage { get; set; }
        public IDictionary<string, ParameterDescriptor> Parameters { get; set; }
        public ConnectionDescriptor Connection { get; set; }
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
