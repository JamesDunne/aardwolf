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
            var host = new HttpAsyncHost(new APIHttpAsyncHandler(), maxConnectionQueue);

            host.Run("http://*:80/");
        }
    }
}
