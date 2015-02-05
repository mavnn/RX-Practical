using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveTester.Shared;

namespace EventTester
{
    class Program
    {
        static void Handler(Tuple<Guid, string> tuple)
        {
            Console.WriteLine("{0} - {1}", tuple.Item1, tuple.Item2);
        }

        static void Error(Exception e)
        {
            var err = Console.OpenStandardError();
            using (var writer = new System.IO.StreamWriter(err, Console.OutputEncoding))
            {
                writer.WriteLine("{0}", e);
            }
        }

        static void Main(string[] args)
        {
            using(var pub = new ChangeReciever("tcp://*:5555"))
            {
                Console.WriteLine("Listening...");

                var obs = Observable.FromEventPattern<Tuple<Guid, string>>(pub, "ChangeRecieved").Select(ep => ep.EventArgs);
                obs.Subscribe<Tuple<Guid, string>>(Handler);

                var staffSender = new NotificationSender("tcp://localhost:5556");
                var customerSender = new NotificationSender("tcp://localhost:5557");

                obs
                    .Subscribe<Tuple<Guid, string>>(t => customerSender.Send(t.Item1));

                var err = Observable.FromEventPattern<Exception>(pub, "OnError").Select(ep => ep.EventArgs);
                err.Subscribe<Exception>(Error);
                pub.Start();
                Console.ReadLine();
                Console.WriteLine("Closing down.");
            }
        }
    }
}
