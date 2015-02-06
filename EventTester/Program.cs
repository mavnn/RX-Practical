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
            // Nice docs (although Java): http://reactivex.io/RxJava/javadoc/rx/Observable.html
            // The challenge:
            // The ChangeReceiver will fire an event every time a change is received.
            // Events can be:
            // "Ignore!" -> don't do anything
            // "Change!" -> send notification to staff and customers
            // "StaffOnly!" -> send notification to staff
            // "CustomerOnly!" -> send notification to customer only
            //
            // Staff must be notified within 3 seconds.
            // Customers most be notified between 5 and 7 seconds.
            using(var pub = new ChangeReceiver("tcp://*:5555"))
            {
                Console.WriteLine("Listening...");

                var staffSender = new NotificationSender("tcp://localhost:5556");
                var customerSender = new NotificationSender("tcp://localhost:5557");

                //var obs = Observable.FromEventPattern<Tuple<Guid, string>>(pub, "ChangeRecieved").Select(ep => ep.EventArgs);
                //obs.Subscribe<Tuple<Guid, string>>(Handler);

                //var err = Observable.FromEventPattern<Exception>(pub, "OnError").Select(ep => ep.EventArgs);
                //err.Subscribe<Exception>(Error);

                pub.Start();
                Console.ReadLine();
                Console.WriteLine("Closing down.");
            }
        }
    }
}
