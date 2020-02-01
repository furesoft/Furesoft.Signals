﻿using Furesoft.Signals;
using Furesoft.Signals.Backends;
using Newtonsoft.Json.Linq;
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
            var channel = Signal.CreateRecieverChannel("signals.test8");

            var json_res = Signal.CallMethod<JObject>(channel, 0xC0FFEE2);

            var strm = Signal.CreateSharedStream(channel);

            byte[] buffer = new byte[4];
            while (strm.Read(buffer, 0, buffer.Length) != 0)
            {
                Console.WriteLine(BitConverter.ToInt32(buffer));
            }

            var pw = Signal.CallMethod<string>(channel, 0xBEEF, 5);
            var pwd = channel.ToFunc<int, string>(0xBEEF)(5);

            Signal.CallEvent(channel, new PingArg { Message = "hello world" });

            var sig = Signal.GetSignatureOf(channel, 0xBADA33);
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