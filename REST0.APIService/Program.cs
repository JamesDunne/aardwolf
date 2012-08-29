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
            // Parse the commandline arguments:
            var configValues = ConfigurationDictionary.Parse(args);

            // Require at least one "bind" value:
            List<string> bindUriPrefixes;
            if (!configValues.TryGetValue("bind", out bindUriPrefixes) || bindUriPrefixes.Count == 0)
            {
                Console.Error.WriteLine("Require at least one bind=http://ip:port/ argument.");
                return;
            }

            // Create an HTTP host and start it:
            var host = new HttpAsyncHost(new APIHttpAsyncHandler(), maxConnectionQueue);
            host.SetConfiguration(configValues);
            host.Run(bindUriPrefixes.ToArray());
        }
    }
}
