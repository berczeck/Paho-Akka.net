using System;
using System.Text;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace M2MMqttClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var mqttClient = new MqttClient("broker.hivemq.com");//, 8000, false, null, null, MqttSslProtocols.None, null);
            mqttClient.ConnectionClosed += MqttClient_ConnectionClosed;
            mqttClient.MqttMsgPublished += MqttClient_MqttMsgPublished;
            mqttClient.MqttMsgPublishReceived += MqttClient_MqttMsgPublishReceived;
            mqttClient.MqttMsgSubscribed += MqttClient_MqttMsgSubscribed;
            mqttClient.MqttMsgUnsubscribed += MqttClient_MqttMsgUnsubscribed;

            var clientId = DateTime.Now.ToString("akka_yyyyMMddhhmmssms");
            byte code = mqttClient.Connect(clientId);
            Console.WriteLine($"Cliente connected: {mqttClient.IsConnected}");

            mqttClient.Subscribe(new string[] { "topic/uno" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });

            mqttClient.Publish("topic/uno", // topic
                              Encoding.UTF8.GetBytes("Hello world M2MMqttClient"), // message body
                              MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, // QoS level
                              false);

            Console.ReadLine();

            mqttClient.Unsubscribe(new string[] { "topic/uno" });

            mqttClient.Disconnect();

            Console.ReadLine();
        }

        private static void MqttClient_MqttMsgUnsubscribed(object sender, MqttMsgUnsubscribedEventArgs e)
        {
            Console.WriteLine($"Message {e.MessageId} unsubscribed");
        }

        private static void MqttClient_MqttMsgSubscribed(object sender, MqttMsgSubscribedEventArgs e)
        {
            Console.WriteLine($"Message {e.MessageId} subscribed");
        }

        private static void MqttClient_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            Console.WriteLine($"Message {e.Topic} received {Encoding.UTF8.GetString(e.Message)}");            
        }

        private static void MqttClient_MqttMsgPublished(object sender, MqttMsgPublishedEventArgs e)
        {
            Console.WriteLine($"Message {e.MessageId} published {e.IsPublished}");
        }

        private static void MqttClient_ConnectionClosed(object sender, EventArgs e)
        {
            Console.WriteLine("Connection Closed");
        }
    }
}
