using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aardwolf.TestNull
{
    class Program
    {
        const int maxConnectionQueue = 100;

        static void Main(string[] args)
        {
            var host = new HttpAsyncHost(null);

            host.Run("http://+/");
        }
    }
}
