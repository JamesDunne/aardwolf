using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using REST0.Definition;
using REST0.Implementation;

namespace REST0.APIService
{
    class Program
    {
        const int maxConnectionQueue = 100;

        static void Main(string[] args)
        {
            var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // Parse cmdline args as a list of 'key=value' pairs, where duplicates add new values to a running list per key.
            var aq = new Queue<string>(args);
            while (aq.Count > 0)
            {
                string arg = aq.Dequeue();

                // Split at first '=':
                int eqidx;
                if ((eqidx = arg.IndexOf('=')) == -1) continue;

                string key, value;
                key = arg.Substring(0, eqidx);
                value = arg.Substring(eqidx + 1);

                // Create the list of values for the key if necessary:
                List<string> list;
                if (!values.TryGetValue(key, out list))
                {
                    list = new List<string>();
                    values.Add(key, list);
                }

                // Add the value to the list:
                list.Add(value);
            }

            // Require at least one "bind" value:
            List<string> bindUriPrefixes;
            if (!values.TryGetValue("bind", out bindUriPrefixes) || bindUriPrefixes.Count == 0)
            {
                Console.Error.WriteLine("Require at least one bind=http://ip:port/ argument.");
                return;
            }

            // Create an HTTP host and start it:
            var host = new HttpAsyncHost(new APIHttpAsyncHandler(), maxConnectionQueue);
            host.SetConfigValues(values);
            host.Run(bindUriPrefixes.ToArray());
        }
    }
}
