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
        public Fail Fail { get; }

        public SubscribeAck(Subscribe subscribe, Fail fail)
        {
            Subscribe = subscribe;
            Fail = fail;
        }

        public bool IsOk() => Fail == null;
    }

    public class Fail
    {
        public string Message { get; }
        public Fail(Exception exception)
        {
            Message = exception.Message;
        }
    }

    public class Message
    {
        public string Topic { get; }
        public string Body { get; }

        public Message(string topic, string body)
        {
            Topic = topic;
            Body = body;
        }

        public Message(string topic, byte[] payload)
        {
            Topic = topic;
            Body = Encoding.UTF8.GetString(payload);
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
