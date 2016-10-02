using System;
using Akka.Actor;

namespace M2MqttAkkaNet
{
    public class Connect { }
    public class Connected { }
    public class Disconnected { }
    public class SubscriberTerminated
    {
        public IActorRef Reference { get; }
        public SubscriberTerminated(IActorRef reference)
        {
            Reference = reference;
        }
    }
    public class UnderlyingSubsAck
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
