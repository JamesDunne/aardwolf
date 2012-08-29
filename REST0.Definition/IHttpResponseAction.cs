using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    /// <summary>
    /// Represents any HTTP response action to respond to a client with.
    /// </summary>
    public interface IHttpResponseAction
    {
        /// <summary>
        /// Execute the intended response action against the Response.
        /// </summary>
        /// <param name="state">The current connection's request/response state.</param>
        /// <returns>A task which represents an asynchronous operation to await or null if a synchronous operation already completed.</returns>
        Task Execute(HttpRequestResponseState state);
    }
}
