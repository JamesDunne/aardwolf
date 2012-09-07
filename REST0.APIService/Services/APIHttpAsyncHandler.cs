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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#pragma warning disable 1998

namespace REST0.APIService.Services
{
    public sealed class APIHttpAsyncHandler : IHttpAsyncHandler, IInitializationTrait, IConfigurationTrait
    {
        ConfigurationDictionary localConfig;
        SHA1Hashed<IDictionary<string, ServiceDescriptor>> services;

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

            // Let a background task refresh the config data every 30 seconds:
#pragma warning disable 4014
            Task.Run(async () =>
            {
                while (true)
                {
                    // Wait until the next even 30-second mark on the clock:
                    const long sec30 = TimeSpan.TicksPerSecond * 30;
                    var now = DateTime.UtcNow;
                    var next30 = new DateTime(((now.Ticks + sec30) / sec30) * sec30, DateTimeKind.Utc);
                    await Task.Delay(next30.Subtract(now));

                    // Refresh config data:
                    await RefreshConfigData();
                }
            });
#pragma warning restore 4014

            return true;
        }

        #endregion

        #region Dealing with remote-fetch of configuration data

        static string getJPropertyValue(JProperty prop)
        {
            return (string)((JValue)prop.Value).Value;
        }

        async Task<bool> RefreshConfigData()
        {
            // Get the latest config data:
            var config = await FetchConfigData();
            if (config == null) return false;

            // Parse the config document:
            var doc = config.Value;

            var tmpServices = new Dictionary<string, ServiceDescriptor>(StringComparer.OrdinalIgnoreCase);

            // 'services' section is not optional:
            JToken jtServices;
            if (!doc.TryGetValue("services", out jtServices))
                return false;
            var joServices = (JObject)jtServices;

            // Parse the root token dictionary first:
            var rootTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var jpTokens = joServices.Property("$");
            if (jpTokens != null)
            {
                // Extract the key/value pairs onto a copy of the token dictionary:
                foreach (var prop in ((JObject)jpTokens.Value).Properties())
                    rootTokens[prop.Name] = getJPropertyValue(prop);
            }

            // Parse each service descriptor:
            foreach (var jpService in joServices.Properties())
            {
                if (jpService.Name == "$") continue;
                var joService = (JObject)jpService.Value;

                // This property is a service:

                ServiceDescriptor baseService = null;

                IDictionary<string, string> tokens;
                ConnectionDescriptor conn;
                IDictionary<string, ParameterTypeDescriptor> parameterTypes;
                IDictionary<string, MethodDescriptor> methods;

                // Go through the properties of the named service object:
                var jpBase = joService.Property("base");
                if (jpBase != null)
                {
                    // NOTE(jsd): Forward references are not allowed. Base service
                    // must be defined before the current service in document order.
                    baseService = tmpServices[getJPropertyValue(jpBase)];

                    // Create copies of what's inherited from the base service to mutate:
                    tokens = new Dictionary<string, string>(baseService.Tokens);
                    conn = baseService.Connection;
                    parameterTypes = new Dictionary<string, ParameterTypeDescriptor>(baseService.ParameterTypes, StringComparer.OrdinalIgnoreCase);

                    // Create clones of the base methods for mutation:
                    methods = new Dictionary<string, MethodDescriptor>(StringComparer.OrdinalIgnoreCase);
                    foreach (var baseMethod in baseService.Methods)
                    {
                        var newMethod = baseMethod.Value;
                        if (newMethod != null) newMethod = newMethod.Clone();
                        methods.Add(baseMethod.Key, newMethod);
                    }
                }
                else
                {
                    // Nothing inherited:
                    conn = null;
                    tokens = new Dictionary<string, string>(rootTokens, StringComparer.OrdinalIgnoreCase);
                    parameterTypes = new Dictionary<string, ParameterTypeDescriptor>(StringComparer.OrdinalIgnoreCase);
                    methods = new Dictionary<string, MethodDescriptor>(StringComparer.OrdinalIgnoreCase);
                }

                // Parse tokens:
                jpTokens = joService.Property("$");
                if (jpTokens != null)
                {
                    // Extract the key/value pairs onto our token dictionary:
                    foreach (var prop in ((JObject)jpTokens.Value).Properties())
                        tokens[prop.Name] = getJPropertyValue(prop);
                }

                // Parse connection:
                var jpConnection = joService.Property("connection");
                if (jpConnection != null)
                {
                    // Completely override what has been inherited, if anything:
                    conn = new ConnectionDescriptor();
                    // Set the connection properties:
                    foreach (var prop in ((JObject)jpConnection.Value).Properties())
                        switch (prop.Name)
                        {
                            case "ds": conn.DataSource = getJPropertyValue(prop); break;
                            case "ic": conn.InitialCatalog = getJPropertyValue(prop); break;
                            case "uid": conn.UserID = getJPropertyValue(prop); break;
                            case "pwd": conn.Password = getJPropertyValue(prop); break;
                            default: break;
                        }
                }

                var jpParameterTypes = joService.Property("parameterTypes");
                if (jpParameterTypes != null)
                {
                    // Define all the parameter types:
                    foreach (var prop in ((JObject)jpParameterTypes.Value).Properties())
                    {
                        parameterTypes[prop.Name] = null;
                    }
                }

                var jpMethods = joService.Property("methods");
                if (jpMethods != null)
                {
                    // Define all the methods:
                    foreach (var prop in ((JObject)jpMethods.Value).Properties())
                    {
                        methods[prop.Name] = null;
                    }
                }

                var desc = new ServiceDescriptor()
                {
                    Name = jpService.Name,
                    BaseService = baseService,
                    Connection = conn,
                    ParameterTypes = parameterTypes,
                    Methods = methods,
                    Tokens = tokens
                };

                // Add the parsed service descriptor:
                tmpServices.Add(jpService.Name, desc);
            }

            // 'aliases' section is optional:
            JToken jtAliases;
            if (doc.TryGetValue("aliases", out jtAliases))
            {
            }

            // The update must boil down to an atomic reference update:
            services = new SHA1Hashed<IDictionary<string, ServiceDescriptor>>(tmpServices, config.Hash);
            return true;
        }

