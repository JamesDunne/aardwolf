using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace REST0.TestNull
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
