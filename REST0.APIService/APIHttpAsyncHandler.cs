using REST0.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable 1998

namespace REST0.APIService
{
    public sealed class APIHttpAsyncHandler : IHttpAsyncHandler, IInitializationTrait, IConfigurationTrait
    {
        Dictionary<string, List<string>> _config;
        Task _getConfig;

        public async Task Configure(IHttpAsyncHostHandlerContext hostContext, Dictionary<string, List<string>> configValues)
        {
            _config = configValues;
        }

        public async Task Initialize(IHttpAsyncHostHandlerContext context)
        {
            // We can fire off a request now to our configuration server for our config data:
            // Pretend with a delay of 10 seconds for now:
            _getConfig = Task.Delay(TimeSpan.FromSeconds(5d));
            _config["config.Url"].First();
        }

        /// <summary>
        /// Main logic.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<IHttpResponseAction> Execute(IHttpRequestContext context)
        {
            // Wait for our config data to arrive first:
            if (_getConfig != null)
            {
                await _getConfig;
                _getConfig = null;
            }

            if (context.Request.Url.AbsolutePath != "/")
                return null;

            return new RedirectResponse("/foo");
        }
    }
}
