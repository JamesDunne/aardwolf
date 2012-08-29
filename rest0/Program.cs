using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace REST0
{
    class Program
    {
        const int maxConnectionQueue = 50;

        static void Main(string[] args)
        {
            var host = new HttpAsyncHost(null, maxConnectionQueue);

            host.Run("http://*:80/");
        }
    }
}
