using System;
using System.Text;
using Akka.Actor;

namespace M2MqttAkkaNet.Client
{
    class Constants
    {
        public const string Topic = "topic/uno";
    }
    class Program
    {
        static void Main(string[] args)
        {
            using (var system = ActorSystem.Create("MqttSystem"))
            {
                var pubsub = system.ActorOf(Props.Create(typeof(MqttPubSub),new PSConfig("broker.hivemq.com")));
                pubsub.Tell(new Publish(new Message(Constants.Topic, Encoding.UTF8.GetBytes($"Hello moto {DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")}"))));
                Console.ReadLine();
            }
        }
    }

    public class SubscribeActor : ReceiveActor
    {
        public SubscribeActor(IActorRef pubSub)
        {
            pubSub.Tell(new Subscribe(Constants.Topic,Self));

            Receive<SubscribeAck>(x =>
            {
                if(x.Fail == null)
                {
                    //Context.Become(Ready);
                }
                else
                {
                    Context.System.Log.Error(x.Fail, $"Can't subscribe to {Constants.Topic}");
                }
            });
        }
    }
}
