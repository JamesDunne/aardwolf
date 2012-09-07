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

        static string getValue(JProperty prop)
        {
            if (prop == null) return null;
            if (prop.Value.Type == JTokenType.Null) return null;
            return (string)((JValue)prop.Value).Value;
        }

        static string interpolate(string input, Func<string, string> lookup)
        {
            if (input == null) return null;

            var sbMsg = new StringBuilder(input.Length);

            int i = 0;
            while (i < input.Length)
            {
                if (input[i] != '$')
                {
                    sbMsg.Append(input[i]);
                    ++i;
                    continue;
                }

                // We have a '$':
                ++i;
                if (i >= input.Length)
                {
                    sbMsg.Append('$');
                    break;
                }

                if (input[i] != '{')
                {
                    // Just a regular old character to go straight through to the final text:
                    sbMsg.Append('$');
                    sbMsg.Append(input[i]);
                    ++i;
                    continue;
                }

                // We have a '{':

                ++i;
                int start = i;
                while (i < input.Length)
                {
                    if (input[i] == '}') break;
                    ++i;
                }

                // We hit the end?
                if (i >= input.Length)
                {
                    // FAIL.
                    i = start;
                    sbMsg.Append('$');
                    sbMsg.Append('{');
                    continue;
                }

                // Did we hit a real '}' character?
                if (input[i] != '}')
                {
                    // Wasn't a token like we thought, just output the '{' and keep going from there:
                    i = start;
                    sbMsg.Append('$');
                    sbMsg.Append('{');
                    continue;
                }

                // We have a token, sweet...
                string tokenName = input.Substring(start, i - start);

                ++i;

                // Look up the token name:
                string replText = lookup(tokenName);
                if (replText != null)
                {
                    // Insert the token's value:
                    sbMsg.Append(replText);
                }
                else
                {
                    // Token wasn't found, so just insert the text raw:
                    sbMsg.Append('$');
                    sbMsg.Append('{');
                    sbMsg.Append(tokenName);
                    sbMsg.Append('}');
                }
            }

            return sbMsg.ToString();
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
                    rootTokens[prop.Name] = getValue(prop);
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
                    baseService = tmpServices[getValue(jpBase)];

                    // Create copies of what's inherited from the base service to mutate:
                    tokens = new Dictionary<string, string>(baseService.Tokens);
                    conn = baseService.Connection;
                    parameterTypes = new Dictionary<string, ParameterTypeDescriptor>(baseService.ParameterTypes, StringComparer.OrdinalIgnoreCase);
                    methods = new Dictionary<string, MethodDescriptor>(baseService.Methods, StringComparer.OrdinalIgnoreCase);
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
                        // NOTE(jsd): No interpolation over tokens themselves.
                        tokens[prop.Name] = getValue(prop);
                }

                // A lookup-or-null function used with `interpolate`:
                Func<string, string> tokenLookup = (key) =>
                {
                    string value;
                    if (!tokens.TryGetValue(key, out value))
                        return null;
                    return value;
                };

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
                            case "ds": conn.DataSource = interpolate(getValue(prop), tokenLookup); break;
                            case "ic": conn.InitialCatalog = interpolate(getValue(prop), tokenLookup); break;
                            case "uid": conn.UserID = interpolate(getValue(prop), tokenLookup); break;
                            case "pwd": conn.Password = interpolate(getValue(prop), tokenLookup); break;
                            default: break;
                        }
                }

                var jpParameterTypes = joService.Property("parameterTypes");
                if (jpParameterTypes != null)
                {
                    // Define all the parameter types:
                    foreach (var jpParam in ((JObject)jpParameterTypes.Value).Properties())
                    {
                        var jpType = ((JObject)jpParam.Value).Property("type");
                        parameterTypes[jpParam.Name] = new ParameterTypeDescriptor()
                        {
                            Name = jpParam.Name,
                            Type = interpolate(getValue(jpType), tokenLookup)
                        };
                    }
                }

                var jpMethods = joService.Property("methods");
                if (jpMethods != null)
                {
                    // Define all the methods:
                    foreach (var jpMethod in ((JObject)jpMethods.Value).Properties())
                    {
                        // Is the method set to null?
                        if (jpMethod.Value.Type == JTokenType.Null)
                        {
                            // Remove it:
                            methods.Remove(jpMethod.Name);
                            continue;
                        }
                        var joMethod = ((JObject)jpMethod.Value);

                        // Create a clone of the inherited descriptor or a new descriptor:
                        MethodDescriptor method;
                        if (methods.TryGetValue(jpMethod.Name, out method))
                            method = method.Clone();
                        else
                        {
                            method = new MethodDescriptor()
                            {
                                Name = jpMethod.Name,
                                Connection = conn
                            };
                        }
                        methods[jpMethod.Name] = method;

                        // Parse the definition:
                        method.DeprecatedMessage = interpolate(getValue(joMethod.Property("deprecated")), tokenLookup);

                        var jpParameters = joMethod.Property("parameters");
                        if (jpParameters != null)
                        {
                            method.Parameters = new Dictionary<string, ParameterDescriptor>(StringComparer.OrdinalIgnoreCase);
                            foreach (var jpParam in ((JObject)jpParameters.Value).Properties())
                            {
                                var joParam = ((JObject)jpParam.Value);
                                var param = new ParameterDescriptor()
                                {
                                    Name = jpParam.Name,
                                    SqlName = interpolate(getValue(joParam.Property("sqlName")), tokenLookup),
                                    Type = parameterTypes[interpolate(getValue(joParam.Property("type")), tokenLookup)]
                                };
                                method.Parameters.Add(jpParam.Name, param);
                            }
                        }
                        else if (method.Parameters == null)
                        {
                            method.Parameters = new Dictionary<string, ParameterDescriptor>(StringComparer.OrdinalIgnoreCase);
                        }

                        var jpQuery = joMethod.Property("query");
                        if (jpQuery != null)
                        {
                            var joQuery = (JObject)jpQuery.Value;
                            method.Query = new QueryDescriptor();
                            // 'from' and 'select' are required:
                            method.Query.From = interpolate(getValue(joQuery.Property("from")), tokenLookup);
                            method.Query.Select = interpolate(getValue(joQuery.Property("select")), tokenLookup);
                            // The rest are optional:
                            method.Query.Where = interpolate(getValue(joQuery.Property("where")), tokenLookup);
                            // TODO: more parts
                        }
                        else if (method.Query == null)
                        {
                            method.Query = new QueryDescriptor();
                        }
                    }
                }

                // Create the service descriptor:
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
            var jpAliases = doc.Property("aliases");
            if (jpAliases != null)
            {
            }

            // The update must boil down to an atomic reference update:
            services = new SHA1Hashed<IDictionary<string, ServiceDescriptor>>(tmpServices, config.Hash);

            return true;
        }

        SHA1Hashed<JObject> ReadJSONStream(Stream input)
        {
            using (var hsr = new HsonReader(input, UTF8.WithoutBOM, true, 8192))
#if TRACE
            // Send the JSON to Console.Out while it's being read:
            using (var tee = new TeeTextReader(hsr, (line) => Console.Write(line)))
            using (var sha1 = new SHA1TextReader(tee, UTF8.WithoutBOM))
#else
            using (var sha1 = new SHA1TextReader(hsr, UTF8.WithoutBOM))
#endif
            using (var jr = new JsonTextReader(sha1))
            {
                var result = new SHA1Hashed<JObject>(Json.Serializer.Deserialize<JObject>(jr), sha1.GetHash());
#if TRACE
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

            // Split the path into component parts:
            string absPath = context.Request.Url.AbsolutePath;
            string[] path;

            if (absPath == "/") path = new string[0];
            else path = absPath.Substring(1).Split('/');

            if (path.Length == 0)
            {
                // Report all service descriptors:
                return new JsonResponse(new Dictionary<string, object> {
                    { "hash", services.HashHexString },
                    { "config", services.Value.Select(pair => new KeyValuePair<string, object>(pair.Key, new {
                        pair.Value.Name,
                        Base = pair.Value.BaseService != null ? pair.Value.BaseService.Name : null,
                        pair.Value.Connection,
                        pair.Value.Methods
                    })) }
                });
            }
            else
            {
                // Look up the service name:
                ServiceDescriptor desc;
                if (!services.Value.TryGetValue(path[0], out desc))
                    return new JsonResponse(new { fail = true });

                if (path.Length == 1)
                {
                    // Report this service descriptor:
                    return new JsonResponse(new
                    {
                        service = new
                        {
                            desc.Name,
                            Base = desc.BaseService != null ? desc.BaseService.Name : null,
                            desc.Connection,
                            desc.Methods
                        }
                    });
                }
                else
                {
                }
            }

            return new JsonResponse(new { unknown = true });
        }

        #endregion
    }
}
