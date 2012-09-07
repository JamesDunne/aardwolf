using REST0.Definition;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Hson;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

#if MS
using System.Json;
#else
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#endif

#pragma warning disable 1998

namespace REST0.APIService
{
    public sealed class APIHttpAsyncHandler : IHttpAsyncHandler, IInitializationTrait, IConfigurationTrait
    {
        ConfigurationDictionary localConfig;
#if MS
        SHA1Hashed<JsonValue> _serviceConfig;
#else
        SHA1Hashed<JObject> _serviceConfig;
#endif

        #region Handler configure and initialization

        public async Task<bool> Configure(IHttpAsyncHostHandlerContext hostContext, ConfigurationDictionary configValues)
        {
            // Configure gets called first.
            localConfig = configValues;
            return true;
        }

        public async Task<bool> Initialize(IHttpAsyncHostHandlerContext context)
        {
            // Initialize gets called after Configure.
            if (!await RefreshConfigData())
                return false;

            // Let a background task refresh the config data every 10 seconds:
#pragma warning disable 4014
            Task.Run(async () =>
            {
                while (true)
                {
                    // Wait until the next even 10-second mark on the clock:
                    const long sec10 = TimeSpan.TicksPerSecond * 10;
                    var now = DateTime.UtcNow;
                    var next10 = new DateTime(((now.Ticks + sec10) / sec10) * sec10, DateTimeKind.Utc);
                    await Task.Delay(next10.Subtract(now));

                    // Refresh config data:
                    await RefreshConfigData();
                }
            });
#pragma warning restore 4014

            return true;
        }

        #endregion

        #region Dealing with remote-fetch of configuration data

        async Task<bool> RefreshConfigData()
        {
            // Get the latest config data:
            var config = await FetchConfigData();
            if (config == null) return false;

            _serviceConfig = config;
            return true;
        }

#if MS
        async Task<SHA1Hashed<JsonValue>> FetchConfigData()
#else
        async Task<SHA1Hashed<JObject>> FetchConfigData()
#endif
        {
            string url, path;
            bool noConfig = true;

            // Prefer to fetch over HTTP:
            if (localConfig.TryGetSingleValue("config.Url", out url))
            {
                noConfig = false;
                Trace.WriteLine("Getting config data via HTTP");

                // Fire off a request now to our configuration server for our config data:
                try
                {
                    var req = HttpWebRequest.CreateHttp(url);
                    using (var rsp = await req.GetResponseAsync())
                    using (var rspstr = rsp.GetResponseStream())
                    using (var hsr = new HsonReader(rspstr))
                    using (var sha1 = new SHA1TextReader(hsr, REST0.Definition.UTF8Encoding.WithoutBOM))
#if MS
                        return new SHA1Hashed<JsonValue>(JsonValue.Load(sha1), sha1.GetHash());
#else
                    using (var jr = new JsonTextReader(sha1))
                        return new SHA1Hashed<JObject>(new JsonSerializer().Deserialize<JObject>(jr), sha1.GetHash());
#endif
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());

                    // Fall back on loading a local file:
                    goto loadFile;
                }
            }

        loadFile:
            if (localConfig.TryGetSingleValue("config.Path", out path))
            {
                noConfig = false;
                Trace.WriteLine("Getting config data via file");

                // Load the local JSON file:
                try
                {
                    using (var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var hsr = new HsonReader(fs, true))
                    using (var sha1 = new SHA1TextReader(hsr, REST0.Definition.UTF8Encoding.WithoutBOM))
#if MS
                        return new SHA1Hashed<JsonValue>(JsonValue.Load(sha1), sha1.GetHash());
#else
                    using (var jr = new JsonTextReader(sha1))
                        return new SHA1Hashed<JObject>(new JsonSerializer().Deserialize<JObject>(jr), sha1.GetHash());
#endif
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());
                    return null;
                }
            }

            // If all else fails, complain:
            if (noConfig)
                throw new Exception(String.Format("Either '{0}' or '{1}' configuration keys are required", "config.Url", "config.Path"));

            return null;
        }

        #endregion

        #region Main handler logic

        /// <summary>
        /// Main logic.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<IHttpResponseAction> Execute(IHttpRequestContext context)
        {
            // Capture the current service configuration values only once per connection in case they update during:
            var config = _serviceConfig;

            if (context.Request.Url.AbsolutePath == "/")
                return new RedirectResponse("/foo");

#if MS
            return new JsonResponse(new JsonObject() {
                { "hash", config.HashHexString },
                { "config", config.Value }
            });
#else
            return new JsonResponse(new JObject() {
                { "hash", config.HashHexString },
                { "config", config.Value }
            });
#endif
        }

        #endregion
    }
}
