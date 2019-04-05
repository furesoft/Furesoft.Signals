using Furesoft.Signals;
using Furesoft.Signals.Attributes;
using Newtonsoft.Json;
using System;
using TestModels;

namespace TestClient
{
    [Shared]
    class Program
    {
        static SharedObject<int> shared;
        static SharedObject<int[]> shared_arr;

        static void Main(string[] args)
        {
            var channel = Signal.CreateSenderChannel("signals.test");

            Signal.Subscribe<PingArg>(channel, _ =>
            {
                Console.WriteLine(_.Message);
            });

            shared = Signal.CreateSharedObject<int>(0xFF00DE, true);
            shared += (_) => Console.WriteLine(_);
            shared_arr = Signal.CreateSharedObject<int[]>(0xFF00DF, true);
            shared_arr += (_) => Console.WriteLine(string.Join(',', _));

            Signal.CollectAllShared(channel);

            while (true) { 
                var input = Console.ReadLine();
                var arg = int.Parse(input);
                if (arg < 0) break;

                shared += arg;
            }
            
            Console.ReadLine();
        }

        [SharedFunction(0xC0FFEE)]
        public static PingArg Pong(PingArg arg)
        {
            return new PingArg { Message = "/PONG" };
        }
    }
}
