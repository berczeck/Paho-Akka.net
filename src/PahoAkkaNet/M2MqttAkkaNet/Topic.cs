using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

namespace M2MqttAkkaNet
{
    public class Topic : ReceiveActor
    {
        private readonly List<IActorRef> subscribers = new List<IActorRef>();

        public Topic()
        {
            Receive<Message>(x => subscribers.ForEach(t => t.Tell(x)));
            Receive<Subscribe>(x =>
            {
                Context.Watch(x.Reference);
                subscribers.Add(x.Reference);
            });
            Receive<Terminated>(x =>
            {
                subscribers.Remove(x.ActorRef);
                Context.Parent.Tell(new SubscriberTerminated(x.ActorRef));
                if(!subscribers.Any())
                {
                    Context.Stop(Self);
                }
            });
        }
    }
}
