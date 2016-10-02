using System;
using System.Text;
using Akka.Actor;

namespace M2MqttAkkaNet.Client
{
    class Constants
    {
        public const string TopicOne = "topic/uno";
        public const string TopicTwo = "topic/dos";
    }
    class Program
    {
        static void Main(string[] args)
        {
            using (var system = ActorSystem.Create("MqttSystem"))
            {
                var pubsub = system.ActorOf(Props.Create(() => new MqttPubSubActor(new MqttConfig("broker.hivemq.com"))), "pubsub");

                var subscriberOne = system.ActorOf(Props.Create(() => new SubscribeActor(pubsub, Constants.TopicOne)),$"subscriberOne");
                var subscriberTwo = system.ActorOf(Props.Create(() => new SubscribeActor(pubsub, Constants.TopicTwo)), $"subscriberTwo");

                pubsub.Tell(new Publish(new Message(Constants.TopicOne, $"Hello moto {DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")} - {Guid.NewGuid()}")));               
                pubsub.Tell(new Publish(new Message(Constants.TopicTwo, $"Hello WORLD {DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")} - {Guid.NewGuid()}")));                
                pubsub.Tell(new Publish(new Message(Constants.TopicOne, $"Hello WORLD {DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")} - {Guid.NewGuid()}")));

                Console.WriteLine("PRESS ENTER TO CONTINUE");
                Console.ReadLine();
                subscriberOne.Tell(PoisonPill.Instance);
                pubsub.Tell(new Publish(new Message(Constants.TopicOne, $"Poison {DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")} - {Guid.NewGuid()}")));
                pubsub.Tell(new Publish(new Message(Constants.TopicTwo, $"Poison {DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")} - {Guid.NewGuid()}")));

                Console.WriteLine("PRESS ENTER TO CONTINUE");
                Console.ReadLine();
                subscriberTwo.Tell(PoisonPill.Instance);
                pubsub.Tell(new Publish(new Message(Constants.TopicTwo, $"Poison {DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")} - {Guid.NewGuid()}")));

                Console.WriteLine("PRESS ENTER TO FINISH");
                Console.ReadLine();
            }
        }
    }

    public class SubscribeActor : ReceiveActor
    {
        public SubscribeActor(IActorRef pubSub, string topic)
        {
            pubSub.Tell(new Subscribe(topic, Self));

            Receive<SubscribeAck>(x =>
            {
                if(x.IsOk())
                {
                    Become(Ready);
                    Context.System.Log.Info($"Subscription ok to topic {x.Subscribe.Topic}");
                }
                else
                {
                    Context.System.Log.Error($"Can't subscribe to {x.Subscribe.Topic} Error: {x.Fail}");
                }
            });            
        }

        private void Ready()
        {
            Receive<Message>(x => Console.WriteLine($"Message received: {x.Topic} {x.Body} {Self.Path}"));
        }
    }
}
