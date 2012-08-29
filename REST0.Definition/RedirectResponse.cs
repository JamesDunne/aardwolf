using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    /// <summary>
    /// Provides a 302 redirect response to the given url.
    /// </summary>
    public sealed class RedirectResponse : IHttpResponseAction
    {
        public string Url { get; private set; }

        public RedirectResponse(string url)
        {
            this.Url = url;
        }

        public Task Execute(HttpRequestResponseState state)
        {
            state.Response.Redirect(Url);
            return null;
        }
    }
}
