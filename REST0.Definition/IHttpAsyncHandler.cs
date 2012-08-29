using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public interface IHttpAsyncHandler
    {
        /// <summary>
        /// Gets a set of traits of this handler.
        /// </summary>
        IEnumerable<ITrait> Traits { get; }

        /// <summary>
        /// Main execution method of the handler which returns an HTTP response intent.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        Task<IHttpResponseAction> Execute(IHttpRequestContext state);
    }
}
