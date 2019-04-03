using Furesoft.Signals;
using System;
using TestModels;

namespace TestSender
{
    class Program
    {
        static SharedObject<int> shared;

        static void Main(string[] args)
        {
            var channel = Signal.CreateRecieverChannel("signals.test");

            Signal.CallEvent(channel, new PingArg { Message = "hello world" });

            shared = Signal.CreateSharedObject<int>(0xFF00DE);
            shared += (_) => Console.WriteLine(_);

            Console.ReadLine();
        }

        private static PingArg Pong(PingArg arg)
        {
            return new PingArg { Message = arg.Message + "/PONG" };
        }
    }

    
}
