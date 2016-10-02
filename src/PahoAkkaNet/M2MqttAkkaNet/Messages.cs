using System;
using System.Text;
using Akka.Actor;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace M2MqttAkkaNet
{
    internal class Subscribe
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

    internal class SubscribeAck
    {
        private readonly Subscribe _subscribe;
        private readonly Exception _fail;

        public SubscribeAck(Subscribe subscribe, Exception fail)
        {
            _subscribe = subscribe;
            _fail = fail;
        }
    }

    internal class Message
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
    internal class Publish
    {
        public Message Message { get; }
        public byte Qos { get; }

        public Publish(Message message, byte qos)
        {
            Message = message;
            Qos = qos;
        }
    }
}
