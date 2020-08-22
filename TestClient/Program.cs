using Furesoft.Signals;
using Furesoft.Signals.Attributes;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using TestModels;

namespace TestClient
{
    public sealed class RequireAuthAttribute : Attribute, IFuncFilter
    {
        public int Right { get; }

        public RequireAuthAttribute(int right)
        {
            Right = right;
        }

        public object AfterCall(MethodInfo mi, int id, object returnValue)
        {
            return returnValue;
        }

        public FuncFilterResult BeforeCall(MethodInfo mi, int id)
        {
            if (Right != 0x255362) return $"You dont habe enough rights to execute the function '0x{id.ToString("x").ToUpper()}";

            return Right == 0x255362;
        }
    }

    [Shared]
    internal class Program
    {
        [SharedFunction(0xBEEF)]
        [NoSignature]
        [RequireAuth(0x255362)]
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
        [Description("Json Test Method")]
        public static string JsonTest()
        {
            return "{ \"data\": {\"value\": true}}";
        }

        [SharedFunction(0xC0FFEE)]
        [Description("Handshake Method")]
        public static PingArg Pong(PingArg arg, bool active, object notnull)
        {
            if (notnull == null) throw new ArgumentException(nameof(notnull));

            return new PingArg { Message = "/PONG" };
        }

        private static SharedObject<int> shared;
        private static SharedObject<int[]> shared_arr;

        private static void Main(string[] args)
        {
            var queue = MessageQueue.CreateConsumer("signals.testqueue");
            queue.Subscribe<PingArg>(_ =>
            {
                Console.WriteLine(_.Message);
            });

            var channel = Signal.CreateSenderChannel("signals.test8");
            Signal.CollectAllShared(channel);

            

            channel.Dispose();
            Console.ReadLine();
        }
    }
}