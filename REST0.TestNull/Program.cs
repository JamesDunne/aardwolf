using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using REST0.Definition;
using REST0.Implementation;

namespace REST0
{
    class Program
    {
        const int maxConnectionQueue = 100;

        static void Main(string[] args)
        {
            var host = new HttpAsyncHost(null);

            host.Run("http://*:80/");
        }
    }
}
