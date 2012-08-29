using REST0.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.APIService
{
    public sealed class APIHttpAsyncHandler : IHttpAsyncHandler
    {
        public Task<IHttpResponseAction> Execute(HttpRequestState state)
        {
            if (state.Request.Url.AbsolutePath != "/")
                return Task.FromResult<IHttpResponseAction>(null);

            return Task.FromResult<IHttpResponseAction>(new RedirectResponse("/foo"));
        }
    }
}
