using Furesoft.Signals;
using Furesoft.Signals.Attributes;
using System;
using System.ComponentModel;
using System.Text;
using TestModels;

namespace TestClient
{
    [Shared]
    internal class Program
    {
        private static SharedObject<int> shared;
        private static SharedObject<int[]> shared_arr;

        private static void Main(string[] args)
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

            while (true)
            {
                var input = Console.ReadLine();
                var arg = int.Parse(input);
                if (arg < 0) break;

                shared += arg;
            }

            Console.ReadLine();
        }

        [SharedFunction(0xC0FFEE)]
        [Description("Handshake Method")]
        public static PingArg Pong(PingArg arg, bool active, object notnull)
        {
            if (notnull == null) throw new ArgumentException(nameof(notnull));

            return new PingArg { Message = "/PONG" };
        }

        [SharedFunction(0xBEEF)]
        [NotTrack]
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
    }
}