using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace REST0.APIService.Services
{
    class QueryDescriptor
    {
        public Dictionary<string, string> XMLNamespaces { get; set; }
        public string CTEidentifier { get; set; }
        public string CTEexpression { get; set; }
        public string From { get; set; }
        public string Where { get; set; }
        public string Select { get; set; }
        public string GroupBy { get; set; }
        public string Having { get; set; }
        public string OrderBy { get; set; }

        /// <summary>
        /// The final composed SQL query.
        /// </summary>
        public string SQL { get; internal set; }
        /// <summary>
        /// Any errors encountered while parsing the query.
        /// </summary>
        public List<string> Errors { get; internal set; }
    }
}
