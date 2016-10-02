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
    public class UnderlyingSubsOkAck
    {
        public ushort MessageId { get; }
        public UnderlyingSubsOkAck(ushort messageId)
        {
            MessageId = messageId;
        }
    }

    public class UnderlyingSubsErrorAck
    {
        public string Topic { get; }
        public Fail Fail { get; }
        public UnderlyingSubsErrorAck(string topic, Exception exception)
        {
            Topic = topic;
            Fail = new Fail(exception);
        }
    }
}
