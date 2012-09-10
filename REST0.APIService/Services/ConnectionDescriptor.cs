using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace REST0.APIService.Services
{
    class ConnectionDescriptor
    {
        [JsonProperty("dataSource")]
        public string DataSource { get; set; }
        [JsonProperty("initialCatalog")]
        public string InitialCatalog { get; set; }
        [JsonProperty("userID", NullValueHandling = NullValueHandling.Ignore)]
        public string UserID { get; set; }
        /// <remarks>
        /// JsonIgnored for security purposes.
        /// </remarks>
        [JsonIgnore]
        public string Password { get; set; }

        /// <summary>
        /// The final constructed connection string that is cached.
        /// </summary>
        /// <remarks>
        /// JsonIgnored for security purposes.
        /// </remarks>
        [JsonIgnore]
        public string ConnectionString { get; internal set; }
    }
}
