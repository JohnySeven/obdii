using Elm327.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Obd2Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var arguments = ParseArguments(args);

            new App().Run(arguments);
        }

        private static Dictionary<string, string> ParseArguments(string[] args)
        {
            var ret = new Dictionary<string, string>();
            for (int i = 0; i < args.Length; i += 2)
            {
                var key = args[i];
                var value = args[i + 1];

                if (key.StartsWith("-"))
                {
                    key = key.Substring(1);
                }

                ret.Add(key, value);
            }

            return ret;
        }
    }
}
