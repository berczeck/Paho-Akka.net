using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Akka.Actor;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Exceptions;

namespace M2MqttAkkaNet
{
    public class MqttPubSub : FSM<MqttClientStates, object>
    {
        private static string UrlEncode(string url) => HttpUtility.UrlEncode(url, Encoding.UTF8);
        private static string UrlDecode(string url) => HttpUtility.UrlDecode(url, Encoding.UTF8);

        private readonly MqttClient mqttClient;
        private readonly PSConfig psConfig;
        
        private readonly Queue<Tuple<long, Publish>> pubStash = new Queue<Tuple<long, Publish>>();
        private readonly Queue<Subscribe> subStash = new Queue<Subscribe>();

        private readonly List<Subscribe> subscribed = new List<Subscribe>();
        private readonly List<Subscribe> subscribing = new List<Subscribe>();

        private readonly Dictionary<ushort, string> subscribeRequest = new Dictionary<ushort, string>();

        private int connectCount = 0;

        public MqttPubSub(PSConfig config)
        {
            psConfig = config;
            mqttClient = new MqttClient(config.BrokerUrl);
            
            StartWith(MqttClientStates.Disconnected, new object());

            When(MqttClientStates.Disconnected, Disconnected);
            When(MqttClientStates.Connected, Connected);
            WhenUnhandled(UnHandled);

            Initialize();

            Self.Tell(new Connect());
        }

        private State<MqttClientStates, object> Disconnected(Event<object> @event)
        {
            if (@event.FsmEvent is Connect)
            {
                Context.System.Log.Info($"Connecting to {psConfig.BrokerUrl}");
                try
                {
                    mqttClient.Connect(Guid.NewGuid().ToString(), psConfig.UserName, psConfig.Password);
                    mqttClient.ConnectionClosed += MqttClient_ConnectionClosed;
                    mqttClient.MqttMsgPublished += MqttClient_MqttMsgPublished;
                    mqttClient.MqttMsgSubscribed += MqttClient_MqttMsgSubscribed;
                    mqttClient.MqttMsgPublishReceived += MqttClient_MqttMsgPublishReceived;
                    Self.Tell(new Connected());
                }
                catch (Exception ex) when (ex.GetType() != typeof(MqttConnectionException))
                {
                    Self.Tell(new Disconnected());
                    Context.System.Log.Error(ex, $"Can't connect to {psConfig.BrokerUrl}");
                    DelayConnect();
                }
                connectCount ++;
            }

            if (@event.FsmEvent is Connected)
            {
                connectCount = 0;
                subscribed.ForEach(x => DoSubscribe(x));
                while (subStash.Any())
                {
                    Self.Tell(subStash.Dequeue());
                }
                while (pubStash.Any())
                {
                    var publish = pubStash.Dequeue();
                    var notExpiredMessage = publish.Item1 > DateTime.UtcNow.Ticks;
                    if (notExpiredMessage)
                    {
                        Self.Tell(pubStash.Dequeue());
                    }
                }
                GoTo(MqttClientStates.Connected);
            }

            if (@event.FsmEvent is Publish)
            {
                if (pubStash.Count > psConfig.StashCapacity)
                {
                    while (pubStash.Count > psConfig.StashCapacity / 2)
                    {
                        pubStash.Dequeue();
                    }
                }
                var expiration = DateTime.UtcNow.Add(psConfig.StashTimeToLive).Ticks;
                pubStash.Enqueue(new Tuple<long, Publish>(expiration, @event.FsmEvent as Publish));
            }

            if (@event.FsmEvent is Subscribe)
            {
                subStash.Enqueue(@event.FsmEvent as Subscribe);
            }

            return Stay();
        }

        private void MqttClient_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            Self.Tell(new Message(e.Topic, e.Message));
        }

        private void MqttClient_MqttMsgSubscribed(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgSubscribedEventArgs e)
        {
            var topic = subscribeRequest[e.MessageId];
            Self.Tell(new UnderlyingSubsAck(topic, null));
            subscribeRequest.Remove(e.MessageId);
        }

        private void MqttClient_MqttMsgPublished(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishedEventArgs e)
        {
            var topic = subscribeRequest[e.MessageId];
            Context.System.Log.Debug($"Delivery {(e.IsPublished ? "completed" : "uncompleted")} for topic {topic}");
        }

