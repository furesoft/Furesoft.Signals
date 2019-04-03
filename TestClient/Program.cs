using Furesoft.Signals;
using System;
using TestModels;

namespace TestClient
{
    class Program
    {
        static SharedObject<int> shared;

        static void Main(string[] args)
        {
            var channel = Signal.CreateSenderChannel("signals.test");

            Signal.Subscribe<PingArg>(channel, _ =>
            {
                Console.WriteLine(_.Message);
            });

            shared = Signal.CreateSharedObject<int>(0xFF00DE, true);
            shared += (_) => Console.WriteLine(_);

            while(true)
            {
                var input = Console.ReadLine();
                var arg = int.Parse(input);

                if (arg < 0) break;

                shared.SetValue(arg);
            }

            Console.ReadLine();
        }
    }
}
