using Furesoft.Signals;
using System;
using TestModels;

namespace TestSender
{
    internal class Program
    {
        private static SharedObject<int> shared;
        private static SharedObject<int[]> shared_arr;

        private static void Main(string[] args)
        {
            var channel = Signal.CreateRecieverChannel("signals.test2");

            Signal.CallEvent(channel, new PingArg { Message = "hello world" });

            var pw = Signal.CallMethod<string>(channel, 0xBEEF, 5);

            var sig = Signal.GetSignatureOf(channel, 0xBEEF);
            var res = Signal.CallMethod<PingArg>(channel, 0xC0FFEE, new PingArg { Message = "ping" }, true, "");

            shared = Signal.CreateSharedObject<int>(0xFF00DE);
            shared += (_) => Console.WriteLine(_);

            shared_arr = Signal.CreateSharedObject<int[]>(0xFF00DF);
            shared_arr += (_) => Console.WriteLine(_);

            shared_arr += new int[] { 42, 5, 3, 6 };

            channel.Dispose();
            Console.ReadLine();
        }
    }
}