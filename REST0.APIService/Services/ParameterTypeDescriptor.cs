using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace REST0.APIService.Services
{
    class ParameterTypeDescriptor
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int? Length { get; set; }
        public int? Scale { get; set; }
    }
}
