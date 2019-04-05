using Furesoft.Signals;
using Furesoft.Signals.Attributes;
using System;
using TestModels;

namespace TestSender
{
    
    class Program
    {
        static SharedObject<int> shared;
        static SharedObject<int[]> shared_arr;

        static void Main(string[] args)
        {
            var channel = Signal.CreateRecieverChannel("signals.test");

            Signal.CallEvent(channel, new PingArg { Message = "hello world" });

            var res = Signal.CallMethod<PingArg>(channel, 0xC0FFEE, new PingArg { Message = "ping" });

            shared = Signal.CreateSharedObject<int>(0xFF00DE);
            shared += (_) => Console.WriteLine(_);

            shared_arr = Signal.CreateSharedObject<int[]>(0xFF00DF);
            shared_arr += (_) => Console.WriteLine(_);

            shared_arr += new int[] { 42, 5, 3, 6 };

            Console.ReadLine();
        }

        
    }

    
}
