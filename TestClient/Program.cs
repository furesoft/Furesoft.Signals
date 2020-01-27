using Furesoft.Signals;
using Furesoft.Signals.Attributes;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using TestModels;

namespace TestClient
{
    public class RequireAuthAttribute : Attribute, IFuncFilter
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
            var channel = Signal.CreateSenderChannel("signals.test5");

            var strm = Signal.CreateSharedStream(channel);
            for (int i = 1; i <= 25; i++)
            {
                byte[] buffer = BitConverter.GetBytes(i);

                Console.WriteLine($"{i} is written on shared stream");

                strm.Write(buffer, 0, buffer.Length);
            }

            strm.Flush();
            Console.WriteLine("Shared stream flushed");

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

            channel.Dispose();
            Console.ReadLine();
        }
    }
}