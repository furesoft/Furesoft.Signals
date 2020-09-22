using Furesoft.Signals;
using Furesoft.Signals.Attributes;
using System;
using System.Text;
using TestModels;

namespace TestClient
{
    [Shared]
    internal class Program
    {
        [SharedFunction(0xBEEF)]
        public static string GetPass(int length)
        {
            string chars = "123456789abcdefghijklmopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ@/\\";
            var rndm = new Random();

            var res = new StringBuilder();

            for (int i = 0; i < length; i++)
            {
                res.Append(chars[rndm.Next(0, chars.Length - 1)]);
            }

            return res.ToString();
        }

        [SharedFunction(0xC0FFEE2)]
        public static string JsonTest()
        {
            return "{ \"data\": {\"value\": true}}";
        }

        [SharedFunction(0xC0FFEE)]
        public static PingArg Pong(PingArg arg, bool active, object notnull)
        {
            if (notnull == null) throw new ArgumentException(nameof(notnull));

            return new PingArg { Message = "/PONG" };
        }

        private static SharedObject<int> shared;
        private static SharedObject<int[]> shared_arr;
        private static MessageQueue queue;

        private static async System.Threading.Tasks.Task Main(string[] args)
        {
            queue = MessageQueue.Open("signals.testqueue");
            queue.Subscribe<TestMessage>(_ =>
            {
                Console.WriteLine(_.Message);
            });

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            queue.Echo<TestMessage>();

            var channel = Signal.OpenChannel("signals.test8");
            Signal.CollectAllShared(channel);

            await queue.Task;
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            queue.Dispose();
        }
    }
}