using System;
using System.Text;
using Akka.Actor;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace M2MqttAkkaNet
{
    public class Subscribe
    {
        public IActorRef Reference { get; }
        public string Topic { get; }
        public byte Qos { get; }

        public Subscribe(string topic, IActorRef reference, byte qos = MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE)
        {
            Reference = reference;
            Topic = topic;
            Qos = qos;
        }
    }

    public class SubscribeAck
    {
        public Subscribe Subscribe { get; }
        public Exception Fail { get; }

        public SubscribeAck(Subscribe subscribe, Exception fail)
        {
            Subscribe = subscribe;
            Fail = fail;
        }
    }

    public class Message
    {
        public string Topic { get; }
        public byte[] Payload { get; }

        public Message(string topic, string message)
        {
            Topic = topic;
            Payload = Encoding.UTF8.GetBytes(message);
        }

        public Message(string topic, byte[] payload)
        {
            Topic = topic;
            Payload = payload;
        }
    }
    public class Publish
    {
        public Message Message { get; }
        public byte Qos { get; }

        public Publish(Message message, byte qos = MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE)
        {
            Message = message;
            Qos = qos;
        }
    }
}