        private void MqttClient_ConnectionClosed(object sender, EventArgs e)
        {
            Self.Tell(new Disconnected());
            Context.System.Log.Info($"Connection closed {psConfig.BrokerUrl}");
        }

        private State<MqttClientStates, object> Connected(Event<object> @event)
        {
            if (@event.FsmEvent is Publish)
            {
                var publish = @event.FsmEvent as Publish;
                try
                {
                    mqttClient.Publish(publish.Message.Topic, publish.Message.Payload);
                }
                catch (MqttClientException ex) when (!mqttClient.IsConnected)
                {
                    Context.System.Log.Error(ex, $"Can't publish to {publish.Message.Topic}");
                    Self.Tell(new Disconnected());
                    Self.Tell(publish);
                }
                catch (Exception ex)
                {
                    Context.System.Log.Error(ex, $"Publish call fail {publish.Message.Topic}");
                }
            }

            if (@event.FsmEvent is Subscribe)
            {
                var subscribe = @event.FsmEvent as Subscribe;
                var child = Context.Child(UrlEncode(subscribe.Topic));
                if(!child.IsNobody())
                {
                    child.Tell(subscribe);
                }
                else
                {
                    DoSubscribe(subscribe);
                }
            }

            if (@event.FsmEvent is Terminated)
            {
                var terminated = @event.FsmEvent as Terminated;
                try
                {
                    mqttClient.Unsubscribe(new string[] { UrlDecode(terminated.ActorRef.Path.Name) });
                }
                catch (Exception ex)
                {
                    Context.System.Log.Error(ex, $"Can't unsubscribe from {terminated.ActorRef.Path.Name}");
                }
            }
                return Stay();
        }

        private State<MqttClientStates, object> UnHandled(Event<object> @event)
        {
            if (@event.FsmEvent is Message)
            {
                var message = @event.FsmEvent as Message;
                Context.Child(UrlDecode(message.Topic)).Tell(message);
            }
            if (@event.FsmEvent is UnderlyingSubsAck)
            {
                var message = @event.FsmEvent as UnderlyingSubsAck;
                var topicSubs = subscribing.Where(x => x.Topic == message.Topic).ToList();

                if (message.Fail == null)
                {
                    var encTopic = UrlEncode(message.Topic);
                    IActorRef topicActor = Context.Child(encTopic);
                    if (!topicActor.IsNobody())
                    {
                        topicActor = Context.ActorOf<Topic>(encTopic);
                        Context.Watch(topicActor);
                    }
                    topicSubs.ForEach(x =>
                    {
                        subscribed.Add(x);
                        topicActor.Tell(x);
                    });
                }
                topicSubs.ForEach(x =>
                {
                    x.Reference.Tell(new SubscribeAck(x, message.Fail));
                    var subscription = subscribing.Find(t => t.Topic == x.Topic && t.Reference.Path == x.Reference.Path);
                    subscribing.Remove(subscription);
                });
            }
            if (@event.FsmEvent is SubscriberTerminated)
            {
                var message = @event.FsmEvent as SubscriberTerminated;

                var subscription = subscribed.Find(t => t.Reference.Path == message.Reference.Path);
                subscribing.Remove(subscription);
            }
            if (@event.FsmEvent is Disconnected)
            {
                DelayConnect();
                GoTo(MqttClientStates.Disconnected);
            }
            return Stay();
        }

        private void DoSubscribe(Subscribe sub)
        {
            if (subscribing.Any(x => x.Topic == sub.Topic || x.Qos < sub.Qos))
            {
                try
                {
                   var subscribeId = mqttClient.Subscribe(new string[] { sub.Topic}, new byte[] { sub.Qos });
                    subscribeRequest.Add(subscribeId, sub.Topic);
                }
                catch (MqttClientException ex) when(!mqttClient.IsConnected)
                {
                    Context.System.Log.Error(ex, $"Can't subscribe to {sub.Topic}");
                    Self.Tell(new Disconnected());
                    Self.Tell(sub);
                }
                catch (Exception ex)
                {
                    Context.System.Log.Error(ex, $"Subscribe call fail  {sub.Topic}");
                    Self.Tell(new UnderlyingSubsAck(sub.Topic, ex));
                }
            }
            subscribing.Add(sub);
        }
        
        private void DelayConnect()
        {
            var delay = psConfig.ConnectDelay(connectCount);
            SetTimer("Reconnect", new Connect(), TimeSpan.FromMilliseconds(delay));
        }
    }
}
