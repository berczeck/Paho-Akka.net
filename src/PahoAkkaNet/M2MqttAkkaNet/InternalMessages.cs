using System;
using Akka.Actor;

namespace M2MqttAkkaNet
{
    internal class Connect { }
    internal class Connected { }
    internal class Disconnected { }
    internal class SubscriberTerminated
    {
        public IActorRef Reference { get; }
        public SubscriberTerminated(IActorRef reference)
        {
            Reference = reference;
        }
    }
    internal class UnderlyingSubsAck
    {
        public string Topic { get; }
        public Exception Fail { get; }
        public UnderlyingSubsAck(string topic, Exception fail)
        {
            Topic = topic;
            Fail = fail;
        }
    }
}
