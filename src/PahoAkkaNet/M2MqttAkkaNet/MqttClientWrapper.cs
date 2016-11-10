using System;
using System.Text;
using Akka.Actor;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace M2MqttAkkaNet
{
    internal class MqttClientWrapper : MqttClient
    {
        private readonly MqttConfig _psConfig;
        private readonly IActorRef _owner;

        public MqttClientWrapper(MqttConfig config, IActorRef owner):base(config.BrokerUrl)
        {
            _psConfig = config;
            _owner = owner;
        }

        public void Connect()
        {
            Connect(DateTime.Now.ToString("akka_yyyyMMddhhmmssms"), _psConfig.UserName, _psConfig.Password);
            ConnectionClosed += MqttClient_ConnectionClosed;
            MqttMsgSubscribed += MqttClient_MqttMsgSubscribed;
            MqttMsgPublishReceived += MqttClient_MqttMsgPublishReceived;
            
            _owner.Tell(new Connected());
        }
        
        public ushort Publish(Message message)
        {
            var payLoad = Encoding.UTF8.GetBytes(message.Body); ;
            return Publish(message.Topic, payLoad);
        }

        public ushort Subscribe(Subscribe sub) 
            => Subscribe(new string[] { sub.Topic }, new byte[] { sub.Qos });

        public ushort Unsubscribe(string topic)
            => Unsubscribe(new string[] { topic });

        private void MqttClient_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            _owner.Tell(new Message(e.Topic, e.Message));
        }

        private void MqttClient_MqttMsgSubscribed(object sender, MqttMsgSubscribedEventArgs e)
        {
            _owner.Tell(new UnderlyingSubsOkAck(e.MessageId));
        }
        
        private void MqttClient_ConnectionClosed(object sender, EventArgs e)
        {
            _owner.Tell(new Disconnected());
        }
    }
}
