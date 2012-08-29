using REST0.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable 1998

namespace REST0.APIService
{
    public sealed class APIHttpAsyncHandler : IHttpAsyncHandler
    {
        readonly InitializeOnceTrait _init = new Initialization();

        /// <summary>
        /// Return our traits.
        /// </summary>
        public IEnumerable<ITrait> Traits
        {
            get
            {
                yield return _init;
            }
        }

        // Our initialization trait.
        class Initialization : InitializeOnceTrait
        {
            public override void Initialize(IHttpAsyncHostHandlerContext context)
            {
                var self = (APIHttpAsyncHandler)context.Handler;
            }
        }

        /// <summary>
        /// Main logic.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<IHttpResponseAction> Execute(IHttpRequestContext context)
        {
            if (context.Request.Url.AbsolutePath != "/")
                return null;

            return new RedirectResponse("/foo");
        }
    }
}
