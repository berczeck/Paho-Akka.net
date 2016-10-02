using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

namespace M2MqttAkkaNet
{
    public class MqttTopicActor : ReceiveActor
    {
        private readonly List<IActorRef> subscribers = new List<IActorRef>();

        public MqttTopicActor()
        {
            Receive<Message>(x => subscribers.ForEach(t => t.Tell(x)));
            Receive<Subscribe>(x =>
            {
                Context.Watch(x.Reference);
                subscribers.Add(x.Reference);
            });
            Receive<Terminated>(x =>
            {
                Console.WriteLine($"Subscriber removed: {x.ActorRef.Path}");
                subscribers.Remove(x.ActorRef);
                Context.Parent.Tell(new SubscriberTerminated(x.ActorRef));
                if(!subscribers.Any())
                {
                    Console.WriteLine($"Topic {Self.Path.Name} stopped {Self.Path}");
                    Context.Stop(Self);
                }
            });
        }
    }
}
