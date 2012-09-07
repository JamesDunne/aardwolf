using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public abstract class StatusResponse
    {
        protected readonly int statusCode;
        protected readonly string statusDescription;

        protected StatusResponse(int statusCode, string statusDescription)
        {
            this.statusCode = statusCode;
            this.statusDescription = statusDescription;
        }

        protected void SetStatus(IHttpRequestResponseContext context)
        {
            context.Response.StatusCode = statusCode;
            context.Response.StatusDescription = statusDescription;
        }
    }
}