        SHA1Hashed<JObject> ReadJSONStream(Stream input)
        {
            using (var hsr = new HsonReader(input, UTF8.WithoutBOM, true, 8192))
#if DEBUG
            // Send the JSON to Console.Out while it's being read:
            using (var tee = new TeeTextReader(hsr, Console.Out))
#endif
            using (var sha1 = new SHA1TextReader(tee, UTF8.WithoutBOM))
            using (var jr = new JsonTextReader(sha1))
            {
                var result = new SHA1Hashed<JObject>(Json.Serializer.Deserialize<JObject>(jr), sha1.GetHash());
#if DEBUG
                Console.WriteLine();
                Console.WriteLine();
#endif
                return result;
            }
        }

        async Task<SHA1Hashed<JObject>> FetchConfigData()
        {
            string url, path;
            bool noConfig = true;

            // Prefer to fetch over HTTP:
            if (localConfig.TryGetSingleValue("config.Url", out url))
            {
                noConfig = false;
                //Trace.WriteLine("Getting config data via HTTP");

                // Fire off a request now to our configuration server for our config data:
                try
                {
                    var req = HttpWebRequest.CreateHttp(url);
                    using (var rsp = await req.GetResponseAsync())
                    using (var rspstr = rsp.GetResponseStream())
                        return ReadJSONStream(rspstr);
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
                //Trace.WriteLine("Getting config data via file");

                // Load the local JSON file:
                try
                {
                    using (var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        return ReadJSONStream(fs);
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
            var services = this.services;

            if (context.Request.Url.AbsolutePath == "/")
                return new RedirectResponse("/foo");

            return new JsonResponse(new Dictionary<string, object> {
                { "hash", services.HashHexString },
                { "config", services.Value.Select(pair => new KeyValuePair<string, object>(pair.Key, new {
                    pair.Value.Name,
                    Base = pair.Value.BaseService != null ? pair.Value.BaseService.Name : null,
                    pair.Value.Tokens,
                    pair.Value.Connection,
                    pair.Value.ParameterTypes,
                    pair.Value.Methods
                })) }
            });
        }

        #endregion
    }
}
