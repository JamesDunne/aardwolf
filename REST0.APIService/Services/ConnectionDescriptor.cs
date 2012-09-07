using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace REST0.APIService.Services
{
    class ConnectionDescriptor
    {
        public string DataSource { get; set; }
        public string InitialCatalog { get; set; }
        public string UserID { get; set; }
        public string Password { get; set; }
    }
}
