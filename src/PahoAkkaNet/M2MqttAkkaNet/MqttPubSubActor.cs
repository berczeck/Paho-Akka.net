using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Akka.Actor;
using uPLibrary.Networking.M2Mqtt.Exceptions;

namespace M2MqttAkkaNet
{
    public class MqttPubSubActor : FSM<MqttClientStates, object>
    {
        private static string UrlEncode(string url) => HttpUtility.UrlEncode(url, Encoding.UTF8);
        private static string UrlDecode(string url) => HttpUtility.UrlDecode(url, Encoding.UTF8);

        private readonly MqttClientWrapper mqttClient;
        private readonly MqttConfig psConfig;

        private readonly Queue<Tuple<long, Publish>> pubStash = new Queue<Tuple<long, Publish>>();
        private readonly Queue<Subscribe> subStash = new Queue<Subscribe>();

        private readonly List<Subscribe> subscribed = new List<Subscribe>();
        private readonly List<Subscribe> subscribing = new List<Subscribe>();

        private readonly Dictionary<ushort, string> subscribeRequest = new Dictionary<ushort, string>();

        private int connectCount = 0;

        public MqttPubSubActor(MqttConfig config)
        {
            psConfig = config;
            mqttClient = new MqttClientWrapper(config, Self);            
        }

        protected override void PreStart()
        {
            StartWith(MqttClientStates.Disconnected, new object());

            When(MqttClientStates.Disconnected, DisconnectedState);
            When(MqttClientStates.Connected, ConnectedState);
            WhenUnhandled(UnHandledMessages);

            Initialize();

            Self.Tell(new Connect());

            base.PreStart();
        }

        private State<MqttClientStates, object> DisconnectedState(Event<object> @event)
        {
            if (@event.FsmEvent is Connect)
            {
                ConnectEvent();
            }

            if (@event.FsmEvent is Connected)
            {
                ConnectedEvent();
                return GoTo(MqttClientStates.Connected);
            }

            if (@event.FsmEvent is Publish)
            {
                DisconnectedPublishEvent(@event.FsmEvent as Publish);
            }

            if (@event.FsmEvent is Subscribe)
            {
                subStash.Enqueue(@event.FsmEvent as Subscribe);
            }

            return null;
        }

        private State<MqttClientStates, object> ConnectedState(Event<object> @event)
        {
            if (@event.FsmEvent is Publish)
            {
                ConnectedPublishEvent(@event.FsmEvent as Publish);
            }

            if (@event.FsmEvent is Subscribe)
            {
                SubscribeEvent(@event.FsmEvent as Subscribe);
            }

            if (@event.FsmEvent is Terminated)
            {
                TerminadtedEvent(@event.FsmEvent as Terminated);
            }
            return null;
        }

        private State<MqttClientStates, object> UnHandledMessages(Event<object> @event)
        {
            if (@event.FsmEvent is Message)
            {
                var message = @event.FsmEvent as Message;
                Context.Child(UrlEncode(message.Topic)).Tell(message);
            }
            if (@event.FsmEvent is UnderlyingSubsOkAck)
            {
                UnderlyingSubsOkAckEvent(@event.FsmEvent as UnderlyingSubsOkAck);
            }

            if (@event.FsmEvent is UnderlyingSubsErrorAck)
            {
                UnderlyingSubsErrorAckEvent(@event.FsmEvent as UnderlyingSubsErrorAck);
            }
            if (@event.FsmEvent is SubscriberTerminated)
            {
                var message = @event.FsmEvent as SubscriberTerminated;
                var subscription = subscribed.Find(t => t.Reference.Path == message.Reference.Path);
                subscribing.Remove(subscription);
            }
            if (@event.FsmEvent is Disconnected)
            {
                Context.System.Log.Info($"Connection closed {psConfig.BrokerUrl}");
                DelayConnect();
                return GoTo(MqttClientStates.Disconnected);
            }
            return Stay();
        }

        private void DisconnectedPublishEvent(Publish @event)
        {
            if (pubStash.Count > psConfig.StashCapacity)
            {
                while (pubStash.Count > psConfig.StashCapacity / 2)
                {
                    pubStash.Dequeue();
                }
            }
            var expiration = DateTime.Now.Add(psConfig.StashTimeToLive).Ticks;
            pubStash.Enqueue(new Tuple<long, Publish>(expiration, @event));
        }

