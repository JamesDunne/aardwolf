using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace REST0.APIService.Services
{
    class ParameterDescriptor
    {
        public string Name { get; set; }
        public string SqlName { get; set; }
        public ParameterTypeDescriptor Type { get; set; }
        public bool IsOptional { get; set; }
    }
}
