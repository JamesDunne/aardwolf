using REST0.Definition;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable 1998

namespace REST0.APIService
{
    public sealed class APIHttpAsyncHandler : IHttpAsyncHandler, IInitializationTrait, IConfigurationTrait
    {
        ConfigurationDictionary _config;
        Task _getConfig;

        public async Task Configure(IHttpAsyncHostHandlerContext hostContext, ConfigurationDictionary configValues)
        {
            // Configure gets called first.
            _config = configValues;
        }

        public async Task Initialize(IHttpAsyncHostHandlerContext context)
        {
            // Initialize gets called after Configure.

            // Fire off a request now to our configuration server for our config data:
            var req = HttpWebRequest.CreateHttp(_config.SingleValue("config.Url"));
            var rsp = await req.GetResponseAsync();
            var rspstr = rsp.GetResponseStream();
            System.Json.JsonValue.Load(rspstr);
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
