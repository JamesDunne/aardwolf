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
using System.Data.SqlClient;

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
                throw new Exception("Either '{0}' or '{1}' configuration keys are required".F("config.Url", "config.Path"));

            return null;
        }

        #endregion

        #region Query execution

        /// <summary>
        /// Correctly strips out all SQL comments, excluding false-positives from string literals.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        static string stripSQLComments(string s)
        {
            if (s == null) return null;

            StringBuilder sb = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] == '\'')
                {
                    // Skip strings.
                    sb.Append('\'');

                    ++i;
                    while (i < s.Length)
                    {
                        if ((i < s.Length - 1) && (s[i] == '\'') && (s[i + 1] == '\''))
                        {
                            // Skip the escaped quote char:
                            sb.Append('\'');
                            sb.Append('\'');
                            i += 2;
                        }
                        else if (s[i] == '\'')
                        {
                            sb.Append('\'');
                            ++i;
                            break;
                        }
                        else
                        {
                            sb.Append(s[i]);
                            ++i;
                        }
                    }
                }
                else if ((i < s.Length - 1) && (s[i] == '-') && (s[i + 1] == '-'))
                {
                    // Scan up to next '\r\n':
                    i += 2;
                    while (i < s.Length)
                    {
                        if ((i < s.Length - 1) && (s[i] == '\r') && (s[i + 1] == '\n'))
                        {
                            // Leave off the parser at the newline:
                            break;
                        }
                        else if ((s[i] == '\r') || (s[i] == '\n'))
                        {
                            // Leave off the parser at the newline:
                            break;
                        }
                        else ++i;
                    }

                    // All of the line comment is now skipped.
                }
                else if ((i < s.Length - 1) && (s[i] == '/') && (s[i + 1] == '*'))
                {
                    // Scan up to next '*/':
                    i += 2;
                    while (i < s.Length)
                    {
                        if ((i < s.Length - 1) && (s[i] == '*') && (s[i + 1] == '/'))
                        {
                            // Skip the end '*/':
                            i += 2;
                            break;
                        }
                        else ++i;
                    }

                    // All of the block comment is now skipped.
                }
                else if (s[i] == ';')
                {
                    // No ';'s allowed.
                    throw new Exception("No semicolons are allowed in any query clause");
                }
                else
                {
                    // Write out the character and advance the pointer:
                    sb.Append(s[i]);
                    ++i;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Checks each word in a SQL fragment against the <paramref name="keywords"/> list and returns true if any match.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="keywords"></param>
        /// <returns></returns>
        static bool containsSQLkeywords(string s, params string[] keywords)
        {
            if (s == null) return false;

            int rec = 0;
            int i = 0;
            int pdepth = 0;

            while (i < s.Length)
            {
                // Allow letters and underscores to pass for keywords:
                if (Char.IsLetter(s[i]) || s[i] == '_')
                {
                    if (rec == -1) rec = i;

                    ++i;
                    continue;
                }

                // Check last keyword only if at depth 0 of nested parens (this allows subqueries):
                if ((rec != -1) && (pdepth == 0))
                {
                    if (keywords.Contains(s.Substring(rec, i - rec), StringComparer.OrdinalIgnoreCase))
                        return true;
                }

                if (s[i] == '\'')
                {
                    // Process strings.

                    ++i;
                    while (i < s.Length)
                    {
                        if ((i < s.Length - 1) && (s[i] == '\'') && (s[i + 1] == '\''))
                        {
                            // Skip the escaped quote char:
                            i += 2;
                        }
                        else if (s[i] == '\'')
                        {
                            ++i;
                            break;
                        }
                        else ++i;
                    }

                    rec = -1;
                }
                else if ((s[i] == '[') || (s[i] == '"'))
                {
                    // Process quoted identifiers.

                    if (s[i] == '[')
                    {
                        // Bracket quoted identifier.
                        ++i;
                        while (i < s.Length)
                        {
                            if (s[i] == ']')
                            {
                                ++i;
                                break;
                            }
                            else ++i;
                        }
                    }
                    else if (s[i] == '"')
                    {
                        // Double-quoted identifier. Note that these are not strings.
                        ++i;
                        while (i < s.Length)
                        {
                            if ((i < s.Length - 1) && (s[i] == '"') && (s[i + 1] == '"'))
                            {
                                i += 2;
                            }
                            else if (s[i] == '"')
                            {
                                ++i;
                                break;
                            }
                            else ++i;
                        }
                    }

                    rec = -1;
                }
                else if (s[i] == ' ' || s[i] == '.' || s[i] == ',' || s[i] == '\r' || s[i] == '\n')
                {
                    rec = -1;

                    ++i;
                }
                else if (s[i] == '(')
                {
                    rec = -1;

                    ++pdepth;
                    ++i;
                }
                else if (s[i] == ')')
                {
                    rec = -1;

                    --pdepth;
                    if (pdepth < 0)
                    {
                        throw new Exception("Too many closing parentheses encountered");
                    }
                    ++i;
                }
                else if (s[i] == ';')
                {
                    // No ';'s allowed.
                    throw new Exception("No semicolons are allowed in any query clause");
                }
                else
                {
                    // Check last keyword:
                    if (rec != -1)
                    {
                        if (keywords.Contains(s.Substring(rec, i - rec), StringComparer.OrdinalIgnoreCase))
                            return true;
                    }

                    rec = -1;
                    ++i;
                }
            }

            // We must be at paren depth 0 here:
            if (pdepth > 0)
            {
                throw new Exception(String.Format("{0} {1} left unclosed", pdepth, pdepth == 1 ? "parenthesis" : "parentheses"));
            }

            if (rec != -1)
            {
                if (keywords.Contains(s.Substring(rec, i - rec), StringComparer.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static JsonResponse sqlError(System.Data.SqlClient.SqlException sqex)
        {
            int statusCode = 500;

            var errorData = new List<System.Data.SqlClient.SqlError>(sqex.Errors.Count);
            var msgBuilder = new StringBuilder(sqex.Message.Length);
            foreach (System.Data.SqlClient.SqlError err in sqex.Errors)
            {
                // Skip "The statement has been terminated.":
                if (err.Number == 3621) continue;

                errorData.Add(err);

                if (msgBuilder.Length > 0)
                    msgBuilder.AppendFormat("\n{0}", err.Message);
                else
                    msgBuilder.Append(err.Message);

                // Determine the HTTP status code to return:
                switch (sqex.Number)
                {
                    // Column does not allow NULLs.
                    case 515: statusCode = 400; break;
                    // Violation of UNIQUE KEY constraint '{0}'. Cannot insert duplicate key in object '{1}'.
                    case 2627: statusCode = 409; break;
                }
            }

            string message = msgBuilder.ToString();
            return new JsonResponse(statusCode, message, errorData);
        }

        List<Dictionary<string, object>> getJSONInflated(string[] header, IEnumerable<IEnumerable<object>> rows)
        {
            var list = new List<Dictionary<string, object>>();
            foreach (IEnumerable<object> row in rows)
            {
                var result = new Dictionary<string, object>();
                Dictionary<string, object> addTo = result;

                using (var rowen = row.GetEnumerator())
                    for (int i = 0; rowen.MoveNext(); ++i)
                    {
                        string name = header[i];
                        object col = rowen.Current;

                        if (name.StartsWith("__obj$"))
                        {
                            string objname = name.Substring(6);
                            if (String.IsNullOrEmpty(objname))
                                addTo = result;
                            else
                            {
                                if (col == DBNull.Value)
                                    addTo = null;
                                else
                                    addTo = new Dictionary<string, object>();
                                //if (result.ContainsKey(objname))
                                //    throw new JsonException(400, String.Format("{0} key specified more than once", name));
                                result.Add(objname, addTo);
                            }
                            continue;
                        }

                        if (addTo == null) continue;
                        addTo.Add(name, col);
                    }

                list.Add(result);
            }
            return list;
        }

        static System.Data.SqlDbType getSqlType(string type)
        {
            switch (type)
            {
                case "int": return System.Data.SqlDbType.Int;
                case "bit": return System.Data.SqlDbType.Bit;
                case "varchar": return System.Data.SqlDbType.VarChar;
                case "nvarchar": return System.Data.SqlDbType.NVarChar;
                case "char": return System.Data.SqlDbType.Char;
                case "nchar": return System.Data.SqlDbType.NChar;
                case "datetime": return System.Data.SqlDbType.DateTime;
                case "datetime2": return System.Data.SqlDbType.DateTime2;
                case "datetimeoffset": return System.Data.SqlDbType.DateTimeOffset;
                case "decimal": return System.Data.SqlDbType.Decimal;
                case "money": return System.Data.SqlDbType.Money;
                default: return System.Data.SqlDbType.VarChar;
            }
        }

        static object getSqlValue(string type, string value)
        {
            switch (type)
            {
                case "int": return new System.Data.SqlTypes.SqlInt32(Int32.Parse(value));
                case "bit": return new System.Data.SqlTypes.SqlBoolean(Boolean.Parse(value));
                case "varchar": return new System.Data.SqlTypes.SqlString(value);
                case "nvarchar": return new System.Data.SqlTypes.SqlString(value);
                case "char": return new System.Data.SqlTypes.SqlString(value);
                case "nchar": return new System.Data.SqlTypes.SqlString(value);
                case "datetime": return new System.Data.SqlTypes.SqlDateTime(DateTime.Parse(value));
                case "datetime2": return DateTime.Parse(value);
                case "datetimeoffset": return DateTimeOffset.Parse(value);
                case "decimal": return new System.Data.SqlTypes.SqlDecimal(Decimal.Parse(value));
                case "money": return new System.Data.SqlTypes.SqlMoney(Decimal.Parse(value));
                default: return new System.Data.SqlTypes.SqlString(value);
            }
        }

        async Task<IHttpResponseAction> ExecuteQuery(HttpListenerRequest req, MethodDescriptor method)
        {
            // Patch together a connection string:
            var csb = new System.Data.SqlClient.SqlConnectionStringBuilder();

            csb.DataSource = method.Connection.DataSource ?? String.Empty;
            csb.InitialCatalog = method.Connection.InitialCatalog ?? String.Empty;

            string uid = method.Connection.UserID;
            string pwd = method.Connection.Password;
            if (uid != null && pwd != null)
            {
                csb.IntegratedSecurity = false;
                csb.UserID = uid;
                csb.Password = pwd;
            }
            else
            {
                csb.IntegratedSecurity = true;
            }

            // Enable async processing:
            csb.AsynchronousProcessing = true;
            // Max 5-second connection timeout:
            csb.ConnectTimeout = 5;
            // Some defaults:
            csb.ApplicationName = "api";
            // TODO(jsd): Tune this parameter
            csb.PacketSize = 32768;
            csb.WorkstationID = req.UserHostName;

            // Finalize the connection string:
            var connString = csb.ToString();

            // Strip out all SQL comments:
            // TODO: XMLNamespaces!!
            string withCTEidentifier = stripSQLComments(method.Query.CTEidentifier);
            string withCTEexpression = stripSQLComments(method.Query.CTEexpression);
            string select = stripSQLComments(method.Query.Select);
            string from = stripSQLComments(method.Query.From);
            string where = stripSQLComments(method.Query.Where);
            string groupBy = stripSQLComments(method.Query.GroupBy);
            string having = stripSQLComments(method.Query.Having);
            string orderBy = stripSQLComments(method.Query.OrderBy);

            // Allocate a StringBuilder with enough space to construct the query:
            StringBuilder qb = new StringBuilder(
                (withCTEidentifier ?? "").Length + (withCTEexpression ?? "").Length + ";WITH  AS ()\r\n".Length
              + (select ?? "").Length + "SELECT ".Length
              + (from ?? "").Length + "\r\nFROM ".Length
              + (where ?? "").Length + "\r\nWHERE ".Length
              + (groupBy ?? "").Length + "\r\nGROUP BY ".Length
              + (having ?? "").Length + "\r\nHAVING ".Length
              + (orderBy ?? "").Length + "\r\nORDER BY ".Length
            );

            // Construct the query:
            if (!String.IsNullOrEmpty(withCTEidentifier) && !String.IsNullOrEmpty(withCTEexpression))
                qb.AppendFormat(";WITH {0} AS ({1})\r\n", withCTEidentifier, withCTEexpression);
            qb.AppendFormat("SELECT {0}", select);
            if (!String.IsNullOrEmpty(from)) qb.AppendFormat("\r\nFROM {0}", from);
            if (!String.IsNullOrEmpty(where)) qb.AppendFormat("\r\nWHERE {0}", where);
            if (!String.IsNullOrEmpty(groupBy)) qb.AppendFormat("\r\nGROUP BY {0}", groupBy);
            if (!String.IsNullOrEmpty(having)) qb.AppendFormat("\r\nHAVING {0}", having);
            if (!String.IsNullOrEmpty(orderBy)) qb.AppendFormat("\r\nORDER BY {0}", orderBy);

            // Finalize the query:
            string query = qb.ToString();

            // This is a very conservative approach and will lead to false-positives for things like EXISTS() and sub-queries:
            if (containsSQLkeywords(select, "from", "into", "where", "group", "having", "order", "for"))
                throw new ArgumentException("SELECT clause cannot contain FROM, INTO, WHERE, GROUP BY, HAVING, ORDER BY, or FOR");
            if (containsSQLkeywords(from, "where", "group", "having", "order", "for"))
                throw new ArgumentException("FROM clause cannot contain WHERE, GROUP BY, HAVING, ORDER BY, or FOR");
            if (containsSQLkeywords(where, "group", "having", "order", "for"))
                throw new ArgumentException("WHERE clause cannot contain GROUP BY, HAVING, ORDER BY, or FOR");
            if (containsSQLkeywords(groupBy, "having", "order", "for"))
                throw new ArgumentException("GROUP BY clause cannot contain HAVING, ORDER BY, or FOR");
            if (containsSQLkeywords(having, "order", "for"))
                throw new ArgumentException("HAVING clause cannot contain ORDER BY or FOR");
            if (containsSQLkeywords(orderBy, "for"))
                throw new ArgumentException("ORDER BY clause cannot contain FOR");

            // Open a connection and execute the command:
            using (var conn = new System.Data.SqlClient.SqlConnection(connString))
            using (var cmd = conn.CreateCommand())
            {
                // Set TRANSACTION ISOLATION LEVEL and optionally ROWCOUNT before the query:
                cmd.CommandText = @"SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;" + Environment.NewLine;
                //if (rowLimit > 0)
                //    cmd.CommandText += String.Format("SET ROWCOUNT {0};", rowLimit) + Environment.NewLine;
                cmd.CommandText += query;
                cmd.CommandType = System.Data.CommandType.Text;
                //cmd.CommandTimeout = 360;   // seconds

                // Add parameters:
                foreach (var param in method.Parameters)
                {
                    bool isValid = true;
                    string message = null;
                    object sqlValue;

                    try
                    {
                        sqlValue = getSqlValue(param.Value.Type.Type, req.QueryString[param.Key]);
                    }
                    catch (Exception ex)
                    {
                        isValid = false;
                        sqlValue = null;
                        message = ex.Message;
                    }

                    if (!isValid) return new JsonResponse(400, "Invalid parameter value", new { success = false, message });

                    // Get the SQL type:
                    var sqlType = getSqlType(param.Value.Type.Type);
                    // TODO: Length/Precision specifiers!

                    // Add the SQL parameter:
                    cmd.Parameters.Add(param.Value.Name, sqlType).SqlValue = sqlValue;
                }

                try
                {
                    // Open the connection:
                    conn.Open();
                }
                catch (SqlException ex)
                {
                    cmd.Dispose();
                    conn.Close();

                    return sqlError(ex);
                }

                // TODO: execute query!
            }

            // TODO
            return new JsonResponse(200, "OK", new { });
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
            var req = context.Request;

            // GET requests only:
            if (req.HttpMethod != "GET")
                return new JsonResponse(405, "Method Not Allowed", new { success = false });

            // Capture the current service configuration values only once per connection in case they update during:
            var services = this.services;

            // Split the path into component parts:
            string absPath = req.Url.AbsolutePath;
            string[] path;

            if (absPath == "/") path = new string[0];
            else path = absPath.Substring(1).Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (path.Length == 0)
            {
                // TODO: some descriptive information here.
                return new JsonResponse(200, "OK", new { success = true, message = "" });
            }

            if (path[0] == "meta")
            {
                if (path.Length == 1)
                {
                    // Report all service descriptors:
                    return new JsonResponse(200, "OK", new
                    {
                        hash = services.HashHexString,
                        services = services.Value.Select(pair => new
                        {
                            pair.Value.Name,
                            Base = pair.Value.BaseService != null ? pair.Value.BaseService.Name : null,
                            pair.Value.Connection,
                            pair.Value.Methods
                        }).ToDictionary(s => s.Name)
                    });
                }

                // Look up the service name:
                string serviceName = path[1];

                ServiceDescriptor desc;
                if (!services.Value.TryGetValue(serviceName, out desc))
                    return new JsonResponse(400, "Bad Request", new { success = false, message = "Unknown service name '{0}'".F(serviceName) });

                // Report this service descriptor:
                return new JsonResponse(200, "OK", new
                {
                    success = true,
                    service = new
                    {
                        desc.Name,
                        Base = desc.BaseService != null ? desc.BaseService.Name : null,
                        desc.Connection,
                        desc.Methods
                    }
                });
            }
            else if (path[0] == "data")
            {
                if (path.Length == 1)
                {
                    return new RedirectResponse("/meta");
                }

                // Look up the service name:
                string serviceName = path[1];

                ServiceDescriptor desc;
                if (!services.Value.TryGetValue(serviceName, out desc))
                    return new JsonResponse(400, "Bad Request", new { success = false, message = "Unknown service name '{0}'".F(serviceName) });

                if (path.Length == 2)
                {
                    return new RedirectResponse("/meta/{0}".F(serviceName));
                }
                if (path.Length > 3)
                {
                    return new JsonResponse(400, "Bad Request", new { success = false, message = "Too many path components supplied" });
                }

                // Find method:
                string methodName = path[2];
                MethodDescriptor method;
                if (!desc.Methods.TryGetValue(methodName, out method))
                    return new JsonResponse(400, "Bad Request", new { success = false, message = "Unknown method name '{0}'".F(methodName) });

                // TODO: Is it deprecated?

                // Check required parameters:
                foreach (var paramName in method.Parameters.Keys)
                {
                    if (!req.QueryString.AllKeys.Contains(paramName))
                        return new JsonResponse(400, "Bad Request", new { success = false, message = "Missing required parameter '{0}'".F(paramName) });
                }

                var response = await ExecuteQuery(req, method);
                return response;
            }
            else
            {
                return new JsonResponse(400, "Bad Request", new { success = false, message = "Unknown request type '{0}'".F(path[0]) });
            }
        }

        #endregion
    }
}
