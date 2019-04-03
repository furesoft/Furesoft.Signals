using Furesoft.Signals;
using System;
using TestModels;

namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var channel = Signal.CreateSenderChannel("signals.test");

            Signal.Subscribe<PingArg>(channel, _ =>
            {
                Console.WriteLine(_.Message);
            });

            Console.ReadLine();
        }
    }
}
