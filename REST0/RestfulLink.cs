using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0
{
    public class RestfulLink
    {
        readonly protected string title;
        readonly protected string rel;
        readonly protected string href;

        internal RestfulLink(string title, string href, string rel)
        {
            this.title = title;
            this.rel = rel;
            this.href = href;
        }

        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get { return title; } }

        [JsonProperty("rel")]
        public string Rel { get { return rel; } }

        [JsonProperty("href")]
        public string Href { get { return href; } }

        public static RestfulLink Create(string title, string href, string rel = "related")
        {
            return new RestfulLink(title, href, rel);
        }
    }
}
