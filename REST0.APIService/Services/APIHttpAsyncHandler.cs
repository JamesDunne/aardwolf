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

        static string getString(JProperty prop)
        {
            if (prop == null) return null;
            if (prop.Value.Type == JTokenType.Null) return null;
            return (string)((JValue)prop.Value).Value;
        }

        static bool? getBool(JProperty prop)
        {
            if (prop == null) return null;
            if (prop.Value.Type == JTokenType.Null) return null;
            return (bool?)((JValue)prop.Value).Value;
        }

        async Task<bool> RefreshConfigData()
        {
            // Get the latest config data:
            var config = await FetchConfigData();
            if (config == null) return false;

            // Parse the config document:
            var doc = config.Value;

            var tmpServices = new Dictionary<string, ServiceDescriptor>(StringComparer.OrdinalIgnoreCase);

            // Parse the root token dictionary first:
            var rootTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var jpTokens = doc.Property("$");
            if (jpTokens != null)
            {
                // Extract the key/value pairs onto a copy of the token dictionary:
                foreach (var prop in ((JObject)jpTokens.Value).Properties())
                    rootTokens[prop.Name] = getString(prop);
            }

            // Parse root parameter types:
            var rootParameterTypes = new Dictionary<string, ParameterTypeDescriptor>(StringComparer.OrdinalIgnoreCase);
            var jpParameterTypes = doc.Property("parameterTypes");
            parseParameterTypes(rootParameterTypes, (s) => s, jpParameterTypes);

            // 'services' section is not optional:
            JToken jtServices;
            if (!doc.TryGetValue("services", out jtServices))
                return false;
            var joServices = (JObject)jtServices;

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
                    baseService = tmpServices[getString(jpBase)];

                    // Create copies of what's inherited from the base service to mutate:
                    tokens = new Dictionary<string, string>(baseService.Tokens);
                    parameterTypes = new Dictionary<string, ParameterTypeDescriptor>(baseService.ParameterTypes, StringComparer.OrdinalIgnoreCase);
                    methods = new Dictionary<string, MethodDescriptor>(baseService.Methods, StringComparer.OrdinalIgnoreCase);

                    // Copy the connection descriptor:
                    conn = new ConnectionDescriptor()
                    {
                        DataSource = baseService.Connection.DataSource,
                        InitialCatalog = baseService.Connection.InitialCatalog,
                        UserID = baseService.Connection.UserID,
                        Password = baseService.Connection.Password,
                    };

                    // Recalculate the connection string:
                    var csb = new SqlConnectionStringBuilder(baseService.Connection.ConnectionString);
                    // TODO: Sanitize the application name
                    csb.ApplicationName = jpService.Name.Replace(';', '/');
                    conn.ConnectionString = csb.ToString();
                }
                else
                {
                    // Nothing inherited:
                    conn = null;
                    tokens = new Dictionary<string, string>(rootTokens, StringComparer.OrdinalIgnoreCase);
                    parameterTypes = new Dictionary<string, ParameterTypeDescriptor>(rootParameterTypes, StringComparer.OrdinalIgnoreCase);
                    methods = new Dictionary<string, MethodDescriptor>(StringComparer.OrdinalIgnoreCase);
                }

                // Parse tokens:
                jpTokens = joService.Property("$");
                if (jpTokens != null)
                {
                    // Extract the key/value pairs onto our token dictionary:
                    foreach (var prop in ((JObject)jpTokens.Value).Properties())
                        // NOTE(jsd): No interpolation over tokens themselves.
                        tokens[prop.Name] = getString(prop);
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
                            case "ds": conn.DataSource = getString(prop).Interpolate(tokenLookup); break;
                            case "ic": conn.InitialCatalog = getString(prop).Interpolate(tokenLookup); break;
                            case "uid": conn.UserID = getString(prop).Interpolate(tokenLookup); break;
                            case "pwd": conn.Password = getString(prop).Interpolate(tokenLookup); break;
                            default: break;
                        }

                    // Build the final connection string:
                    {
                        var csb = new System.Data.SqlClient.SqlConnectionStringBuilder();

                        csb.DataSource = conn.DataSource ?? String.Empty;
                        csb.InitialCatalog = conn.InitialCatalog ?? String.Empty;

                        string uid = conn.UserID;
                        string pwd = conn.Password;
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
                        // TODO: Sanitize the application name
                        csb.ApplicationName = jpService.Name.Replace(';', '/');
                        // TODO(jsd): Tune this parameter
                        csb.PacketSize = 32768;
                        //csb.WorkstationID = req.UserHostName;

                        // Finalize the connection string:
                        conn.ConnectionString = csb.ToString();
                    }
                }

                // Parse the parameter types:
                jpParameterTypes = joService.Property("parameterTypes");
                parseParameterTypes(parameterTypes, (s) => s.Interpolate(tokenLookup), jpParameterTypes);

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
                        method.DeprecatedMessage = getString(joMethod.Property("deprecated")).Interpolate(tokenLookup);

                        // TODO: parse "connection"

                        var jpParameters = joMethod.Property("parameters");
                        if (jpParameters != null)
                        {
                            method.Parameters = new Dictionary<string, ParameterDescriptor>(StringComparer.OrdinalIgnoreCase);
                            foreach (var jpParam in ((JObject)jpParameters.Value).Properties())
                            {
                                var joParam = ((JObject)jpParam.Value);
                                var sqlName = getString(joParam.Property("sqlName")).Interpolate(tokenLookup);
                                var typeName = getString(joParam.Property("type")).Interpolate(tokenLookup);
                                var isOptional = getBool(joParam.Property("optional")) ?? false;

                                var param = new ParameterDescriptor()
                                {
                                    Name = jpParam.Name,
                                    SqlName = sqlName,
                                    Type = parameterTypes[typeName],
                                    IsOptional = isOptional
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

                            // Check what type of query descriptor this is:
                            var sql = getString(joQuery.Property("sql")).Interpolate(tokenLookup);
                            if (sql != null)
                            {
                                // Raw SQL query; it must be a SELECT query but we can't validate that without
                                // some nasty parsing.

                                // Remove comments from the code:
                                sql = stripSQLComments(sql);
                                method.Query.SQL = sql;
                            }
                            else
                            {
                                // Parse the separated form of a query; this ensures that a SELECT query form is
                                // constructed.

                                // 'select' is required:
                                method.Query.Select = getString(joQuery.Property("select")).Interpolate(tokenLookup);
                                // The rest are optional:
                                method.Query.From = getString(joQuery.Property("from")).Interpolate(tokenLookup);
                                method.Query.Where = getString(joQuery.Property("where")).Interpolate(tokenLookup);
                                method.Query.GroupBy = getString(joQuery.Property("groupBy")).Interpolate(tokenLookup);
                                method.Query.Having = getString(joQuery.Property("having")).Interpolate(tokenLookup);
                                method.Query.OrderBy = getString(joQuery.Property("orderBy")).Interpolate(tokenLookup);
                                method.Query.CTEidentifier = getString(joQuery.Property("withCTEidentifier")).Interpolate(tokenLookup);
                                method.Query.CTEexpression = getString(joQuery.Property("withCTEexpression")).Interpolate(tokenLookup);

                                // Parse "xmlns:prefix": "http://uri.example.org/namespace" properties for WITH XMLNAMESPACES:
                                // TODO: Are xmlns namespace prefixes case-insensitive?
                                method.Query.XMLNamespaces = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                foreach (var jpXmlns in joQuery.Properties())
                                {
                                    if (!jpXmlns.Name.StartsWith("xmlns:")) continue;
                                    method.Query.XMLNamespaces.Add(jpXmlns.Name.Substring(6), getString(jpXmlns).Interpolate(tokenLookup));
                                }

                                // Strip out all SQL comments:
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
                                    (withCTEidentifier ?? String.Empty).Length + (withCTEexpression ?? String.Empty).Length + ";WITH  AS ()\r\n".Length
                                  + (select ?? String.Empty).Length + "SELECT ".Length
                                  + (from ?? String.Empty).Length + "\r\nFROM ".Length
                                  + (where ?? String.Empty).Length + "\r\nWHERE ".Length
                                  + (groupBy ?? String.Empty).Length + "\r\nGROUP BY ".Length
                                  + (having ?? String.Empty).Length + "\r\nHAVING ".Length
                                  + (orderBy ?? String.Empty).Length + "\r\nORDER BY ".Length
                                );

                                // This is a very conservative approach and will lead to false-positives for things like EXISTS() and sub-queries:
                                var errors = new List<string>(6);
                                if (containsSQLkeywords(select, "from", "into", "where", "group", "having", "order", "for"))
                                    errors.Add("SELECT clause cannot contain FROM, INTO, WHERE, GROUP BY, HAVING, ORDER BY, or FOR");
                                if (containsSQLkeywords(from, "where", "group", "having", "order", "for"))
                                    errors.Add("FROM clause cannot contain WHERE, GROUP BY, HAVING, ORDER BY, or FOR");
                                if (containsSQLkeywords(where, "group", "having", "order", "for"))
                                    errors.Add("WHERE clause cannot contain GROUP BY, HAVING, ORDER BY, or FOR");
                                if (containsSQLkeywords(groupBy, "having", "order", "for"))
                                    errors.Add("GROUP BY clause cannot contain HAVING, ORDER BY, or FOR");
                                if (containsSQLkeywords(having, "order", "for"))
                                    errors.Add("HAVING clause cannot contain ORDER BY or FOR");
                                if (containsSQLkeywords(orderBy, "for"))
                                    errors.Add("ORDER BY clause cannot contain FOR");

                                if (errors.Count > 0)
                                {
                                    // No query for you.
                                    method.Query.Errors = errors;
                                    method.Query.SQL = null;
                                }
                                else
                                {
                                    // Construct the query:
                                    bool didSemi = false;
                                    if (method.Query.XMLNamespaces.Count > 0)
                                    {
                                        didSemi = true;
                                        qb.AppendLine(";WITH XMLNAMESPACES (");
                                        using (var en = method.Query.XMLNamespaces.GetEnumerator())
                                            for (int i = 0; en.MoveNext(); ++i)
                                            {
                                                var xmlns = en.Current;
                                                qb.AppendFormat("  '{0}' AS {1}", xmlns.Value.Replace("\'", "\'\'"), xmlns.Key);
                                                if (i < method.Query.XMLNamespaces.Count - 1) qb.AppendLine(",");
                                                else qb.AppendLine();
                                            }
                                        qb.AppendLine(")");
                                    }
                                    if (!String.IsNullOrEmpty(withCTEidentifier) && !String.IsNullOrEmpty(withCTEexpression))
                                    {
                                        if (!didSemi) qb.Append(';');
                                        qb.AppendFormat("WITH {0} AS ({1})\r\n", withCTEidentifier, withCTEexpression);
                                    }
                                    qb.AppendFormat("SELECT {0}", select);
                                    if (!String.IsNullOrEmpty(from)) qb.AppendFormat("\r\nFROM {0}", from);
                                    if (!String.IsNullOrEmpty(where)) qb.AppendFormat("\r\nWHERE {0}", where);
                                    if (!String.IsNullOrEmpty(groupBy)) qb.AppendFormat("\r\nGROUP BY {0}", groupBy);
                                    if (!String.IsNullOrEmpty(having)) qb.AppendFormat("\r\nHAVING {0}", having);
                                    if (!String.IsNullOrEmpty(orderBy)) qb.AppendFormat("\r\nORDER BY {0}", orderBy);

                                    method.Query.SQL = qb.ToString();
                                }
                            }
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
                // Parse the named aliases:
                var joAliases = (JObject)jpAliases.Value;
                foreach (var alias in joAliases.Properties())
                {
                    // Add the existing ServiceDescriptor reference to the new name:
                    tmpServices.Add(alias.Name, tmpServices[getString(alias)]);
                }
            }

            // The update must boil down to an atomic reference update:
            services = new SHA1Hashed<IDictionary<string, ServiceDescriptor>>(tmpServices, config.Hash);

            return true;
        }

        static void parseParameterTypes(IDictionary<string, ParameterTypeDescriptor> parameterTypes, Func<string, string> interpolate, JProperty jpParameterTypes)
        {
            if (jpParameterTypes != null)
            {
                // Define all the parameter types:
                foreach (var jpParam in ((JObject)jpParameterTypes.Value).Properties())
                {
                    var jpType = ((JObject)jpParam.Value).Property("type");

                    var type = interpolate(getString(jpType));
                    int? length = null;
                    int? scale = null;

                    int idx = type.LastIndexOf('(');
                    if (idx != -1)
                    {
                        Debug.Assert(type[type.Length - 1] == ')');

                        int comma = type.LastIndexOf(',');
                        if (comma == -1)
                        {
                            length = Int32.Parse(type.Substring(idx + 1, type.Length - idx - 2));
                        }
                        else
                        {
                            length = Int32.Parse(type.Substring(idx + 1, comma - idx - 1));
                            scale = Int32.Parse(type.Substring(comma + 1, type.Length - comma - 2));
                        }

                        type = type.Substring(0, idx);
                    }

                    parameterTypes[jpParam.Name] = new ParameterTypeDescriptor()
                    {
                        Name = jpParam.Name,
                        Type = type,
                        Length = length,
                        Scale = scale,
                    };
                }
            }
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
                    Console.Error.WriteLine(ex.ToString());

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
                    Console.Error.WriteLine(ex.ToString());
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
                throw new Exception("{0} {1} left unclosed".F(pdepth, pdepth == 1 ? "parenthesis" : "parentheses"));
            }

            if (rec != -1)
            {
                if (keywords.Contains(s.Substring(rec, i - rec), StringComparer.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static JsonResult getErrorResponse(Exception ex)
        {
            JsonResultException jex;
            JsonSerializationException jsex;
            System.Data.SqlClient.SqlException sqex;

            object innerException = null;
            if (ex.InnerException != null)
                innerException = (object)getErrorResponse(ex.InnerException);

            if ((jex = ex as JsonResultException) != null)
            {
                return new JsonResult(jex.StatusCode, jex.Message);
            }
            else if ((jsex = ex as JsonSerializationException) != null)
            {
                object errorData = new
                {
                    type = ex.GetType().FullName,
                    message = ex.Message,
                    stackTrace = ex.StackTrace,
                    innerException
                };

                return new JsonResult(500, jsex.Message, new[] { errorData });
            }
            else if ((sqex = ex as System.Data.SqlClient.SqlException) != null)
            {
                return sqlError(sqex);
            }
            else
            {
                object errorData = new
                {
                    type = ex.GetType().FullName,
                    message = ex.Message,
                    stackTrace = ex.StackTrace,
                    innerException
                };

                return new JsonResult(500, ex.Message, new[] { errorData });
            }
        }

        static JsonResult sqlError(System.Data.SqlClient.SqlException sqex)
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
            return new JsonResult(statusCode, message, errorData);
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
            if (value == null) return DBNull.Value;
            if (value == "\0") return DBNull.Value;
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

        async Task<List<Dictionary<string, object>>> ReadResult(SqlDataReader dr)
        {
            int fieldCount = dr.FieldCount;

            // TODO: check if this is superfluous.
            var header = new string[fieldCount];
            for (int i = 0; i < fieldCount; ++i)
            {
                header[i] = dr.GetName(i);
            }

            var list = new List<Dictionary<string, object>>();

            // Enumerate rows asynchronously:
            while (await dr.ReadAsync())
            {
                var result = new Dictionary<string, object>();
                Dictionary<string, object> addTo = result;

                // Enumerate columns asynchronously:
                for (int i = 0; i < fieldCount; ++i)
                {
                    object col = await dr.GetFieldValueAsync<object>(i);
                    string name = header[i];

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
                            if (result.ContainsKey(objname))
                                throw new JsonResultException(400, "{0} key specified more than once".F(name));
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

        async Task<JsonResult> ExecuteQuery(HttpListenerRequest req, MethodDescriptor method)
        {
            if (method.Query.SQL == null)
            {
                return new JsonResult(500, "Malformed query descriptor", method.Query.Errors);
            }

            // Open a connection and execute the command:
            using (var conn = new System.Data.SqlClient.SqlConnection(method.Connection.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                // Add parameters:
                foreach (var param in method.Parameters)
                {
                    bool isValid = true;
                    string message = null;
                    object sqlValue;
                    string rawValue = req.QueryString[param.Key];

                    if (param.Value.IsOptional & (rawValue == null))
                    {
                        sqlValue = DBNull.Value;
                    }
                    else
                    {
                        try
                        {
                            sqlValue = getSqlValue(param.Value.Type.Type, rawValue);
                        }
                        catch (Exception ex)
                        {
                            isValid = false;
                            sqlValue = DBNull.Value;
                            message = ex.Message;
                        }
                    }

                    if (!isValid) return new JsonResult(400, "Invalid parameter value");

                    // Get the SQL type:
                    var sqlType = getSqlType(param.Value.Type.Type);

                    // Add the SQL parameter:
                    var sqlprm = cmd.Parameters.Add(param.Value.Name, sqlType);
                    if (param.Value.Type.Length != null) sqlprm.Precision = (byte)param.Value.Type.Length.Value;
                    if (param.Value.Type.Scale != null) sqlprm.Scale = (byte)param.Value.Type.Scale.Value;
                    sqlprm.SqlValue = sqlValue;
                }

                //cmd.CommandTimeout = 360;   // seconds
                cmd.CommandType = System.Data.CommandType.Text;
                // Set TRANSACTION ISOLATION LEVEL and optionally ROWCOUNT before the query:
                cmd.CommandText = @"SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;" + Environment.NewLine;
                //if (rowLimit > 0)
                //    cmd.CommandText += "SET ROWCOUNT {0};".F(rowLimit) + Environment.NewLine;
                cmd.CommandText += method.Query.SQL;

                try
                {
                    // Open the connection asynchronously:
                    await conn.OpenAsync();
                }
                catch (Exception ex)
                {
                    return getErrorResponse(ex);
                }

                // Execute the query:
                SqlDataReader dr;
                try
                {
                    // Execute the query asynchronously:
                    dr = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess | System.Data.CommandBehavior.CloseConnection);
                }
                catch (ArgumentException aex)
                {
                    // SQL Parameter validation only gives `null` for `aex.ParamName`.
                    return new JsonResult(400, aex.Message);
                }
                catch (Exception ex)
                {
                    return getErrorResponse(ex);
                }

                try
                {
                    var result = await ReadResult(dr);
                    var meta = new { };
                    return new JsonResult(result, meta);
                }
                catch (JsonResultException jex)
                {
                    return new JsonResult(jex.StatusCode, jex.Message);
                }
            }
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
                return new JsonResponse(200, "OK", new { success = true, message = String.Empty });
            }

            if (path[0] == "meta")
            {
                if (path.Length == 1)
                {
                    // Report all service descriptors:
                    return new JsonResponse(200, "OK", new
                    {
                        success = true,
                        statusCode = 200,
                        hash = services.HashHexString,
                        services = services.Value.ToDictionary(s => s.Key, s => s.Key != s.Value.Name ? (object)s.Value.Name : (object)new ServiceDescriptorSerialized(s.Value))
                    });
                }

                // Look up the service name:
                string serviceName = path[1];

                ServiceDescriptor desc;
                if (!services.Value.TryGetValue(serviceName, out desc))
                    return new JsonResponse(400, "Bad Request", new { success = false, message = "Unknown service name '{0}'".F(serviceName) });

                if (path.Length == 2)
                {
                    // Report this service descriptor:
                    return new JsonResponse(200, "OK", new
                    {
                        success = true,
                        statusCode = 200,
                        hash = services.HashHexString,
                        service = new ServiceDescriptorSerialized(desc)
                    });
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

                // Report this method descriptor:
                return new JsonResponse(200, "OK", new
                {
                    success = true,
                    statusCode = 200,
                    hash = services.HashHexString,
                    method = new MethodDescriptorSerialized(method)
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
                foreach (var param in method.Parameters)
                {
                    if (param.Value.IsOptional) continue;
                    if (!req.QueryString.AllKeys.Contains(param.Key))
                        return new JsonResponse(400, "Bad Request", new { success = false, message = "Missing required parameter '{0}'".F(param.Key) });
                }

                // Execute the query:
                var response = await ExecuteQuery(req, method);

                return new JsonResponse(response.statusCode, "OK", response);
            }
            else
            {
                return new JsonResponse(400, "Bad Request", new { success = false, message = "Unknown request type '{0}'".F(path[0]) });
            }
        }

        #endregion
    }
}