        private void ConnectedEvent()
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
                var notExpiredMessage = publish.Item1 > DateTime.Now.Ticks;
                if (notExpiredMessage)
                {
                    Self.Tell(publish.Item2);
                }
            }
        }

        private void ConnectEvent()
        {
            Context.System.Log.Info($"Connecting to {psConfig.BrokerUrl}");
            try
            {
                mqttClient.Connect();
            }
            catch (MqttConnectionException ex)
            {
                Context.System.Log.Error(ex, $"Can't connect to {psConfig.BrokerUrl}");
                Self.Tell(new Disconnected());
            }
            connectCount++;
        }     

        private void ConnectedPublishEvent(Publish @event)
        {
            if (!mqttClient.IsConnected)
            {
                Self.Tell(new Disconnected());
                DelayConnect();
            }
            
            try
            {
                mqttClient.Publish(@event.Message);
            }
            catch (MqttClientException ex)
            {
                Context.System.Log.Error(ex, $"Can't publish to {@event.Message.Topic}");
                Self.Tell(new Disconnected());
                Self.Tell(@event);
            }
            catch (Exception ex)
            {
                Context.System.Log.Error(ex, $"Publish call fail {@event.Message.Topic}");
            }
        }

        private void TerminadtedEvent(Terminated @event)
        {
            try
            {
                mqttClient.Unsubscribe(UrlDecode(@event.ActorRef.Path.Name));
            }
            catch (Exception ex)
            {
                Context.System.Log.Error(ex, $"Can't unsubscribe from {@event.ActorRef.Path.Name}");
            }
        }

        private void SubscribeEvent(Subscribe @event)
        {
            var topicActor = Context.Child(UrlEncode(@event.Topic));
            if (!topicActor.IsNobody())
            {
                topicActor.Tell(@event);
            }
            else
            {
                DoSubscribe(@event);
            }
        }
        
        private void UnderlyingSubsErrorAckEvent(UnderlyingSubsErrorAck @event)
        {
            var topicSubs = subscribing.Where(x => x.Topic == @event.Topic).ToList();

            topicSubs.ForEach(x =>
            {
                x.Reference.Tell(new SubscribeAck(x, @event.Fail));
                var subscription = subscribing.Find(t => t.Topic == x.Topic && t.Reference.Path == x.Reference.Path);
                subscribing.Remove(subscription);
            });
        }

        private void UnderlyingSubsOkAckEvent(UnderlyingSubsOkAck @event)
        {
            var topic = subscribeRequest[@event.MessageId];
            var topicSubs = subscribing.Where(x => x.Topic == topic).ToList();

            var encTopic = UrlEncode(topic);
            IActorRef topicActor = Context.Child(encTopic);
            if (topicActor.IsNobody())
            {
                topicActor = Context.ActorOf<MqttTopicActor>(encTopic);
                Context.Watch(topicActor);
            }
            topicSubs.ForEach(x =>
            {
                subscribed.Add(x);
                topicActor.Tell(x);
            });

            topicSubs.ForEach(x =>
            {
                x.Reference.Tell(new SubscribeAck(x, null));
                var subscription = subscribing.Find(t => t.Topic == x.Topic && t.Reference.Path == x.Reference.Path);
                subscribing.Remove(subscription);
            });
        }

        private void DoSubscribe(Subscribe sub)
        {
            if(!mqttClient.IsConnected)
            {
                Self.Tell(new Disconnected());
                return;
            }

            if (!subscribing.Any(x => x.Topic == sub.Topic && x.Qos < sub.Qos))
            {
                try
                {
                    var messageId = mqttClient.Subscribe(sub);
                    subscribeRequest.Add(messageId, sub.Topic);
                }
                catch (MqttClientException ex)
                {
                    Context.System.Log.Error(ex, $"Can't subscribe to {sub.Topic}");
                    Self.Tell(new Disconnected());
                    Self.Tell(sub);
                }
                catch (Exception ex)
                {
                    Context.System.Log.Error(ex, $"Subscribe call fail  {sub.Topic}");
                    Self.Tell(new UnderlyingSubsErrorAck(sub.Topic, ex));
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