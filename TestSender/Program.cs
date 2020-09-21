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
            Signal.EnableLogging();

            var queue = MessageQueue.Open("signals.testqueue");
            queue.Subscribe<TestMessage>(_ =>
            {
                Console.WriteLine(_.Message);
            });
            queue.Echo<TestMessage>();

            queue.Publish(new TestMessage { Message = "hello world" });

            var channel = Signal.CreateRecieverChannel("signals.test8");

            var sig = Signal.GetSignatureOf(channel, 0xBADA33);

            channel.Dispose();

            Console.ReadLine();
        }
    }
}