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
            var queue = MessageQueue.Open("signals.testqueue");
            queue.Subscribe<TestMessage>(_ =>
            {
                Console.WriteLine(_.Message);
            });
            queue.Echo<TestMessage>();

            queue.Publish(new TestMessage { Message = "hello world" });

            Console.ReadLine();
        }
    }
}