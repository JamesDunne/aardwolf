using REST0.Definition;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Json;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable 1998

namespace REST0.APIService
{
    public sealed class APIHttpAsyncHandler : IHttpAsyncHandler, IInitializationTrait, IConfigurationTrait
    {
        ConfigurationDictionary localConfig;
        JsonValue serviceConfig;

        public async Task Configure(IHttpAsyncHostHandlerContext hostContext, ConfigurationDictionary configValues)
        {
            // Configure gets called first.
            localConfig = configValues;
        }

        public async Task Initialize(IHttpAsyncHostHandlerContext context)
        {
            // Initialize gets called after Configure.
            serviceConfig = await GetConfigData();

            // Let a background task refresh the config data every 10 seconds:
#pragma warning disable 4014
            Task.Run(async () =>
            {
                while (true)
                {
                    // Wait until the next even 10-second mark on the clock:
                    var now = DateTime.UtcNow;
                    var sec10 = TimeSpan.TicksPerSecond * 10;
                    var next10 = new DateTime(((now.Ticks + sec10) / sec10) * sec10, DateTimeKind.Utc);
                    await Task.Delay(next10.Subtract(now));

                    // Refresh config data:
                    serviceConfig = await GetConfigData();
                }
            });
#pragma warning restore 4014
        }

        private async Task<JsonValue> GetConfigData()
        {
            string url, path;

            // Prefer to fetch over HTTP:
            if (localConfig.TryGetSingleValue("config.Url", out url))
            {
                Trace.WriteLine("Getting config data via HTTP");
                // Fire off a request now to our configuration server for our config data:
                try
                {
                    var req = HttpWebRequest.CreateHttp(url);
                    using (var rsp = await req.GetResponseAsync())
                    using (var rspstr = rsp.GetResponseStream())
                        return JsonValue.Load(rspstr);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());
                    goto loadFile;
                }
            }

            // Fall back on loading a local file:
        loadFile:
            if (localConfig.TryGetSingleValue("config.Path", out path))
            {
                Trace.WriteLine("Getting config data via file");
                // Load the local JSON file:
                try
                {
                    using (var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var tr = new StreamReader(fs, true))
                        return JsonValue.Load(tr);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());
                    return null;
                }
            }

            // If all else fails, complain:
            throw new Exception(String.Format("Either '{0}' or '{1}' configuration keys are required", "config.Url", "config.Path"));
        }

        /// <summary>
        /// Main logic.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<IHttpResponseAction> Execute(IHttpRequestContext context)
        {
            // Capture the current service configuration values only once per connection in case they update during:
            var config = serviceConfig;

            if (context.Request.Url.AbsolutePath == "/")
                return new RedirectResponse("/foo");

            return new JsonResponse(config);
        }
    }
}
