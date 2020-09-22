using Furesoft.Signals;
using System;
using TestModels;

namespace TestSender
{
    internal class Program
    {
        private static SharedObject<int> shared;
        private static SharedObject<int[]> shared_arr;
        private static MessageQueue queue;

        private static async System.Threading.Tasks.Task Main(string[] args)
        {
            queue = MessageQueue.Open("signals.testqueue");
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            queue.Subscribe<TestMessage>(_ =>
            {
                Console.WriteLine(_.Message);
            });
            queue.Echo<TestMessage>();

            queue.Publish(new TestMessage { Message = "hello world" });

            await queue.Task;
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            queue.Dispose();
        }
    }
}